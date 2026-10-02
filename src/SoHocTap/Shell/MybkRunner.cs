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
    private readonly SemaphoreSlim _gate = new(1, 1);

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
                    var raw = await EvaluateAsync(js);
                    var o = JsonNode.Parse(raw ?? "{}") as JsonObject;
                    results[r.Name] = new FetchResult(o?["status"]?.GetValue<int>() ?? 0, o?["body"]?.GetValue<string>() ?? "", o?["auth"]?.GetValue<bool>() ?? false);
                }
                return (IReadOnlyDictionary<string, FetchResult>)results;
            }
            finally { _gate.Release(); }
        });

    public Task<string> PageAsync(string url, CancellationToken ct) =>
        UiThread.RunAsync(owner, async () =>
        {
            await _gate.WaitAsync(ct);
            try
            {
                var core = await CoreAsync();
                var prefix = new Uri(url).AbsolutePath;
                prefix = prefix[..(prefix.LastIndexOf('/') + 1)];
                _onApp = false;
                await NavigateUntilAsync(core, url, u => u.Host == new Uri(url).Host && u.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal), ct,
                    failAt: u => u.AbsolutePath.Contains("/cas/login", StringComparison.Ordinal));
                return await EvaluateAsync("document.documentElement.outerHTML") ?? "";
            }
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

    // ------------------------------------------------------------------ internal (chạy trên UI thread)

    private async Task<CoreWebView2> CoreAsync()
    {
        if (_controller is null)
        {
            var env = await WebHost.EnvironmentAsync();
            _controller = await env.CreateCoreWebView2ControllerAsync(hwnd());
            _controller.IsVisible = false;
            // WebView ẩn chạy script của trang trường: chỉ cho đi tới host trường qua https, không mở cửa sổ mới.
            var core = _controller.CoreWebView2;
            core.NavigationStarting += (_, e) => { if (e.Uri != "about:blank" && !WebHost.IsSchoolDomain(e.Uri)) e.Cancel = true; };
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
        catch (InvalidOperationException) { /* session MyBK hết hạn, đi qua SSO */ }
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
        _onApp = true;
        await SessionKeeper.PersistAsync(core);
    }

    /// <summary>Mở url rồi chờ tới khi gặp trang thỏa <paramref name="arrived"/>. Gặp trang login (<paramref name="failAt"/>) thì dừng ngay vì session đã hết hạn,
    /// khỏi chờ đủ 60 giây timeout.</summary>
    private static async Task NavigateUntilAsync(CoreWebView2 core, string url, Func<Uri, bool> arrived, CancellationToken ct, Func<Uri, bool>? failAt = null)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string last = "";
        void OnCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e)
        {
            last = core.Source;
            if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var u)) return;
            if (failAt?.Invoke(u) == true) done.TrySetException(new InvalidOperationException("Chưa đăng nhập MyBK. Cần đăng nhập HCMUT trong app."));
            else if (arrived(u)) done.TrySetResult();
        }
        core.NavigationCompleted += OnCompleted;
        try
        {
            core.Navigate(url);
            var timeout = Task.Delay(TimeSpan.FromSeconds(60), ct);
            if (await Task.WhenAny(done.Task, timeout) == done.Task) await done.Task;   // có lỗi "chưa đăng nhập" thì throw ra luôn
            else
            {
                var where = Uri.TryCreate(last, UriKind.Absolute, out var u) ? u.Host + u.AbsolutePath : "?";
                throw new InvalidOperationException($"Phiên MyBK chưa sẵn sàng (đang ở {where}). Cần đăng nhập HCMUT trong app.");
            }
        }
        finally { core.NavigationCompleted -= OnCompleted; }
    }

    private async Task<string?> EvaluateAsync(string expression)
    {
        var core = await CoreAsync();
        var args = JsonSerializer.Serialize(new { expression, awaitPromise = true, returnByValue = true });
        var res = JsonNode.Parse(await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate", args));
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
