using SoHocTap.Core;

namespace SoHocTap.Ui;

/// <summary>Mức gấp của một hạn nộp, từ nhẹ tới nặng; <see cref="Overdue"/> là quá hạn mà chưa xong.</summary>
public enum Urgency { None, Normal, Soon, Urgent, Overdue }

/// <summary>
/// Mức gấp và chữ đếm ngược của hạn nộp, tính một chỗ cho mọi trang và thông báo. Thời gian là Unix seconds,
/// "còn lại" = due - now (thời gian tuyệt đối nên múi giờ máy không ảnh hưởng). Thuần, không WPF.
/// </summary>
public static class Due
{
    /// <summary>
    /// Đã xong thì None. Quá hạn thì Overdue. Còn lại dưới <paramref name="urgentHours"/> giờ thì Urgent, dưới <paramref name="soonHours"/> giờ thì Soon,
    /// còn lại là Normal. Số giờ bằng 0 nghĩa là tắt mức đó (không bao giờ trả về).
    /// </summary>
    public static Urgency Of(long due, long now, bool done, int urgentHours, int soonHours)
    {
        if (done) return Urgency.None;
        var left = due - now;
        if (left < 0) return Urgency.Overdue;
        if (urgentHours > 0 && left < urgentHours * 3600L) return Urgency.Urgent;
        if (soonHours > 0 && left < soonHours * 3600L) return Urgency.Soon;
        return Urgency.Normal;
    }

    /// <summary>Mốc Rất gấp không nhỏ hơn mốc Gấp (cả hai đang bật) thì <see cref="Of"/> không bao giờ trả về Soon: màu vàng không hiện.</summary>
    public static bool SoonHidden(int urgentHours, int soonHours) => urgentHours > 0 && soonHours > 0 && urgentHours >= soonHours;

    /// <summary>"còn 3 giờ", "ngày mai", "còn 5 ngày", "đã qua" (ngày tính theo giờ VN)</summary>
    public static string LeftText(long due, long now)
    {
        var s = due - now;
        if (s < 0) return L.T("format.until.passed");
        if (s < 3600) return L.F("format.until.minutes", Math.Max(1, s / 60));
        var d = VnTime.DayDiff(due, now);
        if (d == 0) return L.F("format.until.hours", Math.Round(s / 3600.0));
        return d == 1 ? L.T("format.until.tomorrow") : L.F("format.until.days", d);
    }

    /// <summary>Khoảng thời gian ngắn cho thông báo (đi sau "còn"): "2 ngày 3 giờ", "4 giờ", "20 phút".</summary>
    public static string SpanText(long sec) =>
        sec >= 86400 ? L.F("notify.daysHours", sec / 86400, sec % 86400 / 3600)
        : sec >= 3600 ? L.F("notify.hours", sec / 3600)
        : L.F("notify.minutes", Math.Max(1, sec / 60));
}
