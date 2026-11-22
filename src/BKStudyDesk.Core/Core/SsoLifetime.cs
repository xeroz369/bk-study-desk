using System.Globalization;

namespace SoHocTap.Core;

public static class SsoLifetime
{
    public static DateTime? CookieExpiry(bool isSession, DateTime expires, DateTime now, int rememberDays)
    {
        if (rememberDays <= 0) return null;
        var until = now.AddDays(rememberDays);
        if (!isSession && expires >= until) return null;
        return until;
    }

    public static string ExpiredMessage(DateTime? loginAt, DateTime? aliveAt, DateTime now)
    {
        var login = loginAt is { } l ? $"đăng nhập lúc {l.ToString("HH:mm dd/MM", CultureInfo.InvariantCulture)}, sống {Span(now - l)}" : "chưa rõ lúc đăng nhập";
        var alive = aliveAt is { } a ? $"lần cuối còn phiên {Math.Max(0, (int)(now - a).TotalMinutes)} phút trước" : "chưa rõ lần cuối còn phiên";
        return $"Phiên SSO hết hạn trên server: {login}, {alive}";
    }

    private static string Span(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return $"{(int)t.TotalHours} giờ {t.Minutes} phút";
    }
}
