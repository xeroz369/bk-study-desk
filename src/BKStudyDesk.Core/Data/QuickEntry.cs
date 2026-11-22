using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SoHocTap.Core;

namespace SoHocTap.Data;

public enum EntryField { Title, Date, Start, End }

public sealed record QuickEntryResult(string Title, DateTime? Date, int? Start, int? End, string Location, string Kind,
    IReadOnlyDictionary<EntryField, string> Errors, IReadOnlySet<EntryField> Missing)
{
    public bool Ok => Errors.Count == 0;
}

public static partial class QuickEntry
{
    public const string ErrTitleMissing = "events.err.titleMissing";
    public const string ErrDateMissing = "events.err.dateMissing";
    public const string ErrDateBad = "events.err.dateBad";
    public const string ErrStartMissing = "events.err.startMissing";
    public const string ErrStartBad = "events.err.startBad";
    public const string ErrEndBad = "events.err.endBad";
    public const string ErrEndBeforeStart = "events.err.endBeforeStart";

    public static readonly string[] ErrorKeys = [ErrTitleMissing, ErrDateMissing, ErrDateBad, ErrStartMissing, ErrStartBad, ErrEndBad, ErrEndBeforeStart];

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
        while (parts.Count > 1 && parts[^1].Length == 0) parts.RemoveAt(parts.Count - 1);

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

    public static (int? Start, int? End, bool EndBad) ParseRange(string? text)
    {
        var p = (text ?? "").Split(['-', '–'], 2, StringSplitOptions.TrimEntries);
        var start = VnTime.ParseClock(p[0]);
        if (p.Length < 2 || p[1].Length == 0) return (start, null, false);
        var end = VnTime.ParseClock(p[1]);
        return (start, end, end is null);
    }

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

    public static string Clock(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

    public static string Format(CustomEvent e)
    {
        var date = e.Day is { } d ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : e.Date;
        var time = e.End.Length > 0 ? $"{e.Start}-{e.End}" : e.Start;
        return string.Join(" - ", new[] { e.Title, date, time, e.Location }.Where(x => x.Length > 0));
    }
}
