using Microsoft.Web.WebView2.Core;
using SoHocTap.Core;

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
            // Chuỗi rỗng = lấy mọi cookie của profile; lọc theo domain vì cookie SSO gắn path /cas/.
            var cookies = await core.CookieManager.GetCookiesAsync("");
            int kept = 0, total = 0;
            foreach (var c in cookies)
            {
                var domain = c.Domain.TrimStart('.');
                if (!hosts.Any(h => domain.Equals(h, StringComparison.OrdinalIgnoreCase) || domain.EndsWith("." + h, StringComparison.OrdinalIgnoreCase))) continue;
                total++;
                if (!c.IsSession) continue;
                c.Expires = DateTime.Now.AddDays(days);
                core.CookieManager.AddOrUpdateCookie(c);
                kept++;
            }
            if (kept > 0) Log.Info($"Giữ session: {kept} session cookie / {total} cookie của trường");
        }
        catch (Exception e) { Log.Error("Giữ session", e); }
    }
}
