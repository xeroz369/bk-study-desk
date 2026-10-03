using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using SoHocTap.Core;
using System.Windows.Threading;
using SoHocTap.Sources;

namespace SoHocTap.Shell;

/// <summary>
/// WebView2 ẩn (gắn vào window chính qua HWND, không hiển thị) để làm việc với MyBK bằng đúng session SSO của app.
/// Mọi thao tác chạy trên UI thread qua <see cref="UiThread"/>; source MyBK thì gọi từ background thread.
/// Script chạy bằng DevTools Runtime.evaluate (awaitPromise) để await được kết quả fetch().
/// </summary>
internal sealed class MybkRunner(Dispatcher owner, Func<IntPtr> hwnd) : IBrowserRunner, IDisposable
{
    private CoreWebView2Controller? _controller;
    private bool _onApp;                     // đang ở /app (có #hid_Token)
    private bool _upgrading;                 // vừa hủy một điều hướng http để mở lại bằng https: bỏ qua NavigationCompleted lỗi của nó
    private string? _blocked;                // URL ngoài trường vừa bị chặn (NavigationStarting), để báo lỗi rõ thay vì chờ hết giờ
    private readonly SemaphoreSlim _gate = new(1, 1);
    // Giãn cách các request API MyBK trong một lần đồng bộ (sources.mybk.gapMs); server báo 429/503 thì nghỉ hẳn một lúc.
    private static readonly Pace ApiPace = new(() => TimeSpan.FromMilliseconds(Config.Int("sources.mybk.gapMs", 300)));

    // Bearer token lấy từ input ẩn #hid_Token của trang /app; token không bao giờ ra khỏi trang.
    private const string FetchJs = """
        (async () => { try {
          const hid = document.getElementById('hid_Token'); const tok = hid && hid.value;
          const h = { 'Accept': 'application/json' };
          if (tok) h.Authorization = 'Bearer ' + tok;
          if (__BODY__ !== undefined) h['Content-Type'] = 'application/json; charset=utf-8';
          const r = await fetch(__URL__, { method: __METHOD__, credentials: 'include', headers: h, body: __BODY__ });
          return JSON.stringify({ status: r.status, body: await r.text(), auth: !!tok });
        } catch (e) { return JSON.stringify({ status: -1, body: String(e), auth: false }); } })()
        """;

    public Task<IReadOnlyDictionary<string, FetchResult>> FetchAsync(IReadOnlyList<FetchRequest> requests, CancellationToken ct) =>
        UiThread.RunAsync(owner, async () =>
        {
            await _gate.WaitAsync(ct);
            try
            {
                await EnsureOnAppAsync(ct);
                var results = new Dictionary<string, FetchResult>();
                foreach (var r in requests)
                {
                    var js = FetchJs.Replace("__URL__", JsonSerializer.Serialize(r.Url)).Replace("__METHOD__", JsonSerializer.Serialize(r.Method))
                        .Replace("__BODY__", r.Body is null ? "undefined" : JsonSerializer.Serialize(r.Body));
                    await ApiPace.WaitAsync(ct);
                    var sw = Stopwatch.StartNew();
                    var raw = await EvaluateAsync(js);
                    var o = JsonNode.Parse(raw ?? "{}") as JsonObject;
                    var res = new FetchResult(o?["status"]?.GetValue<int>() ?? 0, o?["body"]?.GetValue<string>() ?? "", o?["auth"]?.GetValue<bool>() ?? false);
                    results[r.Name] = res;
                    Log.Debug($"MyBK {r.Name}: HTTP {res.Status}, {res.Body.Length / 1024} KB, {sw.ElapsedMilliseconds} ms");
                    if (Pace.IsThrottle(res.Status))
                    {
                        // MyBK đang giới hạn: nghỉ, bỏ các request còn lại của lần này (lần đồng bộ sau sẽ lấy).
                        ApiPace.PauseFor(Pace.Backoff(null));
                        Log.Warn($"MyBK {r.Name}: HTTP {res.Status}, tạm dừng gọi MyBK");
                        break;
                    }
                }
                return (IReadOnlyDictionary<string, FetchResult>)results;
            }
            catch (Exception e) when (e is TimeoutException or HttpRequestException) { Reset(e); throw; }
            finally { _gate.Release(); }
        });

