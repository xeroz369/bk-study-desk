using System.Globalization;

namespace SoHocTap.Core;

public static class VnTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateTime ToWall(long unixSeconds) =>
        DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime + Offset, DateTimeKind.Unspecified);

    public static long FromWall(DateTime wall) =>
        new DateTimeOffset(DateTime.SpecifyKind(wall, DateTimeKind.Unspecified), Offset).ToUnixTimeSeconds();

    public static DateTime Today => ToWall(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).Date;

    public static int DayDiff(long sec, long now) => (int)(ToWall(sec).Date - ToWall(now).Date).TotalDays;

    public static DateTime Monday(DateTime day) => day.Date.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy", "d/M/yyyy"];

    public static DateTime? ParseDate(string? text) =>
        DateTime.TryParseExact((text ?? "").Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : null;

    public static int? ParseClock(string? text)
    {
        var s = (text ?? "").Trim().ToLowerInvariant();
        var sep = s.IndexOfAny([':', 'g', 'h']);
        if (sep <= 0) return null;
        var hourPart = s[..sep];
        var minutePart = s[(sep + 1)..];
        if (!int.TryParse(hourPart, NumberStyles.None, CultureInfo.InvariantCulture, out var h)) return null;
        var m = 0;
        if (minutePart.Length > 0 && !int.TryParse(minutePart, NumberStyles.None, CultureInfo.InvariantCulture, out m)) return null;
        if (minutePart.Length == 0 && s[sep] == ':') return null;
        return h is >= 0 and < 24 && m is >= 0 and < 60 ? h * 60 + m : null;
    }

    public static long? ParseDateTime(string? text, string format = "dd/MM/yyyy HH:mm") =>
        DateTime.TryParseExact((text ?? "").Trim(), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? FromWall(d) : null;

    public static long? ParseDateAndClock(string? date, string? clock) =>
        ParseDate(date) is { } d ? FromWall(d.AddMinutes(ParseClock(clock) ?? 0)) : null;
}
