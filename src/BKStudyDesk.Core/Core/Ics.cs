using System.Globalization;
using System.Text;

namespace SoHocTap.Core;

public sealed record IcsEvent(string Uid, DateTime Start, DateTime End, string Summary, string? Location = null, string? Description = null);

public static class Ics
{
    public static readonly TimeSpan VietnamOffset = VnTime.Offset;

    public static string Build(string calendarName, IEnumerable<IcsEvent> events, DateTime nowUtc)
    {
        var sb = new StringBuilder();
        void Line(string s) { foreach (var part in Fold(s)) sb.Append(part).Append("\r\n"); }
        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//xeroz369//BK Study Desk//VI");
        Line("CALSCALE:GREGORIAN");
        Line("METHOD:PUBLISH");
        Line("X-WR-CALNAME:" + Escape(calendarName));
        var stamp = Utc(nowUtc);
        foreach (var e in events)
        {
            Line("BEGIN:VEVENT");
            Line("UID:" + e.Uid + "@bkstudydesk");
            Line("DTSTAMP:" + stamp);
            Line("DTSTART:" + Utc(e.Start - VietnamOffset));
            Line("DTEND:" + Utc(e.End - VietnamOffset));
            Line("SUMMARY:" + Escape(e.Summary));
            if (!string.IsNullOrWhiteSpace(e.Location)) Line("LOCATION:" + Escape(e.Location));
            if (!string.IsNullOrWhiteSpace(e.Description)) Line("DESCRIPTION:" + Escape(e.Description));
            Line("END:VEVENT");
        }
        Line("END:VCALENDAR");
        return sb.ToString();
    }

    private static string Utc(DateTime t) => t.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    public static string Escape(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(";", "\\;", StringComparison.Ordinal).Replace(",", "\\,", StringComparison.Ordinal)
         .Replace("\r\n", "\\n", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    public static IEnumerable<string> Fold(string line)
    {
        var start = 0;
        var limit = 75;
        while (start < line.Length)
        {
            var end = start;
            var bytes = 0;
            while (end < line.Length)
            {
                var len = char.IsSurrogatePair(line, end) ? 2 : 1;
                var b = Encoding.UTF8.GetByteCount(line.AsSpan(end, len));
                if (bytes + b > limit) break;
                bytes += b;
                end += len;
            }
            yield return (start == 0 ? "" : " ") + line[start..end];
            start = end;
            limit = 74;
        }
        if (line.Length == 0) yield return "";
    }

    public static DateTime ClassDate(int termStartYear, int firstWeek, int week, int mybkDay)
    {
        var year = week < firstWeek ? termStartYear + 1 : termStartYear;
        var dow = mybkDay == 8 ? DayOfWeek.Sunday : (DayOfWeek)(mybkDay - 1);
        return ISOWeek.ToDateTime(year, week, dow);
    }

    public static List<DateTime> ClassDates(int? termStartYear, IReadOnlyCollection<int> weeks, int mybkDay, DateTime today)
    {
        var dates = new List<DateTime>();
        if (weeks.Count == 0 || mybkDay is < 2 or > 8) return dates;
        var straddles = weeks.Any(w => w >= 27) && weeks.Any(w => w < 27);
        var first = straddles ? weeks.Where(w => w >= 27).Min() : weeks.Min();
        var year = termStartYear ?? (straddles && ISOWeek.GetWeekOfYear(today) < 27 ? today.Year - 1 : today.Year);
        foreach (var w in weeks.Distinct())
            if (w >= 1 && w <= ISOWeek.GetWeeksInYear(w < first ? year + 1 : year)) dates.Add(ClassDate(year, first, w, mybkDay));
        return dates;
    }

    public static int? Minutes(string? hhmm) => VnTime.ParseClock(hhmm);
}
