using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SoHocTap.Core;

namespace SoHocTap.Presentation;

public static partial class BkalendarExport
{
    public const string Header = "HỌC KỲ\tMÃ MH\tTÊN MÔN HỌC\tTÍN CHỈ\tTC HỌC PHÍ\tNHÓM - TỔ\tTHỨ\tTIẾT\tGIỜ HỌC\tPHÒNG\tCƠ SỞ\tTUẦN HỌC";

    public static string? Text(MybkData? m)
    {
        var classes = (m?.Schedule ?? []).Where(c => c.Day is >= 2 and <= 8 && c.Weeks.Count > 0 && Hours(c) is not null).ToList();
        if (m is null || classes.Count == 0) return null;
        var year = classes.Select(c => c.Year).FirstOrDefault(y => y is not null)
                   ?? (m.Term.Code.Length >= 4 && int.TryParse(m.Term.Code[..4], NumberStyles.None, CultureInfo.InvariantCulture, out var y4) ? y4 : DateTime.Today.Year);
        var all = classes.SelectMany(c => c.Weeks).Distinct().ToList();
        var straddles = all.Any(w => w >= 27) && all.Any(w => w < 27) && all.Max() - all.Min() > 26;
        var first = straddles ? all.Where(w => w >= 27).Min() : all.Min();
        var weeksInYear = ISOWeek.GetWeeksInYear(year);
        int Index(int w) => straddles && w < 27 ? w + weeksInYear - first : w - first;
        int WeekAt(int i) => first + i > weeksInYear ? first + i - weeksInYear : first + i;

        var sb = new StringBuilder();
        sb.Append(m.Term.Code).Append(" - ").Append(TermLine(m.Term, year)).Append('\n');
        sb.Append(Header).Append('\n');
        foreach (var c in classes.OrderBy(c => c.Day).ThenBy(c => c.Lesson))
        {
            var set = c.Weeks.Select(Index).Where(i => i >= 0).ToHashSet();
            if (set.Count == 0) continue;
            var weeks = string.Concat(Enumerable.Range(0, set.Max() + 1)
                .Select(i => (set.Contains(i) ? WeekAt(i).ToString("00", CultureInfo.InvariantCulture) : "--") + "|"));
            var lessons = c.Lesson > 0 && c.Lessons > 0 ? $"{c.Lesson} - {c.Lesson + c.Lessons - 1}" : "--";
            sb.Append(string.Join('\t',
                m.Term.Code, c.Code, Clean(c.Name), "--", "--", Clean(c.Group ?? "--"), c.Day == 8 ? "CN" : c.Day.ToString(CultureInfo.InvariantCulture),
                lessons, Hours(c)!, Clean(c.Room.Length > 0 ? c.Room : "--"), "--", weeks)).Append('\n');
        }
        return sb.ToString();
    }

    private static string? Hours(MybkClass c) =>
        VnTime.ParseClock(c.Start) is { } s && VnTime.ParseClock(c.End) is { } e && e > s ? $"{s / 60}:{s % 60:00} - {e / 60}:{e % 60:00}" : null;

    [GeneratedRegex(@"Học kỳ [123] Năm học \d+ - \d+")] private static partial Regex TermPattern();

    private static string TermLine(MybkTerm t, int year)
    {
        if (TermPattern().IsMatch(t.Name)) return t.Name;
        var start = t.Code.Length >= 4 && int.TryParse(t.Code[..4], NumberStyles.None, CultureInfo.InvariantCulture, out var y) ? y : year;
        var term = t.Code.Length >= 5 && t.Code[4] is '1' or '2' or '3' ? t.Code[4] : '1';
        return $"Học kỳ {term} Năm học {start} - {start + 1}";
    }

    private static string Clean(string s) => s.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();
}