    public Task<string> PageAsync(string url, CancellationToken ct, string? mustContain = null) =>
        UiThread.RunAsync(owner, async () =>
        {
            await _gate.WaitAsync(ct);
            try
            {
                var core = await CoreAsync();
                var target = new Uri(url);
                var prefix = target.AbsolutePath[..(target.AbsolutePath.LastIndexOf('/') + 1)];
                _onApp = false;
                // Hệ thống đăng ký môn (/dkmh) đi qua SSO rồi trả về trang chủ của nó (home.action) trước, không phải trang được hỏi.
                // Chỉ coi là tới khi đúng trang; rơi vào trang khác cùng hệ thống thì mở lại trang đó (tối đa 2 lần).
                var retries = 0;
                await NavigateUntilAsync(core, url, u =>
                {
                    if (u.Host != target.Host || !u.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) return false;
                    if (u.AbsolutePath == target.AbsolutePath) return true;
                    if (retries++ < 2) core.Navigate(url);
                    return false;
                }, ct, failAt: u => u.AbsolutePath.Contains("/cas/login", StringComparison.Ordinal));
                // Lần đầu vào /dkmh trong phiên, trang được hỏi chỉ là trang trung gian nhỏ, tự chuyển về home.action để khởi tạo
                // phiên của hệ thống đăng ký (thấy trên máy thật ở 1.1.5). Chưa có chữ cần có thì chờ trang đó chuyển xong rồi mở lại.
                var html = await EvaluateAsync("document.documentElement.outerHTML") ?? "";
                for (var again = 0; mustContain is not null && !html.Contains(mustContain, StringComparison.OrdinalIgnoreCase) && again < 3; again++)
                {
                    Log.Info($"MyBK ẩn: {Log.Where(core.Source)} chưa có nội dung cần đọc ({html.Length} ký tự), mở lại lần {again + 1}");
                    await Task.Delay(1500, ct);
                    await NavigateUntilAsync(core, url, u => u.Host == target.Host && u.AbsolutePath == target.AbsolutePath, ct,
                        failAt: u => u.AbsolutePath.Contains("/cas/login", StringComparison.Ordinal));
                    html = await EvaluateAsync("document.documentElement.outerHTML") ?? "";
                }
                return html;
            }
            catch (Exception e) when (e is TimeoutException or HttpRequestException) { Reset(e); throw; }
            finally { _gate.Release(); }
        });

    /// <summary>Đóng WebView2 ẩn sau mỗi lần sync để tắt hẳn process renderer cho đỡ tốn pin. Lần sau tự tạo lại.</summary>
    public void Release() => owner.InvokeAsync(async () =>
    {
        if (!await _gate.WaitAsync(0)) return;   // đang bận thì để lần sau
        try { _controller?.Close(); _controller = null; _onApp = false; }
        finally { _gate.Release(); }
    });

    public void Dispose() { _controller?.Close(); _gate.Dispose(); }

