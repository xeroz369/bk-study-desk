using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public sealed record TodoItem(string Id, DateTime Day, string Title, string Sub, string Left, Urgency Level, string State, string? Url, bool Group = false, bool Indent = false,
    bool Done = false, string Hour = "")
{
    public bool IsDanger => Level is Urgency.Overdue or Urgency.Urgent;
    public bool IsSoon => Level == Urgency.Soon;

    public int DaysLeft => (Day.Date - Format.Today).Days;

    public bool ChipDanger => !Done && (IsDanger || DaysLeft <= 3);

    public bool ChipSoon => !Done && !ChipDanger && (IsSoon || DaysLeft <= 7);

    public string Month => L.F("home.monthOf", Day);
}

public sealed record AgendaItem(string Id, DateTime Day, string Start, string End, string Title, string Sub, string Right);

public sealed record NewsItem(string Title, string Sub, string Ago, string? Url, Reading? Read = null);

public sealed record RegItem(string Code, string Name, string When, string Status, bool Open);

public sealed record HomeModel(bool HasLms, bool HasMybk, IReadOnlyList<TodoItem> Todo, IReadOnlyList<AgendaItem> Upcoming,
    IReadOnlyList<NewsItem> News, IReadOnlyList<RegItem> Registrations);

public static class HomePresenter
{
    public static int TodoDays => Config.Int("app.home.todoDays", 14);
    public static int ExamDays => Config.Int("app.home.examDays", 14);
    public static int NewsDays => Config.Int("app.home.newsDays", 7);

    public sealed record MybkPart(IReadOnlyList<MybkClass> Classes, IReadOnlyList<MybkExam> Exams, IReadOnlyList<MybkRegistration> Registrations);

    public static HomeModel Build(IReadOnlyList<TimelineItem> timeline, LmsData? lms, MybkData? mybk, long now) =>
        Build(timeline, lms is not null, lms?.Announcements ?? [], mybk is null ? null : new MybkPart(mybk.Schedule, mybk.Exams, mybk.Registration ?? []), now);

    public static HomeModel Build(IReadOnlyList<TimelineItem> timeline, bool hasLms, IReadOnlyList<LmsAnnouncement> news, MybkPart? mybk, long now)
    {
        var today = VnTime.ToWall(now).Date;
        var opens = timeline.Where(e => e.Kind == "quiz" && e.Opens).GroupBy(e => (e.Name, e.Course)).ToDictionary(g => g.Key, g => g.Min(e => e.Time));
        var todo = timeline
            .Where(e => e.Kind is "assign" or "quiz" or "event" && !e.Opens && (!e.Done && e.Overdue || (e.Time >= now && e.Time <= now + TodoDays * 86400L)))
            .OrderBy(e => e.Done).ThenBy(e => e.GroupRank).ThenBy(e => e.Time)
            .Select(e => new TodoItem(e.Id, VnTime.ToWall(e.Time).Date, e.Name,
                e.Kind == "quiz" && opens.TryGetValue((e.Name, e.Course), out var open) && open < e.Time ? QuizSub(e, open) : Sub(e),
                e.Done ? Ui.Due.LeftText(e.Time, now) : e.Left, e.Done ? Urgency.None : e.Urgency, e.Done ? DoneTag(e.Kind) : e.StateText, e.Url, Done: e.Done, Hour: Format.Hm(e.Time)))
            .ToList();

        var upcoming = new List<(long Time, AgendaItem Item)>();
        foreach (var c in mybk?.Classes ?? [])
            foreach (var d in Ics.ClassDates(c.Year, c.Weeks ?? [], c.Day, today).Where(d => d == today || d == today.AddDays(1)))
                upcoming.Add((VnTime.FromWall(d.AddMinutes(VnTime.ParseClock(c.Start) ?? 0)),
                    new AgendaItem($"cl-{c.Code}-{d:yyyyMMdd}", d, c.Start, c.End, c.Name, L.F("timeline.room", c.Room),
                        L.T(d == today ? "format.group.today" : "format.group.tomorrow"))));
        foreach (var x in mybk?.Exams ?? [])
        {
            if (VnTime.ParseDate(x.Date) is not { } d || d < today || d > today.AddDays(ExamDays)) continue;
            var time = VnTime.FromWall(d.AddMinutes(VnTime.ParseClock(x.Time) ?? 0));
            var start = VnTime.ParseClock(x.Time) is { } m ? $"{m / 60:00}:{m % 60:00}" : x.Time;
            var sub = x.Minutes is { } min ? L.F("timeline.roomMinutes", x.Room, min) : L.F("timeline.room", x.Room);
            upcoming.Add((time, new AgendaItem($"ex-{x.Code}-{x.Type}", d, start, "", x.Name, sub, Due.LeftText(time, now))));
        }

        var newsItems = news.Where(a => a.Time > now - NewsDays * 86400L).OrderByDescending(a => a.Time)
            .Select(a => new NewsItem(a.Title, a.Subject, Format.Ago(a.Time), a.Url, SubjectsPresenter.ReadingOf(a))).ToList();

        var regs = Registrations.Upcoming(mybk?.Registrations ?? [], now).Select(r =>
        {
            var open = Registrations.IsOpen(r, now);
            return new RegItem(r.Code, r.Name, open ? L.F("home.reg.closes", Format.DateTime(r.End)) : L.F("home.reg.opens", Format.DateTime(r.Start)),
                open ? L.T("home.reg.openNow") : Format.Until(r.Start), open);
        }).ToList();

        return new HomeModel(hasLms, mybk is not null, todo, [.. upcoming.OrderBy(u => u.Time).Select(u => u.Item)], newsItems, regs);
    }

    public static string SyncLine(long? lms, long? mybk)
    {
        static string At(long? t) => t is { } x ? StatusPanel.When(x) : L.T("home.sync.never");
        return L.F("home.sync", At(lms), At(mybk));
    }

    private static string DoneTag(string kind) => L.T(kind switch { "assign" => "state.submitted", "quiz" => "home.done.quiz", _ => "home.done.event" });

    private static string QuizSub(TimelineItem e, long open)
    {
        var sameDay = VnTime.ToWall(open).Date == VnTime.ToWall(e.Time).Date;
        var window = L.F("home.quizWindow", sameDay ? Format.Hm(open) : Format.DateTime(open), Format.Hm(e.Time));
        return e.Subject.Length == 0 ? window : $"{e.Subject}, {window}";
    }

    private static string Sub(TimelineItem e)
    {
        var hour = e.Hour;
        var label = hour.Length > 0 ? $"{e.Label} {hour}" : e.Label;
        return e.Subject.Length == 0 ? label : $"{e.Subject}, {label}";
    }
}
