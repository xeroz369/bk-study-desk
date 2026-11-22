using BKStudyDesk.Desktop.Platform;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Sources;
using SoHocTap.Web;

namespace BKStudyDesk.Desktop.Web;

internal sealed class MybkBrowser : IBrowserRunner
{
    private readonly HiddenWeb _hidden = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile WebPage? _app;
    private static readonly Pace ApiPace = new(() => TimeSpan.FromMilliseconds(
        Config.Int("sources.mybk.gapMs", 1500) + Random.Shared.Next(0, Math.Max(0, Config.Int("sources.mybk.gapJitterMs", 500)) + 1)));

    private const string FetchJs = """
        (async () => { const hid = document.getElementById('hid_Token'); const tok = hid && hid.value; try {
          const h = { 'Accept': 'application/json' };
          if (tok) h.Authorization = 'Bearer ' + tok;
          if (__BODY__ !== undefined) h['Content-Type'] = 'application/json; charset=utf-8';
          const signal = (typeof AbortSignal !== 'undefined' && AbortSignal.timeout) ? AbortSignal.timeout(__MS__) : undefined;
          const r = await fetch(__URL__, { method: __METHOD__, credentials: 'include', headers: h, body: __BODY__, signal });
          return JSON.stringify({ status: r.status, body: await r.text(), auth: !!tok });
        } catch (e) { return JSON.stringify({ status: -1, body: String(e), auth: !!tok }); } })()
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
                    var limit = r.Timeout ?? ScriptTimeout;
                    var js = FetchJs.Replace("__URL__", JsonSerializer.Serialize(r.Url, DesktopJson.Default.String)).Replace("__METHOD__", JsonSerializer.Serialize(r.Method, DesktopJson.Default.String))
                        .Replace("__BODY__", r.Body is null ? "undefined" : JsonSerializer.Serialize(r.Body, DesktopJson.Default.String))
                        .Replace("__MS__", ((int)Math.Max(1000, limit.TotalMilliseconds - 3000)).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    await ApiPace.WaitAsync(ct);
                    var sw = Stopwatch.StartNew();
                    var o = await EvalOnAppAsync(js, limit, ct) is { } raw ? JsonNode.Parse(raw) as JsonObject : null;
                    var res = o is null ? new FetchResult(-1, "không có phản hồi (quá giờ)", false)
                        : new FetchResult(o["status"]?.GetValue<int>() ?? 0, o["body"]?.GetValue<string>() ?? "", o["auth"]?.GetValue<bool>() ?? false);
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
                var retries = 0;
                Func<Uri, bool> failAt = u => u.AbsolutePath.Contains("/cas/login", StringComparison.Ordinal);
                await page.NavigateUntilAsync(url, u =>
                {
                    if (u.Host != target.Host || !u.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) return false;
                    if (SchoolUrls.PagePath(u) == SchoolUrls.PagePath(target)) return true;
                    if (retries++ < 2) page.Navigate(url);
                    return false;
                }, ct, failAt);
                for (var again = 0; mustContain is not null && !await HasTextAsync(page, mustContain, ct) && again < 3; again++)
                {
                    Log.Info($"MyBK ẩn: {Log.Where(page.Source?.AbsoluteUri ?? "")} chưa có nội dung cần đọc, mở lại lần {again + 1}");
                    await page.NavigateUntilAsync(url, u => u.Host == target.Host && SchoolUrls.PagePath(u) == SchoolUrls.PagePath(target), ct, failAt);
                }
                return await page.EvalAsync("document.documentElement.outerHTML") ?? "";
            }
            catch (Exception e) when (e is TimeoutException or HttpRequestException) { Reset(e); throw; }
            finally { _gate.Release(); }
        });

    private static async Task<bool> HasTextAsync(WebPage page, string text, CancellationToken ct)
    {
        var seconds = Config.Int("sources.mybk.contentSeconds", 10);
        var js = """
            new Promise(done => {
              const want = __TEXT__.toLowerCase();
              const has = () => (document.documentElement.textContent || '').toLowerCase().includes(want);
              if (has()) return done(true);
              const watch = new MutationObserver(() => { if (has()) { watch.disconnect(); done(true); } });
              watch.observe(document.documentElement, { childList: true, subtree: true, characterData: true });
              setTimeout(() => { watch.disconnect(); done(has()); }, __MS__);
            })
            """.Replace("__TEXT__", JsonSerializer.Serialize(text, DesktopJson.Default.String))
               .Replace("__MS__", (seconds * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture));
        return await page.EvalAwaitAsync(js, TimeSpan.FromSeconds(seconds + 5), ct) == "true";
    }

    public void Reenter() => _app = null;

    public void Release() => Dispatcher.UIThread.Post(() =>
    {
        if (!_gate.Wait(0)) return;
        try { _hidden.Close(); _app = null; }
        finally { _gate.Release(); }
    });

    private void Reset(Exception why)
    {
        Log.Warn($"MyBK: bỏ trình duyệt ẩn sau lỗi ({why.GetType().Name}), lần sau tạo mới");
        _hidden.Close();
        _app = null;
    }

    private async Task<string?> EvalOnAppAsync(string js, TimeSpan limit, CancellationToken ct)
    {
        await EnsureOnAppAsync(ct);
        var page = Page();
        try { return await page.EvalAwaitAsync(js, limit, ct); }
        catch (InvalidOperationException) when (!_hidden.Holds(page))
        {
            Log.Info("MyBK: trình duyệt ẩn mất giữa chừng, vào lại /app rồi chạy lại");
            await EnsureOnAppAsync(ct);
            return await Page().EvalAwaitAsync(js, limit, ct);
        }
    }

    private async Task EnsureOnAppAsync(CancellationToken ct, bool afterAutoLogin = false)
    {
        if (_app is { } cur && _hidden.Holds(cur)) return;
        var page = Page();
        static bool OnApp(Uri u) => u.AbsolutePath.StartsWith("/app", StringComparison.Ordinal) && !u.AbsolutePath.Contains("/login") && !u.AbsolutePath.Contains("/401");
        try
        {
            await page.NavigateUntilAsync(Config.Str("sources.mybk.home"), OnApp, ct,
                failAt: u => u.AbsolutePath.Contains("/login", StringComparison.Ordinal) || u.AbsolutePath.Contains("/401", StringComparison.Ordinal));
            if (await page.EvalAsync("String(!!document.getElementById('hid_Token')?.value)") == "true")
            {
                _app = page;
                await SsoKeep.PersistAsync(page);
                return;
            }
        }
        catch (SessionExpiredException) { }
        var hosts = LoginHosts.FromConfig();
        var lane = await SsoGate.EnterAsync(ct);
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
            var renewed = !afterAutoLogin && await TryAutoLoginAsync(page, ct);
            lane.Dispose();
            if (!renewed)
            {
                SsoSession.Expired();
                throw;
            }
            await EnsureOnAppAsync(ct, afterAutoLogin: true);
            return;
        }
        finally { lane.Dispose(); }
        Log.Debug("MyBK: vào lại qua SSO");
        _app = page;
        SsoSession.MarkAlive();
        await SsoKeep.PersistAsync(page);
    }

    private async Task<bool> TryAutoLoginAsync(WebPage page, CancellationToken ct)
    {
        if (Platform.Credentials.Usable() is not { } c || !SsoGate.TryClaimFill(UserActivity.Active)) return false;
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
            SsoGate.FillResult(false);
            return false;
        }
        catch
        {
            SsoGate.FillAborted();
            throw;
        }
        SsoGate.FillResult(true);
        SsoSession.MarkLogin();
        await SsoKeep.PersistAsync(page);
        Log.Info("Tự đăng nhập lại: xong");
        return true;
    }

    private static Task<T> OnUi<T>(Func<Task<T>> work) => Dispatcher.UIThread.InvokeAsync(work);
}
