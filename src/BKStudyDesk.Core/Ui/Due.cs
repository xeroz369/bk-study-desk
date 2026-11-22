using SoHocTap.Core;

namespace SoHocTap.Ui;

public enum Urgency { None, Normal, Soon, Urgent, Overdue }

public static class Due
{
    public static Urgency Of(long due, long now, bool done, int urgentHours, int soonHours)
    {
        if (done) return Urgency.None;
        var left = due - now;
        if (left < 0) return Urgency.Overdue;
        if (urgentHours > 0 && left < urgentHours * 3600L) return Urgency.Urgent;
        if (soonHours > 0 && left < soonHours * 3600L) return Urgency.Soon;
        return Urgency.Normal;
    }

    public static bool SoonHidden(int urgentHours, int soonHours) => urgentHours > 0 && soonHours > 0 && urgentHours >= soonHours;

    public static string LeftText(long due, long now)
    {
        var s = due - now;
        if (s < 0) return L.T("format.until.passed");
        if (s < 3600) return L.F("format.until.minutes", Math.Max(1, s / 60));
        var d = VnTime.DayDiff(due, now);
        if (d == 0) return L.F("format.until.hours", Math.Round(s / 3600.0));
        return d == 1 ? L.T("format.until.tomorrow") : L.F("format.until.days", d);
    }

    public static string SpanText(long sec) =>
        sec >= 86400 ? L.F("notify.daysHours", sec / 86400, sec % 86400 / 3600)
        : sec >= 3600 ? L.F("notify.hours", sec / 3600)
        : L.F("notify.minutes", Math.Max(1, sec / 60));
}
