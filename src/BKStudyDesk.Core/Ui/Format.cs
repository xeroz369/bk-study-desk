using System.Globalization;

namespace SoHocTap.Ui;

public static class Format
{
    private static readonly string[] DayShort = ["format.dayShort.sun", "format.dayShort.mon", "format.dayShort.tue", "format.dayShort.wed", "format.dayShort.thu", "format.dayShort.fri", "format.dayShort.sat"];
    private static readonly string[] DayLong = ["format.day.sun", "format.day.mon", "format.day.tue", "format.day.wed", "format.day.thu", "format.day.fri", "format.day.sat"];
    public static IReadOnlyDictionary<int, string> MybkDays => Enumerable.Range(2, 7).ToDictionary(d => d, d => L.T(DayLong[(d - 1) % 7]));

    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static DateTime Local(long sec) => Core.VnTime.ToWall(sec);
    public static long Sec(DateTime wall) => Core.VnTime.FromWall(wall);
    public static DateTime Today => Core.VnTime.Today;

    public static string DateTime(long sec) { var d = Local(sec); return L.F("format.dateTime", L.T(DayShort[(int)d.DayOfWeek]), d); }
    public static string Hm(long sec) => Local(sec).ToString("HH:mm", CultureInfo.InvariantCulture);
    public static string DateLong(DateTime d) => L.F("format.dateLong", L.T(DayLong[(int)d.DayOfWeek]), d);

    public static int DayDiff(long sec) => Core.VnTime.DayDiff(sec, Now);

    public static string Until(long sec) => Due.LeftText(sec, Now);

    public static string Ago(long sec)
    {
        var s = Now - sec;
        if (s < 3600) return L.F("format.ago.minutes", Math.Max(1, s / 60));
        var d = -DayDiff(sec);
        if (d == 0) return L.F("format.ago.hours", s / 3600);
        if (d == 1) return L.T("format.ago.yesterday");
        return d < 7 ? L.F("format.ago.days", d) : L.F("format.date", Local(sec));
    }

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

    public static int IsoWeek(DateTime d) => ISOWeek.GetWeekOfYear(d);
    public static int MybkDay(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 8 : (int)d.DayOfWeek + 1;
}
