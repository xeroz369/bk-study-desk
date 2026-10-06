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

    /// <summary>url là http:// tới tên miền trường (xem <see cref="IsSchoolDomain"/>) thì trả bản https://, không thì null.</summary>
    public static string? HttpsOfSchoolDomain(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttp) return null;
        var https = new UriBuilder(u) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri.AbsoluteUri;
        return IsSchoolDomain(https) ? https : null;
    }

    public static string Scheme(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Scheme : "?";

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
/// Sau mỗi lượt đi qua SSO thành công (đăng nhập, giữ phiên, đồng bộ MyBK qua SSO, trang trường tải xong), mọi cookie của
/// sso.hosts mà server còn giữ được đẩy hạn tới sso.rememberDays ngày, ngay trong cookie store của WebView2 (Chromium tự encrypt
/// bằng DPAPI). App không ghi cookie ra file riêng. Phải gọi trên UI thread.
/// </summary>
internal static class SessionKeeper
{
    public static async Task PersistAsync(CoreWebView2 core)
    {
        try
        {
            var hosts = Config.List("sso.hosts").Select(h => h.ToLowerInvariant()).ToList();
            var days = Settings.Sso.RememberDays;
            if (days <= 0) return;   // người dùng tắt "Ghi nhớ đăng nhập" thì session cookie mất khi tắt app
            // Hạn cookie chỉ để app không tự xóa vé SSO sớm hơn server. Vé (TGT) trên server vẫn tự hết theo lịch của server,
            // lúc đó trang trường đòi đăng nhập lại dù cookie còn hạn. Bản 1.1.9 trở về trước giữ cookie tối đa 8 giờ (chép số mặc định
            // của CAS, chưa đo trên server trường) và không gia hạn cookie đã có hạn, nên chính app xóa cookie SSO đúng 8 giờ sau
            // khi đăng nhập. OWASP muốn hạn phiên do server kiểm; cookie sống lâu hơn không làm phiên sống lâu hơn, chỉ để server
            // tự quyết khi nào hết.
            // Chuỗi rỗng = lấy mọi cookie của profile; lọc theo domain vì cookie SSO gắn path /cas/.
            var cookies = await core.CookieManager.GetCookiesAsync("");
            var now = DateTime.Now;
            int kept = 0, converted = 0, total = 0;
            foreach (var c in cookies)
            {
                var domain = c.Domain.TrimStart('.');
                if (!hosts.Any(h => domain.Equals(h, StringComparison.OrdinalIgnoreCase) || domain.EndsWith("." + h, StringComparison.OrdinalIgnoreCase))) continue;
                total++;
                if (SsoLifetime.CookieExpiry(c.IsSession, c.Expires, now, days) is not { } until) continue;
                if (c.IsSession) converted++;
                c.Expires = until;
                core.CookieManager.AddOrUpdateCookie(c);
                kept++;
                Log.Debug($"Giữ cookie {c.Name} ({domain}{c.Path}) tới {until:dd/MM HH:mm}");   // chỉ tên cookie, không ghi giá trị
            }
            // Lượt nào qua SSO cũng gia hạn (mỗi giờ khi giữ phiên): chỉ ghi Info khi có session cookie mới, còn lại để Debug.
            var line = $"Giữ session: gia hạn {kept} cookie ({converted} session cookie mới) / {total} cookie của trường, {days} ngày";
            if (converted > 0) Log.Info(line);
            else if (kept > 0) Log.Debug(line);
        }
        catch (Exception e) { Log.Error("Giữ session", e); }
    }
}
