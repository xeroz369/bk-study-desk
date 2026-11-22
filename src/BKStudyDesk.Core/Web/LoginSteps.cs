using SoHocTap.Core;

namespace SoHocTap.Web;

public enum LoginStage { Sso, LmsCas, Done }

public enum LoginAction { Wait, NeedPassword, GoAppLogin, MybkReady, GoLaunch }

public sealed record LoginHosts(string Sso, string Lms, string Mybk)
{
    public static LoginHosts FromConfig() =>
        new(Host(Config.Str("sources.mybk.casLogin")), Host(Config.Str("sources.lms.site")), Host(Config.Str("sources.mybk.site")));

    private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : "";
}

public static class LoginSteps
{
    public static LoginAction Next(LoginStage stage, Uri page, LoginHosts hosts)
    {
        if (stage == LoginStage.Done) return LoginAction.Wait;
        var path = page.AbsolutePath;
        if (page.Host == hosts.Sso && path.Contains("/login", StringComparison.Ordinal)) return LoginAction.NeedPassword;
        if (stage == LoginStage.Sso && page.Host == hosts.Mybk)
        {
            if (path.StartsWith("/my/", StringComparison.Ordinal) || path.TrimEnd('/') == "/app/login") return LoginAction.GoAppLogin;
            if (path.StartsWith("/app", StringComparison.Ordinal) && !path.Contains("/login", StringComparison.Ordinal) && !path.Contains("/401", StringComparison.Ordinal))
                return LoginAction.MybkReady;
        }
        if (stage == LoginStage.LmsCas && page.Host == hosts.Lms && !path.Contains("/login/", StringComparison.Ordinal)) return LoginAction.GoLaunch;
        return LoginAction.Wait;
    }
}
