using System.Globalization;
using System.Text;

namespace SoHocTap.Core;

/// <summary>Một sự kiện để xuất. Start/End là giờ Việt Nam (giờ ghi trên MyBK), không phụ thuộc múi giờ của máy.</summary>
public sealed record IcsEvent(string Uid, DateTime Start, DateTime End, string Summary, string? Location = null, string? Description = null);

/// <summary>
/// Xuất lịch ra file iCalendar (.ics, RFC 5545) để nhập vào Google Calendar, Outlook, Lịch của Windows.
/// - Giờ ghi dạng UTC ("Z"): Việt Nam là UTC+7 quanh năm, không có giờ mùa hè, nên đổi bằng cách trừ 7 giờ.
/// - UID cố định theo môn, ngày, giờ: nhập lại cùng file thì ứng dụng lịch nhận ra sự kiện cũ thay vì tạo bản trùng (tùy ứng dụng).
/// - Dòng dài hơn 75 byte được gấp (folding) và ký tự đặc biệt được escape theo RFC 5545 mục 3.1 và 3.3.11.
/// </summary>
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

    /// <summary>Escape giá trị TEXT: \ ; , và xuống dòng.</summary>
    public static string Escape(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(";", "\\;", StringComparison.Ordinal).Replace(",", "\\,", StringComparison.Ordinal)
         .Replace("\r\n", "\\n", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>Gấp dòng: mỗi dòng tối đa 75 byte UTF-8, dòng nối tiếp bắt đầu bằng một dấu cách. Không cắt giữa một ký tự.</summary>
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
            limit = 74;   // dòng nối tiếp đã có 1 byte dấu cách
        }
        if (line.Length == 0) yield return "";
    }

    /// <summary>
    /// Ngày của buổi học: tuần ISO <paramref name="week"/>, thứ theo MyBK (2 = Thứ hai … 8 = Chủ nhật).
    /// Tuần học của một kỳ có thể vắt qua năm mới (…, 52, 1, 2…): tuần nhỏ hơn tuần đầu kỳ thuộc năm sau.
    /// </summary>
    public static DateTime ClassDate(int termStartYear, int firstWeek, int week, int mybkDay)
    {
        var year = week < firstWeek ? termStartYear + 1 : termStartYear;
        var dow = mybkDay == 8 ? DayOfWeek.Sunday : (DayOfWeek)(mybkDay - 1);
        return ISOWeek.ToDateTime(year, week, dow);
    }

    /// <summary>
    /// Mọi ngày học của một môn trong kỳ, mỗi tuần MyBK liệt kê một buổi. Kỳ vắt qua năm mới (tuần 52 rồi tuần 1) thì tuần đầu kỳ
    /// là tuần nhỏ nhất trong nửa sau của năm. MyBK không ghi năm (calendarYear) thì đoán theo <paramref name="today"/>: kỳ vắt năm mà
    /// đang ở nửa đầu năm sau thì năm bắt đầu kỳ là năm trước. Tuần không có trong năm (tuần 53 của năm chỉ có 52 tuần) thì bỏ qua.
    /// </summary>
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

    /// <summary>"07:00" hay "7g30" → số phút từ 0 giờ; chuỗi lạ → null (xem <see cref="VnTime.ParseClock"/>).</summary>
    public static int? Minutes(string? hhmm) => VnTime.ParseClock(hhmm);
}
