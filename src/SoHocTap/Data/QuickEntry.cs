using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SoHocTap.Core;

namespace SoHocTap.Data;

/// <summary>Ô trong form thêm sự kiện, để báo lỗi đúng chỗ.</summary>
public enum EntryField { Title, Date, Start, End }

/// <summary>
/// Kết quả đọc một dòng nhập nhanh. Phần nào đọc được thì có giá trị, dù phần khác lỗi (form điền dần khi gõ).
/// Errors: key ngôn ngữ của lỗi theo từng ô (bản tiếng Việt ở lang/vi.json). Missing: ô chưa gõ tới, khác với gõ sai,
/// để lúc đang gõ chỉ báo phần sai chứ không la "thiếu ngày" khi người dùng mới gõ xong tên.
/// </summary>
public sealed record QuickEntryResult(string Title, DateTime? Date, int? Start, int? End, string Location, string Kind,
    IReadOnlyDictionary<EntryField, string> Errors, IReadOnlySet<EntryField> Missing)
{
    public bool Ok => Errors.Count == 0;
}

/// <summary>
/// Nhập nhanh một sự kiện bằng một dòng (issue #22): "Nội dung - Ngày - giờ - địa điểm", ví dụ
/// "Học bù Giải tích 2 - 12/10 - 7:00-8:50 - H1-201". Hàm thuần, không đụng UI hay file.
/// - Phân cách là " - " (có dấu cách hai bên), nên "H1-201" hay "7:00-8:50" không bị tách.
/// - Ngày: dd/MM, dd/MM/yyyy, yyyy-MM-dd. Không ghi năm thì lấy ngày gần nhất từ hôm nay trở đi.
/// - Giờ: "7:00", "7h", "7g30", "07:00-08:50" (đọc bằng <see cref="VnTime.ParseClock"/>).
/// - Có chữ "học bù" thì là buổi học bù.
/// </summary>
public static partial class QuickEntry
{
    public const string ErrTitleMissing = "events.err.titleMissing";
    public const string ErrDateMissing = "events.err.dateMissing";
    public const string ErrDateBad = "events.err.dateBad";
    public const string ErrStartMissing = "events.err.startMissing";
    public const string ErrStartBad = "events.err.startBad";
    public const string ErrEndBad = "events.err.endBad";
    public const string ErrEndBeforeStart = "events.err.endBeforeStart";

    /// <summary>Mọi key lỗi, để test kiểm đủ chữ trong file ngôn ngữ.</summary>
    public static readonly string[] ErrorKeys = [ErrTitleMissing, ErrDateMissing, ErrDateBad, ErrStartMissing, ErrStartBad, ErrEndBad, ErrEndBeforeStart];

    // Dấu gạch ở đầu hay cuối dòng cũng là phân cách (" - 12/10" là bỏ trống tên), nhưng "H1-201", "7:00-8:50" thì không.
    [GeneratedRegex(@"(?:^|\s+)[-–—](?:\s+|$)")] private static partial Regex Separator();
    [GeneratedRegex(@"^(\d{4})-(\d{1,2})-(\d{1,2})$")] private static partial Regex IsoDate();
    [GeneratedRegex(@"^(\d{1,2})/(\d{1,2})(?:/(\d{4}))?$")] private static partial Regex VnDate();
    [GeneratedRegex(@"^(?:ng[aà]y\s+)", RegexOptions.IgnoreCase)] private static partial Regex DayWord();
    [GeneratedRegex(@"\bh[oọ]c\s+b[uù]\b", RegexOptions.IgnoreCase)] private static partial Regex Makeup();

    public static QuickEntryResult Parse(string? line, DateTime today)
    {
        var text = (line ?? "").Normalize(NormalizationForm.FormC).Trim();
        var errors = new Dictionary<EntryField, string>();
        var missing = new HashSet<EntryField>();
        List<string> parts = text.Length == 0 ? [] : [.. Separator().Split(text).Select(p => p.Trim())];
        // Đang gõ dở "Họp - 12/10 -": phần rỗng ở cuối là chưa gõ tới, không phải gõ sai.
        while (parts.Count > 1 && parts[^1].Length == 0) parts.RemoveAt(parts.Count - 1);

        // Tiêu đề có thể chứa " - " ("Lab 2 - Kỹ thuật số - 12/10 - ..."): phần đầu tiên đọc được là ngày mới là ngày.
        var dateAt = parts.Count > 1 ? parts.FindIndex(1, p => ParseDate(p, today) is not null) : -1;
        DateTime? date = null;
        string title;
        List<string> rest;
        if (dateAt > 0)
        {
            title = string.Join(" - ", parts.Take(dateAt));
            date = ParseDate(parts[dateAt], today);
            rest = parts.Skip(dateAt + 1).ToList();
        }
        else
        {
            title = parts.Count > 0 ? parts[0] : "";
            rest = parts.Skip(1).ToList();
            // Bỏ qua ngày mà gõ luôn giờ ("Họp nhóm - 19:00") thì là thiếu ngày; còn lại là ngày gõ sai.
            if (rest.Count == 0 || ParseRange(rest[0]) is { Start: not null }) { errors[EntryField.Date] = ErrDateMissing; missing.Add(EntryField.Date); }
            else { errors[EntryField.Date] = ErrDateBad; rest.RemoveAt(0); }
        }
        if (title.Length == 0) { errors[EntryField.Title] = ErrTitleMissing; missing.Add(EntryField.Title); }

        int? start = null, end = null;
        if (rest.Count == 0) { errors[EntryField.Start] = ErrStartMissing; missing.Add(EntryField.Start); }
        else
        {
            var (s, e, endBad) = ParseRange(rest[0]);
            rest.RemoveAt(0);
            // "7:00 - 8:50" (có dấu cách) bị tách làm hai phần: phần sau chỉ là một giờ thì đó là giờ kết thúc, không phải địa điểm.
            if (s is not null && e is null && !endBad && rest.Count > 0 && VnTime.ParseClock(rest[0]) is { } e2)
            {
                e = e2;
                rest.RemoveAt(0);
            }
            if (s is null) errors[EntryField.Start] = ErrStartBad;
            else if (endBad) errors[EntryField.End] = ErrEndBad;
            else if (e is not null && e <= s) errors[EntryField.End] = ErrEndBeforeStart;
            start = s;
            end = e;
        }
        var location = string.Join(" - ", rest);
        var kind = Makeup().IsMatch(text) ? CustomEvents.KindMakeup : CustomEvents.KindEvent;
        return new QuickEntryResult(title, date, start, end, location, kind, errors, missing);
    }

