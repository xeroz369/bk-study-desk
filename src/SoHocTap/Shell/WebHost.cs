using Microsoft.Web.WebView2.Core;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Shell;

/// <summary>
/// Một WebView2 environment dùng chung cho mọi window của app (cùng profile data/webview nên dùng chung cookie SSO).
/// </summary>
internal static class WebHost
{
    private static Task<CoreWebView2Environment>? _env;

    public static Task<CoreWebView2Environment> EnvironmentAsync() =>
        _env ??= CoreWebView2Environment.CreateAsync(null, Paths.WebViewProfile);

    public static bool IsSchoolHost(string url)
    {
        // Chỉ https: cửa sổ trường dùng chung cookie đăng nhập, không để cookie đi qua http trần.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return false;
        return Config.List("sso.hosts").Any(h => u.Host.Equals(h, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// https tới host trường hoặc subdomain cùng tên miền (sso.hosts "sso.hcmut.edu.vn" → mọi *.hcmut.edu.vn). Dùng cho WebView ẩn
    /// của MyBK: trang có thể chuyển qua subdomain khác của trường lúc đăng nhập, nhưng không được ra ngoài trường.
    /// </summary>
    public static bool IsSchoolDomain(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return false;
        return Config.List("sso.hosts").Any(h =>
        {
            var dot = h.IndexOf('.');
            var domain = dot > 0 ? h[(dot + 1)..] : h;
            return u.Host.Equals(h, StringComparison.OrdinalIgnoreCase) || u.Host.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || u.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Trang đang ở là trang đăng nhập riêng của LMS/MyBK (phiên server đã hết)? Trả về cổng vào qua SSO tương ứng
    /// (sources.lms.casLogin, sources.mybk.appLogin), không phải thì null.
    /// </summary>
    public static string? SsoEntry(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return null;
        bool On(string key) => Uri.TryCreate(Config.Str(key), UriKind.Absolute, out var s) && s.Host.Equals(u.Host, StringComparison.OrdinalIgnoreCase);
        var path = u.AbsolutePath.ToLowerInvariant();
        if (On("sources.lms.site") && path.StartsWith("/login/index.php", StringComparison.Ordinal) && !u.Query.Contains("authCAS", StringComparison.OrdinalIgnoreCase))
            return Config.Str("sources.lms.casLogin");
        if (On("sources.mybk.site") && (path == "/app/401" || (path.StartsWith("/app/login", StringComparison.Ordinal) && !u.Query.Contains("type=cas", StringComparison.OrdinalIgnoreCase))))
            return Config.Str("sources.mybk.appLogin");
        return null;
    }

    /// <summary>Link trường viết http:// (danh sách dịch vụ cũ) thì đổi sang https:// trước khi mở bằng phiên đăng nhập.</summary>
    public static string Secure(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttp && IsSchoolHost("https://" + u.Authority)
            ? new UriBuilder(u) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri.AbsoluteUri : url;

    public static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : "";

    /// <summary>Đang ở trang nhập mật khẩu SSO (phiên SSO đã hết hoặc chưa đăng nhập).</summary>
    public static bool IsSsoLogin(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Host.Equals(Host(Config.Str("sources.mybk.casLogin")), StringComparison.OrdinalIgnoreCase)
        && u.AbsolutePath.Contains("/login", StringComparison.OrdinalIgnoreCase);

    /// <summary>Navigation hỏng vì mạng (mất mạng, không phân giải được tên, server không trả lời): câu báo lỗi cho người dùng, không phải thì null.</summary>
    public static string? NetworkError(CoreWebView2NavigationCompletedEventArgs e) => e.IsSuccess ? null : e.WebErrorStatus switch
    {
        CoreWebView2WebErrorStatus.Disconnected or CoreWebView2WebErrorStatus.HostNameNotResolved or CoreWebView2WebErrorStatus.CannotConnect
            or CoreWebView2WebErrorStatus.ConnectionAborted or CoreWebView2WebErrorStatus.ConnectionReset or CoreWebView2WebErrorStatus.ServerUnreachable
            => L.T("web.offline"),
        CoreWebView2WebErrorStatus.Timeout => L.T("web.timeout"),
        _ => null,
    };
}

/// <summary>
/// Giữ session login: web trường chỉ dùng session cookie (form SSO không có "ghi nhớ") nên WebView2 xóa hết khi tắt app.
/// Ở đây đổi session cookie của sso.hosts thành cookie có hạn sso.rememberDays ngày, ngay trong cookie store của WebView2
/// (Chromium tự encrypt bằng DPAPI). App không ghi cookie ra file riêng. Phải gọi trên UI thread.
/// </summary>
internal static class SessionKeeper
{
    public static async Task PersistAsync(CoreWebView2 core)
    {
        try
        {
            var hosts = Config.List("sso.hosts").Select(h => h.ToLowerInvariant()).ToList();
            var days = Config.Int("sso.rememberDays", 30);
            if (days <= 0) return;   // người dùng tắt "Ghi nhớ đăng nhập" thì session cookie mất khi tắt app
            // Phiên trên server không sống quá giới hạn của nó (CAS 3.5 mặc định: tối đa 8 giờ kể từ lúc đăng nhập, 2 giờ không dùng;
            // Moodle sessiontimeout mặc định 8 giờ), nên cookie cũng chỉ giữ tối đa sso.rememberHours (8) giờ. OWASP khuyên cookie phiên
            // không nên sống lâu hơn phiên trên server.
            // Chuỗi rỗng = lấy mọi cookie của profile; lọc theo domain vì cookie SSO gắn path /cas/.
            var cookies = await core.CookieManager.GetCookiesAsync("");
            int kept = 0, total = 0;
            foreach (var c in cookies)
            {
                var domain = c.Domain.TrimStart('.');
                if (!hosts.Any(h => domain.Equals(h, StringComparison.OrdinalIgnoreCase) || domain.EndsWith("." + h, StringComparison.OrdinalIgnoreCase))) continue;
                total++;
                if (!c.IsSession) continue;
                c.Expires = DateTime.Now.AddHours(Math.Min(days * 24, Config.Int("sso.rememberHours", 8)));
                core.CookieManager.AddOrUpdateCookie(c);
                kept++;
                Log.Debug($"Giữ cookie {c.Name} ({domain}{c.Path}) tới {c.Expires:dd/MM HH:mm}");   // chỉ tên cookie, không ghi giá trị
            }
            if (kept > 0) Log.Info($"Giữ session: {kept} session cookie / {total} cookie của trường");
        }
        catch (Exception e) { Log.Error("Giữ session", e); }
    }
}