    /// <summary>
    /// Giữ phiên SSO: mở cổng SSO của MyBK trong WebView ẩn. SSO còn phiên thì cấp vé và chuyển về MyBK (phiên SSO được tính
    /// là vừa dùng, không bị hủy vì để lâu); hết phiên thì dừng ở trang đăng nhập. true = còn phiên, false = đã hết,
    /// null = không biết (mất mạng, app đang đồng bộ MyBK, server chậm).
    /// </summary>
    public Task<bool?> KeepAliveAsync(CancellationToken ct) =>
        UiThread.RunAsync<bool?>(owner, async () =>
        {
            if (!await _gate.WaitAsync(0, ct)) return null;   // đang đồng bộ MyBK: lần đồng bộ đó đã đi qua SSO rồi
            try
            {
                var core = await CoreAsync();
                _onApp = false;
                var mybkHost = WebHost.Host(Config.Str("sources.mybk.site"));
                await NavigateUntilAsync(core, Config.Str("sources.mybk.casLogin"), u => u.Host == mybkHost, ct,
                    failAt: u => WebHost.IsSsoLogin(u.AbsoluteUri));
                await SessionKeeper.PersistAsync(core);
                return true;
            }
            catch (SessionExpiredException) { return false; }
            catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TimeoutException) { Log.Debug($"Giữ phiên SSO: {e.Message}"); Reset(e); return null; }
            finally { _gate.Release(); }
        });

    // ------------------------------------------------------------------ internal (chạy trên UI thread)

    /// <summary>
    /// Bỏ WebView ẩn sau một lần lỗi mạng hoặc quá giờ: process của WebView có thể đã treo, dùng lại thì lần Thử lại cũng lỗi y hệt.
    /// Lần sau CoreAsync tạo WebView mới (cookie vẫn còn trong profile nên không phải đăng nhập lại).
    /// </summary>
    private void Reset(Exception why)
    {
        Log.Warn($"MyBK: bỏ WebView ẩn sau lỗi ({why.GetType().Name}), lần sau tạo mới");
        try { _controller?.Close(); } catch (InvalidOperationException) { }
        _controller = null;
        _onApp = false;
    }

    private async Task<CoreWebView2> CoreAsync()
    {
        if (_controller is null)
        {
            var env = await WebHost.EnvironmentAsync();
            _controller = await env.CreateCoreWebView2ControllerAsync(hwnd());
            _controller.IsVisible = false;
            // WebView ẩn chạy script của trang trường: chỉ cho đi tới host trường qua https, không mở cửa sổ mới.
            var ctl = _controller;
            var core = ctl.CoreWebView2;
            core.Settings.AreDevToolsEnabled = System.Diagnostics.Debugger.IsAttached;
            core.NavigationStarting += (_, e) =>
            {
                if (e.Uri == "about:blank" || WebHost.IsSchoolDomain(e.Uri)) return;
                e.Cancel = true;
                // MyBK hết phiên có lúc chuyển về http://mybk.../app/login. Cùng tên miền trường thì đổi sang https và đi tiếp
                // (trang login đó được nhận ra là hết phiên và đi lại qua SSO), không coi là trang ngoài trường.
                if (WebHost.HttpsOfSchoolDomain(e.Uri) is { } https)
                {
                    Log.Info($"MyBK ẩn: đổi http sang https {Log.Where(e.Uri)}");
                    _upgrading = true;
                    // So controller, không so CoreWebView2: mỗi lần đọc CoreWebView2 là một wrapper COM mới nên == luôn sai
                    // (lỗi ở 1.1.5, 1.1.6: không mở bản https, kẹt ở trang trước tới hết 45 giây).
                    owner.BeginInvoke(() => { if (ReferenceEquals(_controller, ctl)) core.Navigate(https); });
                    return;
                }
                _blocked = e.Uri;
                Log.Warn($"MyBK ẩn: chặn chuyển sang trang ngoài trường {Log.Where(e.Uri)} ({WebHost.Scheme(e.Uri)})");
            };
            // Process của WebView chết (hết RAM, bị diệt, cập nhật runtime): bỏ WebView này, lần sau tạo mới.
            core.ProcessFailed += (_, e) =>
            {
                Log.Warn($"MyBK ẩn: process WebView2 lỗi ({e.ProcessFailedKind}, {e.Reason})");
                if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited || e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited
                    || e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
                {
                    _controller = null;
                    _onApp = false;
                }
            };
            core.NewWindowRequested += (_, e) => e.Handled = true;
        }
        return _controller.CoreWebView2;
    }

    /// <summary>Vào /app: thử thẳng bằng session MyBK đã lưu (không cần SSO); session hết hạn thì đi qua SSO:
    /// /my/homeSSO.action → /app/login?type=cas → /app/.</summary>
    private async Task EnsureOnAppAsync(CancellationToken ct)
    {
        if (_onApp) return;
        var core = await CoreAsync();
        try
        {
            await NavigateUntilAsync(core, Config.Str("sources.mybk.home"),
                u => u.AbsolutePath.StartsWith("/app", StringComparison.Ordinal) && !u.AbsolutePath.Contains("/login") && !u.AbsolutePath.Contains("/401"), ct,
                failAt: u => u.AbsolutePath.Contains("/login", StringComparison.Ordinal) || u.AbsolutePath.Contains("/401", StringComparison.Ordinal));
            if (await EvaluateAsync("String(!!document.getElementById('hid_Token')?.value)") == "true")
            {
                _onApp = true;
                return;
            }
        }
        catch (SessionExpiredException) { /* session MyBK hết hạn, đi qua SSO */ }
        var appLogin = Config.Str("sources.mybk.appLogin");
        var mybkHost = WebHost.Host(Config.Str("sources.mybk.site"));
        var ssoHost = WebHost.Host(Config.Str("sources.mybk.casLogin"));
        await NavigateUntilAsync(core, Config.Str("sources.mybk.casLogin"), u =>
        {
            if (u.Host != mybkHost) return false;
            if (u.AbsolutePath.StartsWith("/my/", StringComparison.Ordinal) || u.AbsolutePath.TrimEnd('/') == "/app/login")
            {
                core.Navigate(appLogin);
                return false;
            }
            return u.AbsolutePath.StartsWith("/app", StringComparison.Ordinal) && !u.AbsolutePath.Contains("/login") && !u.AbsolutePath.Contains("/401");
        }, ct, failAt: u => u.Host == ssoHost && u.AbsolutePath.Contains("/login", StringComparison.Ordinal));
        Log.Debug("MyBK: vào lại qua SSO");
        _onApp = true;
        await SessionKeeper.PersistAsync(core);
    }

    /// <summary>
    /// Mở url rồi chờ tới khi gặp trang thỏa <paramref name="arrived"/>. Gặp trang login (<paramref name="failAt"/>) thì dừng ngay vì
    /// session đã hết (<see cref="SessionExpiredException"/>); mất mạng hoặc server lỗi thì báo <see cref="HttpRequestException"/>;
    /// quá sources.mybk.timeoutSeconds giây thì báo trang trường phản hồi chậm. Không để người dùng chờ mà không biết vì sao.
    /// </summary>
    private async Task NavigateUntilAsync(CoreWebView2 core, string url, Func<Uri, bool> arrived, CancellationToken ct, Func<Uri, bool>? failAt = null)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string last = "";
        void OnCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e)
        {
            last = core.Source;
            if (!e.IsSuccess && _upgrading && e.WebErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled)
            {
                _upgrading = false;   // điều hướng http bị hủy có chủ đích, bản https đang mở ngay sau
                return;
            }
            Log.Debug($"MyBK ẩn: {Log.Where(core.Source)} {(e.IsSuccess ? "ok" : e.WebErrorStatus.ToString())} HTTP {e.HttpStatusCode}");
            if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var u)) return;
            if (WebHost.NetworkError(e) is { } net) done.TrySetException(new HttpRequestException(net));
            else if (_blocked is { } b)
                done.TrySetException(new HttpRequestException($"MyBK chuyển sang trang ngoài trường ({Log.Where(b)}), app không mở trang này. Mở MyBK trên web để kiểm tra."));
            else if (!e.IsSuccess && u.Scheme == "about")
                // Điều hướng tới trường bị hủy hoặc lỗi trước khi rời trang trống: báo luôn, không chờ hết giờ.
                done.TrySetException(new HttpRequestException($"Không mở được MyBK ({e.WebErrorStatus}). Thử lại sau ít phút."));
            else if (failAt?.Invoke(u) == true) done.TrySetException(new SessionExpiredException("Phiên đăng nhập HCMUT đã hết hạn. Cần đăng nhập lại trong app."));
            else if (e.HttpStatusCode >= 500 || e.HttpStatusCode == 429)
                done.TrySetException(new HttpRequestException($"Máy chủ {u.Host} đang lỗi (HTTP {e.HttpStatusCode}). Thử lại sau ít phút.", null, (System.Net.HttpStatusCode)e.HttpStatusCode));
            else if (arrived(u)) done.TrySetResult();
        }
        _blocked = null;
        _upgrading = false;
        core.NavigationCompleted += OnCompleted;
        var seconds = Config.Int("sources.mybk.timeoutSeconds", 45);
        try
        {
            core.Navigate(url);
            var timeout = Task.Delay(TimeSpan.FromSeconds(seconds), ct);
            if (await Task.WhenAny(done.Task, timeout) == done.Task) await done.Task;   // có lỗi thì throw ra luôn
            else
            {
                ct.ThrowIfCancellationRequested();
                Log.Warn($"MyBK: quá {seconds} giây chưa tới trang cần, đang ở {Log.Where(last)}");
                throw new TimeoutException($"MyBK không phản hồi sau {seconds} giây (đang ở {Log.Where(last)}). Máy chủ trường có thể đang chậm, thử lại sau.");
            }
        }
        finally { core.NavigationCompleted -= OnCompleted; }
    }

    private async Task<string?> EvaluateAsync(string expression)
    {
        var core = await CoreAsync();
        var args = JsonSerializer.Serialize(new { expression, awaitPromise = true, returnByValue = true });
        var res = JsonNode.Parse(await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate", args));
        // Script lỗi trong trang (trang đổi cấu trúc, CSP chặn…): DevTools trả exceptionDetails thay vì value. Ghi log loại lỗi và
        // message của JS (không ghi nội dung trang), để biết vì sao MyBK "không trả gì" thay vì chỉ thấy rỗng.
        if (res?["exceptionDetails"] is JsonObject ex)
        {
            var what = ex["exception"]?["description"]?.ToString() ?? ex["text"]?.ToString() ?? "?";
            if (what.Length > 300) what = what[..300];
            Log.Warn($"MyBK ẩn: script lỗi ở dòng {ex["lineNumber"]}:{ex["columnNumber"]}: {what.ReplaceLineEndings(" ")}");
            return null;
        }
        return res?["result"]?["value"]?.GetValue<string>();
    }
}

/// <summary>Chạy một việc async trên UI thread từ thread nào cũng được, không block UI thread.</summary>
internal static class UiThread
{
    public static Task<T> RunAsync<T>(Dispatcher owner, Func<Task<T>> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.InvokeAsync(async () =>
        {
            try { tcs.SetResult(await work()); }
            catch (Exception e) { tcs.SetException(e); }
        });
        return tcs.Task;
    }
}
