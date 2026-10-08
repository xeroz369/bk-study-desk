using SoHocTap.Core;

namespace SoHocTap.Web;

public enum LoginStage { Sso, LmsCas, Done }

/// <summary>Việc cần làm khi trình duyệt vừa mở xong một trang trong lượt đăng nhập.</summary>
public enum LoginAction { Wait, NeedPassword, GoAppLogin, MybkReady, GoLaunch }

/// <summary>Host của ba hệ thống trong một lượt đăng nhập (đọc từ config, không viết cứng).</summary>
public sealed record LoginHosts(string Sso, string Lms, string Mybk)
{
    public static LoginHosts FromConfig() =>
        new(Host(Config.Str("sources.mybk.casLogin")), Host(Config.Str("sources.lms.site")), Host(Config.Str("sources.mybk.site")));

    private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : "";
}

/// <summary>
/// Đăng nhập HCMUT một lần cho cả MyBK và LMS (cùng quy tắc với bản 1.x, Shell/LoginFlow):
///  1. SSO cho MyBK: /my/... hay /app/login thì sang trang appLogin; tới /app (không phải /login, /401) là MyBK đã sẵn sàng.
///  2. LMS: sau CAS của LMS (tự qua nhờ phiên SSO), ra khỏi /login/ là lúc gọi launch.php lấy token.
/// Gặp trang đăng nhập của SSO thì cần người dùng tự gõ mật khẩu (app không đọc mật khẩu).
/// </summary>
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
