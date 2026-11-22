using SoHocTap.Core;

namespace SoHocTap.Web;

public static class SchoolUrls
{
    public static bool IsSchoolHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return false;
        return Config.List("sso.hosts").Any(h => u.Host.Equals(h, StringComparison.OrdinalIgnoreCase));
    }

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

    public static string Secure(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttp && IsSchoolHost("https://" + u.Authority)
            ? new UriBuilder(u) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri.AbsoluteUri : url;

    public static string? HttpsOfSchoolDomain(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttp) return null;
        var https = new UriBuilder(u) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri.AbsoluteUri;
        return IsSchoolDomain(https) ? https : null;
    }

    public static string Scheme(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Scheme : "?";

    public static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : "";

    public static string PagePath(Uri u) => string.Join('/', u.AbsolutePath.Split('/').Select(s => s.IndexOf(';') is var i and >= 0 ? s[..i] : s));

    public static bool IsSsoLogin(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Host.Equals(Host(Config.Str("sources.mybk.casLogin")), StringComparison.OrdinalIgnoreCase)
        && u.AbsolutePath.Contains("/login", StringComparison.OrdinalIgnoreCase);
}
