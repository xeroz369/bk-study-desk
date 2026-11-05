namespace SoHocTap.Ui;

/// <summary>Phép tính vị trí trên lưới tuần, tách khỏi WPF để unit test được.</summary>
public static class WeekMath
{
    /// <summary>
    /// Vạch "bây giờ": cột ngày (0 = Thứ hai) và tọa độ y trong phần thân lưới, theo đúng phút hiện tại.
    /// null khi tuần đang xem không chứa <paramref name="now"/> hoặc giờ hiện tại nằm ngoài [from, to] (lưới tuần dùng 0:00 tới 24:00
    /// nên trong tuần có hôm nay luôn có vạch).
    /// </summary>
    public static (int Column, double Y)? NowLine(DateTime monday, DateTime now, int fromMin, int toMin, double pxPerMin, double pad)
    {
        var column = (int)Math.Floor((now.Date - monday.Date).TotalDays);
        if (column is < 0 or > 6) return null;
        var minute = now.Hour * 60 + now.Minute;
        if (minute < fromMin || minute > toMin) return null;
        return (column, pad + (minute - fromMin) * pxPerMin);
    }

    /// <summary>
    /// Minute shown at the top when a week opens. The grid always spans 0:00 to 24:00; default anchor is the hour before the
    /// earliest class. In the current week, if now is outside the view, scroll so the now line sits 1 hour below the top.
    /// <paramref name="visibleMin"/>: minutes that fit the viewport. <paramref name="lastEndMin"/>: latest class end of the week;
    /// once past it, do not follow now (an empty evening looked like an empty calendar, 05/11/2026).
    /// </summary>
    public static int ScrollAnchor(DateTime monday, DateTime now, int? firstStartMin, int visibleMin, int? lastEndMin = null)
    {
        var anchor = firstStartMin is { } f ? f / 60 * 60 : 7 * 60;
        var column = (int)Math.Floor((now.Date - monday.Date).TotalDays);
        if (column is >= 0 and <= 6)
        {
            var minute = now.Hour * 60 + now.Minute;
            var nothingLeft = lastEndMin is { } last && minute >= last;
            if (!nothingLeft && (minute < anchor || minute > anchor + visibleMin)) anchor = Math.Max(0, minute / 60 * 60 - 60);
        }
        return Math.Clamp(anchor, 0, Math.Max(0, 24 * 60 - visibleMin));
    }

    /// <summary>Thời gian chờ tới đầu phút kế tiếp (cộng chút dư để tick rơi vào phút mới chứ không phải 59.999 giây).</summary>
    public static TimeSpan UntilNextMinute(DateTime now) =>
        TimeSpan.FromMilliseconds(60_000 - (now.Second * 1000 + now.Millisecond) + 50);
}
