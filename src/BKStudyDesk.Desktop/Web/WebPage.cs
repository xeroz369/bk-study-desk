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

/// <summary>
/// Một trang web nhúng (NativeWebView: WebView2 trên Windows, WKWebView trên macOS, WebKitGTK trên Linux) với những việc app cần:
/// mở trang và chờ tới đúng trang, chạy script (kể cả chờ promise), đọc và ghi cookie. Mọi trang dùng chung profile data/webview
/// nên dùng chung cookie đăng nhập trường. Chỉ gọi trên UI thread.
/// </summary>
internal sealed class WebPage
{
    /// <summary>macOS: WKWebView giữ dữ liệu theo mã này (cố định để cookie còn sau khi mở lại app).</summary>
    private static readonly Guid AppleStore = new("6b0d7f3e-2c1a-4f5e-9a51-0b0000000001");

    private bool _cancelled;          // vừa tự hủy một điều hướng (ngoài trường, đổi sang https): bỏ qua lần báo lỗi của nó
    private string? _blocked;         // link ngoài trường vừa bị chặn, để báo lỗi rõ thay vì chờ hết giờ
    private Action<Uri, bool>? _loaded;

    public NativeWebView View { get; } = new();

    /// <summary>Chỉ cho đi tới các trang thỏa điều kiện; null là cho mọi trang (cửa sổ đăng nhập người dùng tự xem).</summary>
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
        View.NewWindowRequested += (_, e) => e.Handled = true;   // không mở cửa sổ mới
        View.NavigationStarted += (_, e) =>
        {
            if (e.Request is not { } u || Allow is null || u.Scheme == "about" || Allow(u)) return;
            e.Cancel = true;
            _cancelled = true;
            // MyBK hết phiên có lúc chuyển về http://mybk.../app/login: cùng tên miền trường thì mở lại bằng https.
            if (SchoolUrls.HttpsOfSchoolDomain(u.AbsoluteUri) is { } https)
            {
                Log.Info($"Web: đổi http sang https {Log.Where(u.AbsoluteUri)}");
                Dispatcher.UIThread.Post(() => View.Navigate(new Uri(https)));
                return;
            }
            _blocked = u.AbsoluteUri;
            Log.Warn($"Web: chặn trang ngoài trường {Log.Where(u.AbsoluteUri)} ({u.Scheme})");
        };
        View.NavigationCompleted += (_, e) =>
        {
            if (!e.IsSuccess && _cancelled) { _cancelled = false; return; }
            if (View.Source is { } u) _loaded?.Invoke(u, e.IsSuccess);
            Loaded?.Invoke(View.Source, e.IsSuccess);
        };
    }

    /// <summary>Trang vừa tải xong (thành công hay không).</summary>
    public event Action<Uri?, bool>? Loaded;

    public void Navigate(string url) => View.Navigate(new Uri(url));

    /// <summary>Chạy một biểu thức; kết quả là chuỗi (đã bỏ lớp JSON) hay null.</summary>
    public async Task<string?> EvalAsync(string js) => Decode(await View.InvokeScript(js));

    /// <summary>
    /// Chạy một biểu thức trả promise (fetch) và chờ kết quả: InvokeScript không chờ promise trên mọi nền tảng, nên script ghi kết quả
    /// vào window.__bk[id] và app hỏi lại mỗi 50 ms. Script lỗi hay quá giờ thì null (có ghi log).
    /// </summary>
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

    /// <summary>
    /// Mở url (hay chạy <paramref name="begin"/>) rồi chờ tới trang thỏa <paramref name="arrived"/>. Gặp trang <paramref name="failAt"/>
    /// (đăng nhập) thì báo SessionExpiredException; tải lỗi, bị chặn ra ngoài trường thì HttpRequestException; quá
    /// sources.mybk.timeoutSeconds giây thì TimeoutException. Không để người dùng chờ mà không biết vì sao.
    /// </summary>
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

    /// <summary>InvokeScript trả kết quả dạng JSON: chuỗi có ngoặc kép, null là "null".</summary>
    private static string? Decode(string? raw) =>
        raw is null or "null" or "undefined" ? null : raw.StartsWith('"') ? JsonSerializer.Deserialize<string>(raw) : raw;
}
