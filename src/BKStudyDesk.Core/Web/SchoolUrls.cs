using SoHocTap.Core;

namespace SoHocTap.Web;

/// <summary>
/// Quy tắc link của trường (host nào là của trường, đổi http sang https, trang đăng nhập SSO). Chép phần thuần của Shell/WebHost
/// bản 1.x (file đó gắn với WebView2); bản 1.x chỉ còn sửa lỗi nên hai bản không cần giữ chung.
/// </summary>
public static class SchoolUrls
{
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

}
