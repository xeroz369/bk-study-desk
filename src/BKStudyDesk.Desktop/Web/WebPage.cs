using BKStudyDesk.Desktop.Platform;
using System.Net;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using SoHocTap.Core;
using SoHocTap.Sources;
using SoHocTap.Ui;
using SoHocTap.Web;

namespace BKStudyDesk.Desktop.Web;

internal sealed class WebPage
{
    private static readonly Guid AppleStore = new("6b0d7f3e-2c1a-4f5e-9a51-0b0000000001");

    private Uri? _upgrade;
    private string? _blocked;
    private int _completions;
    private Action<Uri, bool>? _loaded;

    public NativeWebView View { get; } = new();

    public Func<Uri, bool>? Allow { get; init; }

    public Uri? Source => View.Source;

    public WebPage()
    {
        View.EnvironmentRequested += (_, e) =>
        {
            e.EnableDevTools = System.Diagnostics.Debugger.IsAttached;
            switch (e)
            {
                case WindowsWebView2EnvironmentRequestedEventArgs w: w.UserDataFolder = Paths.WebViewProfile; break;
                case GtkWebViewEnvironmentRequestedEventArgs g: g.BaseDataDirectory = Paths.WebViewProfile; break;
                case AppleWKWebViewEnvironmentRequestedEventArgs a: a.DataStoreIdentifier = AppleStore; break;
            }
        };
        View.NewWindowRequested += (_, e) => e.Handled = true;
        View.NavigationStarted += (_, e) =>
        {
            if (e.Request is not { } u || Allow is null || u.Scheme == "about" || Allow(u)) return;
            e.Cancel = true;
            if (SchoolUrls.HttpsOfSchoolDomain(u.AbsoluteUri) is { } https)
            {
                Log.Info($"Web: đổi http sang https {Log.Where(u.AbsoluteUri)}");
                _upgrade = new Uri(https);
                DispatcherTimer.RunOnce(Upgrade, TimeSpan.FromSeconds(2));
                return;
            }
            _blocked = u.AbsoluteUri;
            Log.Warn($"Web: chặn trang ngoài trường {Log.Where(u.AbsoluteUri)} ({u.Scheme})");
        };
        View.NavigationCompleted += (_, e) =>
        {
            _completions++;
            if (_upgrade is not null && !e.IsSuccess) { Upgrade(); return; }
            if (View.Source is { } u) _loaded?.Invoke(u, e.IsSuccess);
            Loaded?.Invoke(View.Source, e.IsSuccess);
        };
    }

    private void Upgrade()
    {
        if (_upgrade is not { } target) return;
        _upgrade = null;
        View.Navigate(target);
        var seen = _completions;
        DispatcherTimer.RunOnce(() =>
        {
            if (_completions != seen) return;
            Log.Info($"Web: {Log.Where(target.AbsoluteUri)} chưa tải xong sau 10 giây, mở lại một lần");
            View.Navigate(target);
        }, TimeSpan.FromSeconds(10));
    }

    public event Action<Uri?, bool>? Loaded;

    public void Navigate(string url) => View.Navigate(new Uri(url));

    public async Task<string?> EvalAsync(string js) => Decode(await View.InvokeScript(js));

    public async Task<string?> EvalAwaitAsync(string asyncExpression, TimeSpan timeout, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N");
        await View.InvokeScript($$"""
            (window.__bk = window.__bk || {}); Promise.resolve().then(() => ({{asyncExpression}}))
              .then(v => { window.__bk['{{id}}'] = { ok: true, v: String(v) }; }, e => { window.__bk['{{id}}'] = { ok: false, v: String(e) }; }); 0
            """);
        var poll = $"(window.__bk && window.__bk['{id}']) ? JSON.stringify(window.__bk['{id}']) : null";
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            ct.ThrowIfCancellationRequested();
            if (await EvalAsync(poll) is { } raw)
            {
                await View.InvokeScript($"delete window.__bk['{id}']; 0");
                using var doc = JsonDocument.Parse(raw);
                var value = doc.RootElement.GetProperty("v").GetString();
                if (doc.RootElement.GetProperty("ok").GetBoolean()) return value;
                Log.Warn($"Web: script lỗi: {(value is { Length: > 300 } ? value[..300] : value)}");
                return null;
            }
            await Task.Delay(50, ct);
        }
        Log.Warn($"Web: script chưa xong sau {timeout.TotalSeconds:0} giây");
        return null;
    }

    public async Task<IReadOnlyList<Cookie>> CookiesAsync() =>
        View.TryGetCookieManager() is { } m ? await m.GetCookiesAsync() : [];

    public void SetCookie(Cookie c) => View.TryGetCookieManager()?.AddOrUpdateCookie(c);

    public async Task NavigateUntilAsync(string? url, Func<Uri, bool> arrived, CancellationToken ct, Func<Uri, bool>? failAt = null, Func<Task>? begin = null)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var last = "";
        _blocked = null;
        _loaded = (u, ok) =>
        {
            last = u.AbsoluteUri;
            Log.Debug($"Web: xong {Log.Where(last)} {(ok ? "ok" : "lỗi")}");
            if (_blocked is { } b)
                done.TrySetException(new HttpRequestException(L.F("web.blocked", Log.Where(b))));
            else if (!ok) done.TrySetException(new HttpRequestException(L.T("web.offline")));
            else if (failAt?.Invoke(u) == true) done.TrySetException(new SessionExpiredException(L.T("web.sessionEnded")));
            else if (arrived(u)) done.TrySetResult();
        };
        var seconds = Config.Int("sources.mybk.timeoutSeconds", 45);
        try
        {
            if (begin is null) Navigate(url!);
            else await begin();
            var timeout = Task.Delay(TimeSpan.FromSeconds(seconds), ct);
            if (await Task.WhenAny(done.Task, timeout) == done.Task) await done.Task;
            else
            {
                ct.ThrowIfCancellationRequested();
                throw new TimeoutException(L.F("web.noResponse", seconds, Log.Where(last)));
            }
        }
        finally { _loaded = null; }
    }

    private static string? Decode(string? raw) =>
        raw is null or "null" or "undefined" ? null : raw.StartsWith('"') ? JsonSerializer.Deserialize(raw, DesktopJson.Default.String) : raw;
}
