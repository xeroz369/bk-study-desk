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
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        return Config.List("sso.hosts").Any(h => u.Host.Equals(h, StringComparison.OrdinalIgnoreCase));
    }

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
                if (!hosts.Any(h => c.Domain.TrimStart('.').EndsWith(h, StringComparison.OrdinalIgnoreCase))) continue;
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
