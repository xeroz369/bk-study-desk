using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public sealed record WeekRow(int Order, string Group, string Time, int StartMin, string Name, string Code, string Room, string Lessons, string Teacher,
    bool InWeek, string? CustomId = null);

public sealed record ExamRow(string Subject, string When, long Time, string Label, string Left, bool Past);

public enum TimetableGap { None, SyncOff, SignedOut, NoData, TermEmpty, WeekEmpty }

public sealed record TimetableNote(TimetableGap Gap, string? Text, bool ShowGrid);

public static class WeekPresenter
{
    public static string Title(DateTime monday) => L.F("calendar.week", Format.IsoWeek(monday), monday, monday.AddDays(6));

    public static IReadOnlyList<WeekRow> Rows(MybkData? mybk, IEnumerable<CustomEvent> custom, DateTime monday)
    {
        var week = Format.IsoWeek(monday);
        string Day(int day) => L.F("format.dateLong", Format.MybkDays.GetValueOrDefault(day) ?? L.F("calendar.weekday", day), monday.AddDays(day - 2));
        var rows = (mybk?.Schedule ?? []).Where(c => c.Day is >= 2 and <= 8)
            .Select(c => new WeekRow(c.Day, Day(c.Day), $"{c.Start}-{c.End}", VnTime.ParseClock(c.Start) ?? 24 * 60, c.Name, c.Code, c.Room,
                $"{c.Lesson}-{c.Lesson + c.Lessons - 1}", c.Teacher ?? "", c.Weeks.Contains(week)))
            .ToList();
        foreach (var e in custom.Where(e => e.Day is { } d && d >= monday.Date && d < monday.Date.AddDays(7) && e.StartMin is not null))
        {
            var day = Format.MybkDay(e.Day!.Value);
            rows.Add(new WeekRow(day, Day(day), AppState.CustomTime(e), e.StartMin ?? 0, e.Title, L.T(e.IsMakeup ? "kind.makeup" : "kind.custom"),
                e.Location, "", e.Note, true, e.Id));
        }
        return [.. rows.OrderBy(r => r.Order).ThenBy(r => r.StartMin)];
    }

    public static TimetableNote Note(MybkData? mybk, bool syncOn, bool signedIn, string? noData, int shown)
    {
        if (mybk?.Schedule.Count > 0)
            return shown > 0 ? new(TimetableGap.None, null, true) : new(TimetableGap.WeekEmpty, L.T("calendar.weekEmpty"), true);
        var term = mybk?.Term.Name is { Length: > 0 } name ? name : L.T("calendar.thisTerm");
        var (gap, text) = !syncOn ? (TimetableGap.SyncOff, L.T("calendar.mybkOff"))
            : !signedIn ? (TimetableGap.SignedOut, L.T("calendar.mybkSignedOut"))
            : noData is not null ? (TimetableGap.NoData, noData)
            : (TimetableGap.TermEmpty, L.F("calendar.mybkNoTimetable", term));
        return new(gap, text, shown > 0);
    }

    public static IReadOnlyList<string> NoSlot(MybkData? mybk) =>
        [.. (mybk?.Schedule ?? []).Where(c => c.Day is < 2 or > 8 || VnTime.ParseClock(c.Start) is null || VnTime.ParseClock(c.End) is null)
            .Select(c => c.Name).Distinct()];

    public static IReadOnlyList<ExamRow> Exams(IReadOnlyList<TimelineItem> timeline, long now) =>
        [.. timeline.Where(e => e.Kind == "exam").OrderBy(e => e.Time < now).ThenBy(e => e.Time < now ? -e.Time : e.Time).Select(e => new ExamRow(e.Subject, e.When, e.Time, e.Label, e.Left, e.Time < now))];

    public static (List<IcsEvent> Events, List<string> Skipped) Ics(MybkData? m, IEnumerable<CustomEvent> custom, DateTime today)
    {
        var events = custom.Select(x => CustomEvents.ToIcs(x, L.T("kind.makeup"))).OfType<IcsEvent>().ToList();
        var skipped = new List<string>();
        foreach (var c in (m?.Schedule ?? []).Where(c => c.Day is >= 2 and <= 8 && c.Weeks.Count > 0))
        {
            if (VnTime.ParseClock(c.Start) is not { } s || VnTime.ParseClock(c.End) is not { } en || en <= s) { skipped.Add(c.Name); continue; }
            foreach (var date in Core.Ics.ClassDates(c.Year, c.Weeks, c.Day, today))
                events.Add(new IcsEvent($"cl-{c.Code}-{c.Group}-{date:yyyyMMdd}-{s}", date.AddMinutes(s), date.AddMinutes(en), c.Name, c.Room,
                    string.Join("\n", new[] { c.Code + (c.Group is { } g ? ", " + g : ""), c.Teacher ?? "" }.Where(x => x.Length > 0))));
        }
        foreach (var x in m?.Exams ?? [])
        {
            if (VnTime.ParseDate(x.Date) is not { } d || VnTime.ParseClock(x.Time) is not { } min) { skipped.Add(L.F("timeline.exam", x.Name)); continue; }
            var start = d.AddMinutes(min);
            var type = x.Type == "GK" ? L.T("timeline.examMid") : x.Type == "CK" ? L.T("timeline.examFinal") : "";
            var title = type.Length > 0 ? L.F("timeline.examTyped", type, x.Name) : L.F("timeline.exam", x.Name);
            events.Add(new IcsEvent($"ex-{x.Code}-{x.Type}-{start:yyyyMMddHHmm}", start, start.AddMinutes(x.Minutes ?? 90), title, x.Room, x.Code));
        }
        return (events, [.. skipped.Distinct()]);
    }
}
