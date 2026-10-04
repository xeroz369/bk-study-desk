using System.Globalization;

namespace SoHocTap.Core;

/// <summary>
/// Phần tính toán thuần của việc giữ phiên SSO (không WebView2, không config, test được).
/// Luật: app không bao giờ kết thúc phiên sớm hơn server. Cookie chỉ là chỗ chứa vé SSO trên máy; vé (TGT) trên server vẫn tự hết
/// theo lịch của server. Thời gian sống thật của phiên thì đo (<see cref="ExpiredMessage"/>), không đoán (DESIGN.md mục 8, 6b-3).
/// </summary>
public static class SsoLifetime
{
    /// <summary>
    /// Hạn mới cho một cookie của trường sau một lượt đi qua SSO thành công; null = giữ nguyên.
    /// rememberDays &lt;= 0 (tắt "Ghi nhớ đăng nhập") thì không đụng tới, session cookie mất khi tắt app như bình thường.
    /// Cookie session hay cookie đã có hạn đều được đẩy hạn tới now + rememberDays, trừ khi server đã cho hạn xa hơn thế
    /// (không rút ngắn hạn của server).
    /// </summary>
    public static DateTime? CookieExpiry(bool isSession, DateTime expires, DateTime now, int rememberDays)
    {
        if (rememberDays <= 0) return null;
        var until = now.AddDays(rememberDays);
        if (!isSession && expires >= until) return null;
        return until;
    }

    /// <summary>
    /// Dòng log khi thấy phiên SSO đã hết trên server, để biết giới hạn TGT thật của HCMUT. Giờ là giờ máy (như dòng log).
    /// Chưa biết lúc đăng nhập (bản cũ, đăng nhập trước khi có đo) thì ghi "chưa rõ".
    /// </summary>
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