    /// <summary>
    /// Ngày theo các dạng dd/MM, dd/MM/yyyy, yyyy-MM-dd (cho phép chữ "ngày" ở đầu). Không có năm thì lấy ngày gần nhất
    /// từ <paramref name="today"/> trở đi; 29/02 thì tới năm nhuận gần nhất. Ngày không có thật (31/04) thì null.
    /// </summary>
    public static DateTime? ParseDate(string? text, DateTime today)
    {
        var s = DayWord().Replace((text ?? "").Trim(), "");
        if (IsoDate().Match(s) is { Success: true } iso) return Make(Num(iso.Groups[1]), Num(iso.Groups[2]), Num(iso.Groups[3]));
        if (VnDate().Match(s) is not { Success: true } vn) return null;
        var (day, month) = (Num(vn.Groups[1]), Num(vn.Groups[2]));
        if (vn.Groups[3].Success) return Make(Num(vn.Groups[3]), month, day);
        for (var y = today.Year; y <= today.Year + 8; y++)
            if (Make(y, month, day) is { } d && d >= today.Date) return d;
        return null;

        static int Num(Group g) => int.Parse(g.Value, NumberStyles.None, CultureInfo.InvariantCulture);
        static DateTime? Make(int y, int m, int d) =>
            y is >= 2000 and <= 2100 && m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m) ? new DateTime(y, m, d) : null;
    }

    /// <summary>"7:00", "7h", "07:00-08:50", "7g30-9h": phút bắt đầu, phút kết thúc (null nếu không ghi), và giờ kết thúc có gõ mà sai.</summary>
    public static (int? Start, int? End, bool EndBad) ParseRange(string? text)
    {
        var p = (text ?? "").Split(['-', '–'], 2, StringSplitOptions.TrimEntries);
        var start = VnTime.ParseClock(p[0]);
        if (p.Length < 2 || p[1].Length == 0) return (start, null, false);
        var end = VnTime.ParseClock(p[1]);
        return (start, end, end is null);
    }

    /// <summary>Kiểm tra form (lúc bấm Lưu): như khi nhập nhanh nhưng từng ô riêng. Rỗng = hợp lệ.</summary>
    public static Dictionary<EntryField, string> Validate(string? title, DateTime? date, string? start, string? end)
    {
        var errors = new Dictionary<EntryField, string>();
        if (string.IsNullOrWhiteSpace(title)) errors[EntryField.Title] = ErrTitleMissing;
        if (date is null) errors[EntryField.Date] = ErrDateMissing;
        var s = VnTime.ParseClock(start);
        if (string.IsNullOrWhiteSpace(start)) errors[EntryField.Start] = ErrStartMissing;
        else if (s is null) errors[EntryField.Start] = ErrStartBad;
        if (!string.IsNullOrWhiteSpace(end))
        {
            var e = VnTime.ParseClock(end);
            if (e is null) errors[EntryField.End] = ErrEndBad;
            else if (s is not null && e <= s) errors[EntryField.End] = ErrEndBeforeStart;
        }
        return errors;
    }

    /// <summary>Số phút → "HH:mm" (cùng dạng giờ trên MyBK và trong file).</summary>
    public static string Clock(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

    /// <summary>Dựng lại dòng nhập nhanh từ một sự kiện (Sao chép, mở form sửa): đọc lại bằng <see cref="Parse"/> ra đúng sự kiện đó.</summary>
    public static string Format(CustomEvent e)
    {
        var date = e.Day is { } d ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : e.Date;
        var time = e.End.Length > 0 ? $"{e.Start}-{e.End}" : e.Start;
        return string.Join(" - ", new[] { e.Title, date, time, e.Location }.Where(x => x.Length > 0));
    }
}
