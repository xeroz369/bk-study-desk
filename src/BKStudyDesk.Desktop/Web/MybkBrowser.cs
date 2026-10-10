using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Sources;
using SoHocTap.Web;

namespace BKStudyDesk.Desktop.Web;

/// <summary>
/// MyBK chạy ẩn bằng đúng phiên SSO của app (bản đa nền tảng của Shell/MybkRunner): mở /app, chạy fetch() trong trang với token #hid_Token,
/// đọc trang HTML, giữ phiên SSO. MybkSource gọi từ thread nền; mọi việc với trình duyệt chạy trên UI thread. Hết phiên SSO mà người
/// dùng đã bật tự đăng nhập lại (mặc định tắt) thì điền tài khoản đã lưu một lần; không thì báo để người dùng đăng nhập lại.
/// </summary>
internal sealed class MybkBrowser : IBrowserRunner
{
    private readonly HiddenWeb _hidden = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WebPage? _app;   // trang đang ở /app (có #hid_Token); HiddenWeb bỏ trang đó (bị hủy) thì phải vào lại
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

    private static TimeSpan ScriptTimeout => TimeSpan.FromSeconds(Config.Int("sources.mybk.timeoutSeconds", 45));

    private WebPage Page() => _hidden.Open(u => SchoolUrls.IsSchoolDomain(u.AbsoluteUri));

