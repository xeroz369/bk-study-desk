namespace SoHocTap.Ui;

public static class WeekMath
{
    public static (int Column, double Y)? NowLine(DateTime monday, DateTime now, int fromMin, int toMin, double pxPerMin, double pad)
    {
        var column = (int)Math.Floor((now.Date - monday.Date).TotalDays);
        if (column is < 0 or > 6) return null;
        var minute = now.Hour * 60 + now.Minute;
        if (minute < fromMin || minute > toMin) return null;
        return (column, pad + (minute - fromMin) * pxPerMin);
    }

    public static int ScrollAnchor(DateTime monday, DateTime now, int? firstStartMin, int visibleMin, int? lastEndMin = null)
    {
        var anchor = firstStartMin is { } f ? f / 60 * 60 : 7 * 60;
        var column = (int)Math.Floor((now.Date - monday.Date).TotalDays);
        if (column is >= 0 and <= 6)
        {
            var minute = now.Hour * 60 + now.Minute;
            var nothingLeft = lastEndMin is { } last && minute >= last;
            if (!nothingLeft && minute > anchor + visibleMin) anchor = Math.Max(0, minute / 60 * 60 - 60);
        }
        return Math.Clamp(anchor, 0, Math.Max(0, 24 * 60 - visibleMin));
    }

    public static TimeSpan UntilNextMinute(DateTime now) =>
        TimeSpan.FromMilliseconds(60_000 - (now.Second * 1000 + now.Millisecond) + 50);
}
