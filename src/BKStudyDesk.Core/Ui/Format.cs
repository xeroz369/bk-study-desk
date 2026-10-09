using System.Globalization;

namespace SoHocTap.Ui;

/// <summary>
/// Format ngày giờ, số, dung lượng. Thời gian là Unix seconds, hiện theo giờ VN (giờ trường, <see cref="Core.VnTime"/>) chứ không theo
/// múi giờ máy: "hôm nay", đầu tuần, nhóm ngày phải khớp với lịch học dựng bằng giờ VN.
/// </summary>
public static class Format
{
    // Key trong file ngôn ngữ, theo thứ tự DayOfWeek (chủ nhật = 0).
    private static readonly string[] DayShort = ["format.dayShort.sun", "format.dayShort.mon", "format.dayShort.tue", "format.dayShort.wed", "format.dayShort.thu", "format.dayShort.fri", "format.dayShort.sat"];
    private static readonly string[] DayLong = ["format.day.sun", "format.day.mon", "format.day.tue", "format.day.wed", "format.day.thu", "format.day.fri", "format.day.sat"];
    /// <summary>Tên thứ theo số thứ của MyBK (2 = thứ hai … 8 = chủ nhật).</summary>
    public static IReadOnlyDictionary<int, string> MybkDays => Enumerable.Range(2, 7).ToDictionary(d => d, d => L.T(DayLong[(d - 1) % 7]));

    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    /// <summary>Giờ đồng hồ ở VN của một mốc.</summary>
    public static DateTime Local(long sec) => Core.VnTime.ToWall(sec);
    /// <summary>Giờ đồng hồ ở VN ra Unix seconds.</summary>
    public static long Sec(DateTime wall) => Core.VnTime.FromWall(wall);
    /// <summary>Hôm nay ở VN (không dùng DateTime.Today của máy).</summary>
    public static DateTime Today => Core.VnTime.Today;

    /// <summary>T3 15/10 09:00</summary>
    public static string DateTime(long sec) { var d = Local(sec); return L.F("format.dateTime", L.T(DayShort[(int)d.DayOfWeek]), d); }
    public static string Hm(long sec) => Local(sec).ToString("HH:mm", CultureInfo.InvariantCulture);
    /// <summary>Thứ ba 15/10</summary>
    public static string DateLong(DateTime d) => L.F("format.dateLong", L.T(DayLong[(int)d.DayOfWeek]), d);

    /// <summary>Số ngày lịch từ hôm nay: hôm nay = 0, ngày mai = 1.</summary>
    public static int DayDiff(long sec) => Core.VnTime.DayDiff(sec, Now);

    /// <summary>Chữ đếm ngược tới <paramref name="sec"/>, xem <see cref="Due.LeftText"/>.</summary>
    public static string Until(long sec) => Due.LeftText(sec, Now);

    /// <summary>2 giờ trước · hôm qua · 3 ngày trước · 12/09</summary>
    public static string Ago(long sec)
    {
        var s = Now - sec;
        if (s < 3600) return L.F("format.ago.minutes", Math.Max(1, s / 60));
        var d = -DayDiff(sec);
        if (d == 0) return L.F("format.ago.hours", s / 3600);
        if (d == 1) return L.T("format.ago.yesterday");
        return d < 7 ? L.F("format.ago.days", d) : L.F("format.date", Local(sec));
    }

    /// <summary>Nhóm theo ngày kiểu File Explorer.</summary>
    public static string DayGroup(long sec)
    {
        var d = DayDiff(sec);
        return L.T(d < 0 ? "format.group.past" : d == 0 ? "format.group.today" : d == 1 ? "format.group.tomorrow"
            : d <= 7 ? "format.group.week" : d <= 30 ? "format.group.month" : "format.group.later");
    }

    public static string Num(double x, int digits = 2) => Math.Round(x, digits).ToString("0.##", L.Culture);
    public static string Score(double? x) => x is null ? "-" : Math.Abs(x.Value % 1) < 1e-9 ? x.Value.ToString(CultureInfo.InvariantCulture) : Num(x.Value);
    public static string Money(long n) => L.F("format.money", n);

    public static string Size(long b) => b < 1024 ? $"{b} B" : b < 1048576 ? $"{b / 1024} KB"
        : b < 1073741824 ? $"{(b / 1048576.0).ToString("0.0", L.Culture)} MB" : $"{(b / 1073741824.0).ToString("0.0", L.Culture)} GB";

    // Tên riêng (PDF, Word…) giữ nguyên; value bắt đầu bằng "files." là key trong file ngôn ngữ.
    private static readonly Dictionary<string, string> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "PDF",
        [".doc"] = "Word",
        [".docx"] = "Word",
        [".ppt"] = "files.slides",
        [".pptx"] = "files.slides",
        [".xls"] = "Excel",
        [".xlsx"] = "Excel",
        [".png"] = "files.image",
        [".jpg"] = "files.image",
        [".jpeg"] = "files.image",
        [".mp4"] = "Video",
        [".m"] = "MATLAB",
        [".py"] = "Python",
        [".c"] = "C",
        [".txt"] = "files.text",
        [".zip"] = "files.archive",
        [".rar"] = "files.archive",
        [".7z"] = "files.archive",
        [".htm"] = "files.web",
        [".html"] = "files.web",
    };
    public static string FileKind(string ext) =>
        Kinds.TryGetValue(ext, out var k) ? (k.StartsWith("files.", StringComparison.Ordinal) ? L.T(k) : k) : ext.Length > 1 ? ext[1..].ToUpperInvariant() : L.T("files.file");

    /// <summary>Tuần ISO (MyBK đánh số tuần theo năm dương lịch).</summary>
    public static int IsoWeek(DateTime d) => ISOWeek.GetWeekOfYear(d);
    /// <summary>Thứ theo MyBK: 2 = thứ hai … 8 = chủ nhật.</summary>
    public static int MybkDay(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 8 : (int)d.DayOfWeek + 1;
}
