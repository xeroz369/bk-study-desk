using System.Globalization;

namespace SoHocTap.Core;

/// <summary>
/// Giờ Việt Nam (UTC+7 quanh năm, không có giờ mùa hè). Giờ trên MyBK, LMS là giờ VN dạng chữ ("07:00", "7g30", "09g00",
/// "15/10/2026 09:00"); đổi qua Unix seconds bằng offset cố định, không dùng múi giờ của máy: máy đặt múi giờ khác
/// (sinh viên đi nước ngoài, máy mới cài để UTC) thì lịch học vẫn đúng giờ trường.
/// </summary>
public static class VnTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    /// <summary>Unix seconds → giờ đồng hồ ở VN (DateTimeKind.Unspecified).</summary>
    public static DateTime ToWall(long unixSeconds) =>
        DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime + Offset, DateTimeKind.Unspecified);

    /// <summary>Giờ đồng hồ ở VN → Unix seconds. Kind của <paramref name="wall"/> bị bỏ qua: luôn coi là giờ VN.</summary>
    public static long FromWall(DateTime wall) =>
        new DateTimeOffset(DateTime.SpecifyKind(wall, DateTimeKind.Unspecified), Offset).ToUnixTimeSeconds();

    /// <summary>Ngày hôm nay ở VN.</summary>
    public static DateTime Today => ToWall(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).Date;

    /// <summary>
    /// Số ngày lịch ở VN từ ngày của <paramref name="now"/> tới ngày của <paramref name="sec"/>: hôm nay = 0, ngày mai = 1.
    /// Tính theo giờ VN chứ không theo DateTime.Today của máy: máy để UTC thì 0:30 sáng ở VN vẫn là "hôm nay" mới.
    /// </summary>
    public static int DayDiff(long sec, long now) => (int)(ToWall(sec).Date - ToWall(now).Date).TotalDays;

    /// <summary>Thứ hai của tuần chứa <paramref name="day"/> (tuần ở trường bắt đầu từ Thứ hai).</summary>
    public static DateTime Monday(DateTime day) => day.Date.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    // MyBK ghi ngày thi kiểu "2026-10-15"; mấy kiểu kia để dự phòng khi trường đổi định dạng.
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy", "d/M/yyyy"];

    /// <summary>Ngày dạng chữ của MyBK ("2026-10-15", "15/10/2026") ra ngày; chuỗi lạ thì null.</summary>
    public static DateTime? ParseDate(string? text) =>
        DateTime.TryParseExact((text ?? "").Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : null;

    /// <summary>
    /// Giờ trong ngày → số phút từ 0 giờ: "07:00", "7:5", "7g30", "09g00", "9g" (MyBK ghi giờ thi kiểu "09g00").
    /// Chuỗi lạ, giờ ≥ 24 hay phút ≥ 60 thì null, không throw: dữ liệu trường đổi định dạng không được làm hỏng cả trang.
    /// </summary>
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
        // "7g" có, "7:" thì không: dấu ':' luôn phải có phút đi kèm.
        if (minutePart.Length == 0 && s[sep] == ':') return null;
        return h is >= 0 and < 24 && m is >= 0 and < 60 ? h * 60 + m : null;
    }

    /// <summary>"15/10/2026 09:00" (giờ VN, bảng đợt đăng ký môn) → Unix seconds; sai định dạng thì null.</summary>
    public static long? ParseDateTime(string? text, string format = "dd/MM/yyyy HH:mm") =>
        DateTime.TryParseExact((text ?? "").Trim(), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? FromWall(d) : null;

    /// <summary>"2026-10-15" + "09g00" (lịch thi MyBK) → Unix seconds; ngày sai thì null, giờ sai thì lấy 0 giờ.</summary>
    public static long? ParseDateAndClock(string? date, string? clock) =>
        ParseDate(date) is { } d ? FromWall(d.AddMinutes(ParseClock(clock) ?? 0)) : null;
}