    public Task<IReadOnlyDictionary<string, FetchResult>> FetchAsync(IReadOnlyList<FetchRequest> requests, CancellationToken ct, Action<string>? onEach = null) =>
        OnUi(async () =>
        {
            await _gate.WaitAsync(ct);
            try
            {
                var results = new Dictionary<string, FetchResult>();
                foreach (var r in requests)
                {
                    var js = FetchJs.Replace("__URL__", JsonSerializer.Serialize(r.Url)).Replace("__METHOD__", JsonSerializer.Serialize(r.Method))
                        .Replace("__BODY__", r.Body is null ? "undefined" : JsonSerializer.Serialize(r.Body));
                    await ApiPace.WaitAsync(ct);
                    var sw = Stopwatch.StartNew();
                    var o = JsonNode.Parse(await EvalOnAppAsync(js, ct) ?? "{}") as JsonObject;
                    var res = new FetchResult(o?["status"]?.GetValue<int>() ?? 0, o?["body"]?.GetValue<string>() ?? "", o?["auth"]?.GetValue<bool>() ?? false);
                    results[r.Name] = res;
                    Log.Debug($"MyBK {r.Name}: HTTP {res.Status}, {res.Body.Length / 1024} KB, {sw.ElapsedMilliseconds} ms");
                    onEach?.Invoke(r.Name);
                    if (Pace.IsThrottle(res.Status))
                    {
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
        OnUi(async () =>
        {
            await _gate.WaitAsync(ct);
            try
            {
                var page = Page();
                var target = new Uri(url);
                var prefix = target.AbsolutePath[..(target.AbsolutePath.LastIndexOf('/') + 1)];
                _app = null;
                // Hệ thống đăng ký môn (/dkmh) đi qua SSO rồi về trang chủ của nó trước: chỉ coi là tới khi đúng trang, rơi vào trang khác
                // cùng hệ thống thì mở lại (tối đa 2 lần). Lần đầu vào trong phiên, trang chỉ là trang trung gian: chờ rồi mở lại.
                var retries = 0;
                Func<Uri, bool> failAt = u => u.AbsolutePath.Contains("/cas/login", StringComparison.Ordinal);
                await page.NavigateUntilAsync(url, u =>
                {
                    if (u.Host != target.Host || !u.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) return false;
                    if (u.AbsolutePath == target.AbsolutePath) return true;
                    if (retries++ < 2) page.Navigate(url);
                    return false;
                }, ct, failAt);
                var html = await page.EvalAsync("document.documentElement.outerHTML") ?? "";
                for (var again = 0; mustContain is not null && !html.Contains(mustContain, StringComparison.OrdinalIgnoreCase) && again < 3; again++)
                {
                    Log.Info($"MyBK ẩn: {Log.Where(page.Source?.AbsoluteUri ?? "")} chưa có nội dung cần đọc ({html.Length} ký tự), mở lại lần {again + 1}");
                    await Task.Delay(1500, ct);
                    await page.NavigateUntilAsync(url, u => u.Host == target.Host && u.AbsolutePath == target.AbsolutePath, ct, failAt);
                    html = await page.EvalAsync("document.documentElement.outerHTML") ?? "";
                }
                return html;
            }
            catch (Exception e) when (e is TimeoutException or HttpRequestException) { Reset(e); throw; }
            finally { _gate.Release(); }
        });

    /// <summary>
    /// Giữ phiên SSO: mở cổng SSO của MyBK. SSO còn phiên thì cấp vé, chuyển về MyBK (phiên được tính là vừa dùng); hết phiên thì
    /// dừng ở trang đăng nhập. true = còn, false = hết, null = không biết (mất mạng, đang đồng bộ MyBK, server chậm).
    /// </summary>
    public Task<bool?> KeepAliveAsync(CancellationToken ct) =>
        OnUi<bool?>(async () =>
        {
            if (!await _gate.WaitAsync(0, ct)) return null;
            try
            {
                _app = null;
                var mybkHost = SchoolUrls.Host(Config.Str("sources.mybk.site"));
                try { await Page().NavigateUntilAsync(Config.Str("sources.mybk.casLogin"), u => u.Host == mybkHost, ct, failAt: u => SchoolUrls.IsSsoLogin(u.AbsoluteUri)); }
                catch (SessionExpiredException) when (Platform.Credentials.Usable() is not null)
                {
                    if (!await TryAutoLoginAsync(Page(), ct)) return false;
                }
                SsoSession.MarkAlive();
                await SsoKeep.PersistAsync(Page());
                return true;
            }
            catch (SessionExpiredException) { return false; }
            catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TimeoutException) { Log.Debug($"Giữ phiên SSO: {e.Message}"); Reset(e); return null; }
            finally { _gate.Release(); }
        });

    /// <summary>Đóng trình duyệt ẩn sau mỗi lượt để tắt process của nó (đỡ tốn pin). Đang bận thì để lần sau.</summary>
    public void Release() => Dispatcher.UIThread.Post(() =>
    {
        if (!_gate.Wait(0)) return;
        try { _hidden.Close(); _app = null; }
        finally { _gate.Release(); }
    });

    /// <summary>Bỏ trình duyệt ẩn sau một lần lỗi mạng hay quá giờ: process có thể đã treo; lần sau tạo mới, cookie vẫn còn.</summary>
    private void Reset(Exception why)
    {
        Log.Warn($"MyBK: bỏ trình duyệt ẩn sau lỗi ({why.GetType().Name}), lần sau tạo mới");
        _hidden.Close();
        _app = null;
    }

    /// <summary>
    /// Chạy một fetch trên trang /app. Trình duyệt ẩn bị hủy giữa chừng (HiddenWeb bỏ trang, script báo chưa có trang) thì vào lại /app
    /// trên trang mới và chạy lại một lần: mọi API MyBK app gọi đều chỉ đọc nên chạy lại không đổi gì trên trường.
    /// </summary>
    private async Task<string?> EvalOnAppAsync(string js, CancellationToken ct)
    {
        await EnsureOnAppAsync(ct);
        var page = Page();
        try { return await page.EvalAwaitAsync(js, ScriptTimeout, ct); }
        catch (InvalidOperationException) when (!_hidden.Holds(page))
        {
            Log.Info("MyBK: trình duyệt ẩn mất giữa chừng, vào lại /app rồi chạy lại");
            await EnsureOnAppAsync(ct);
            return await Page().EvalAwaitAsync(js, ScriptTimeout, ct);
        }
    }

    /// <summary>Vào /app: thử thẳng bằng phiên MyBK đã có; hết thì đi qua SSO: /my/homeSSO.action, /app/login?type=cas, /app/.</summary>
    private async Task EnsureOnAppAsync(CancellationToken ct, bool afterAutoLogin = false)
    {
        if (_app is { } cur && _hidden.Holds(cur)) return;
        var page = Page();
        static bool OnApp(Uri u) => u.AbsolutePath.StartsWith("/app", StringComparison.Ordinal) && !u.AbsolutePath.Contains("/login") && !u.AbsolutePath.Contains("/401");
        try
        {
            await page.NavigateUntilAsync(Config.Str("sources.mybk.home"), OnApp, ct,
                failAt: u => u.AbsolutePath.Contains("/login", StringComparison.Ordinal) || u.AbsolutePath.Contains("/401", StringComparison.Ordinal));
            if (await page.EvalAsync("String(!!document.getElementById('hid_Token')?.value)") == "true") { _app = page; return; }
        }
        catch (SessionExpiredException) { /* phiên MyBK hết, đi qua SSO */ }
        var hosts = LoginHosts.FromConfig();
        try
        {
            await page.NavigateUntilAsync(Config.Str("sources.mybk.casLogin"), u =>
            {
                var step = LoginSteps.Next(LoginStage.Sso, u, hosts);
                if (step == LoginAction.GoAppLogin) page.Navigate(Config.Str("sources.mybk.appLogin"));
                return step == LoginAction.MybkReady;
            }, ct, failAt: u => LoginSteps.Next(LoginStage.Sso, u, hosts) == LoginAction.NeedPassword);
        }
        catch (SessionExpiredException)
        {
            // Chỉ trang mật khẩu của SSO dừng ở đây: chính phiên SSO đã hết trên server. Người dùng cho phép thì tự đăng nhập lại một lần.
            if (afterAutoLogin || !await TryAutoLoginAsync(page, ct))
            {
                SsoSession.Expired();
                throw;
            }
            await EnsureOnAppAsync(ct, afterAutoLogin: true);   // vừa có phiên SSO mới: đi lại đường cũ, một lần
            return;
        }
        Log.Debug("MyBK: vào lại qua SSO");
        _app = page;
        SsoSession.MarkAlive();
        await SsoKeep.PersistAsync(page);
    }

    private bool _autoLoginTried;

    /// <summary>
    /// SSO vừa đòi mật khẩu (trang đăng nhập đang mở trong trang ẩn): người dùng cho phép (Credentials.Usable) thì điền tài khoản đã lưu
    /// và gửi. Một lần mỗi lần chạy app cho tới khi thành công: sai mật khẩu, có captcha hay trang đổi thì không thử lại, tránh bị trường
    /// khóa tài khoản; người dùng thấy báo đăng nhập lại như khi tắt tính năng này.
    /// </summary>
    private async Task<bool> TryAutoLoginAsync(WebPage page, CancellationToken ct)
    {
        if (_autoLoginTried || Platform.Credentials.Usable() is not { } c) return false;
        _autoLoginTried = true;
        var mybkHost = SchoolUrls.Host(Config.Str("sources.mybk.site"));
        try
        {
            await page.NavigateUntilAsync(null, u => u.Host == mybkHost, ct, failAt: u => SchoolUrls.IsSsoLogin(u.AbsoluteUri), begin: async () =>
            {
                if (await page.EvalAsync(SsoForm.FillScript(c.User, c.Password)) != "true")
                    throw new SessionExpiredException("Trang đăng nhập không có form như mong đợi (captcha hay đổi giao diện).");
            });
        }
        catch (SessionExpiredException e)
        {
            Log.Info($"Tự đăng nhập lại: không được ({e.Message}), cần đăng nhập trong app");
            return false;
        }
        _autoLoginTried = false;
        SsoSession.MarkLogin();
        Log.Info("Tự đăng nhập lại: xong");
        return true;
    }

    /// <summary>Chạy một việc async trên UI thread từ thread nào cũng được.</summary>
    private static Task<T> OnUi<T>(Func<Task<T>> work) => Dispatcher.UIThread.InvokeAsync(work);
}
