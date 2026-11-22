using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using SoHocTap.Sources;
using SoHocTap.Sources.Lms;

namespace SoHocTap.Ui;

public sealed record TimelineItem(string Id, string Kind, string Name, string Subject, long Time, string Label, string? Url, string Source,
    bool Done = false, string? Warn = null, long? Course = null, bool Opens = false, long? Due = null, string? RawWhen = null, string ClassGroup = "",
    SubmissionState State = SubmissionState.Unknown)
{
    public const long UndatedTime = 253_370_764_800;
    public bool Undated => Time >= UndatedTime;

    public bool Overdue => Source == SourceIds.Lms && !Done && !Opens && !Undated && Time < Format.Now && Time >= Format.Now - Sources.Lms.AssignPairing.WindowBefore;

    public int GroupRank => Overdue ? 0 : 1;

    public static readonly Dictionary<string, string> KindText = new()
    {
        ["assign"] = "kind.assign",
        ["quiz"] = "kind.quiz",
        ["event"] = "kind.event",
        ["exam"] = "kind.exam",
        ["class"] = "kind.class",
        ["reg"] = "kind.reg",
        ["custom"] = "kind.custom",
        ["makeup"] = "kind.makeup",
    };

    public const string CustomPrefix = "ce-";

    public string? CustomId => Source == "custom" && Id.StartsWith(CustomPrefix, StringComparison.Ordinal) ? Id[CustomPrefix.Length..] : null;
    public string StateText => State switch
    {
        SubmissionState.Submitted => L.T("state.submitted"),
        SubmissionState.Draft => L.T("state.draft"),
        SubmissionState.NotSubmitted => L.T("state.notSubmitted"),
        SubmissionState.Graded => L.T("state.graded"),
        _ => "",
    };
    public string KindName => Opens && Kind == "quiz" ? L.T("kind.quizOpen") : KindText.TryGetValue(Kind, out var k) ? L.T(k) : Kind;
    public string Sub => Subject.Length == 0 || Subject == Name ? Label : Label.Length == 0 ? Subject : $"{Subject}, {Label}";
    public string WhenAndLabel => Label.Length == 0 ? When : $"{When}, {Label}";
    public string Spoken => Sub.Length == 0 ? Name : $"{Name}, {Sub}";
    public string When => RawWhen ?? Format.DateTime(Time);
    public string Left => Kind == "class" || Undated ? "" : Done ? L.T("timeline.done") : Overdue ? L.F("timeline.overdueOn", Format.Local(Time))
        : Opens ? (Due is { } d ? L.F("timeline.dueOn", Format.Local(d)) : L.T("timeline.opens")) : Ui.Due.LeftText(Time, Format.Now);
    public Urgency Urgency
    {
        get
        {
            if (Kind == "class" || Undated || Opens) return Urgency.None;
            var u = Ui.Due.Of(Time, Format.Now, Done, Core.Settings.Notify.UrgentHours, Core.Settings.Notify.SoonHours);
            return u == Urgency.Overdue && !Overdue ? Urgency.None : u;
        }
    }
    public string Group => Undated ? L.T("timeline.undated") : Overdue ? L.T("format.group.overdue") : Format.DayGroup(Time);
    public string Hour => RawWhen is not null || Undated ? "" : Format.Hm(Time);
    public string Day => Undated ? L.T("timeline.undated") : Overdue ? L.T("format.group.overdue") : Format.DayDiff(Time) switch
    {
        0 => L.F("format.dayToday", Format.DateLong(Format.Local(Time))),
        1 => L.F("format.dayTomorrow", Format.DateLong(Format.Local(Time))),
        _ => Format.DateLong(Format.Local(Time)),
    };
}

public enum AccountNeed { None, Lms, Mybk, Both }

public sealed partial class AppState(SourceHub hub)
{
    public LmsData? Lms { get; private set; }
    public MybkData? Mybk { get; private set; }
    public JsonObject Sources { get; private set; } = [];
    public List<TimelineItem> Timeline { get; private set; } = [];
    public event Action? Changed;

    public void Reload()
    {
        Lms = LmsStore.Read();
        Mybk = MybkStore.Read();
        RefreshStatus();
    }

    public event Action? Progress;

    public void RefreshProgress()
    {
        Sources = hub.StatusJson();
        Progress?.Invoke();
    }

    public event Action? StatusChanged;

    public void RefreshStatusOnly()
    {
        Sources = hub.StatusJson();
        StatusChanged?.Invoke();
    }

    public void RefreshStatus()
    {
        Sources = hub.StatusJson();
        Timeline = BuildTimeline();
        Changed?.Invoke();
    }

    public JsonObject? Source(string name) => Sources[name] as JsonObject;
    public bool Syncing(string name) => Source(name)?["syncing"]?.GetValue<bool>() == true;
    public string? Error(string name) => Source(name)?["error"]?.GetValue<string>();
    public long? SyncedAt(string name) => Source(name)?["syncedAt"]?.GetValue<long?>();

    public SyncErrorKind? ErrorKind(string name) => Error(name) is null ? null : SyncErrorText.Parse(Source(name)?["errorKind"]?.GetValue<string>());

    public static string? Unreadable(string name) => name switch
    {
        SourceIds.Lms => LmsStore.Problem,
        SourceIds.Mybk => MybkStore.Problem,
        _ => null,
    };

    public IReadOnlyList<(string What, string Detail)> Warnings(string name) =>
        (Source(name)?["warnings"] as JsonArray ?? []).Select(w => w?.GetValue<string>() ?? "").Where(w => w.Length > 0)
            .Select(w => w.Split(SyncSignal.DetailMark, 2) is var p && p.Length == 2 ? (p[0], p[1]) : (w, "")).ToList();

    public DateTimeOffset? NextAutoAt(string name) => hub.NextAutoAt(name);

    public (string Text, string? Detail)? ExplainError(string name, string label) =>
        Error(name) is { } raw ? Explain(label, ErrorKind(name), raw) : null;

    public static (string Text, string? Detail) Explain(string label, SyncErrorKind? kind, string raw) =>
        kind is { } k ? (L.F(SyncErrorText.LangKey(k), label), raw) : Explain(label, raw);

    public static (string Text, string? Detail) Explain(string label, string raw)
    {
        if (raw.StartsWith("Không ", StringComparison.Ordinal) || raw.StartsWith("Phiên ", StringComparison.Ordinal)
            || raw.Contains("không phản hồi", StringComparison.Ordinal) || raw.Contains("giới hạn", StringComparison.Ordinal)
            || raw.Contains("bảo trì", StringComparison.Ordinal))
            return (raw, null);
        if (raw.Contains("Access to the path", StringComparison.OrdinalIgnoreCase) || raw.Contains("being used by another process", StringComparison.OrdinalIgnoreCase))
            return (L.F("error.fileBusy", label), raw);
        return (L.F("error.generic", label), raw);
    }

    public SyncProgress? Step(string name) =>
        Source(name) is { } o && o["step"]?.GetValue<string>() is { } k
            ? new SyncProgress(k, o["count"]?.GetValue<int>() ?? 0, o["of"]?.GetValue<int>() ?? 0, o["detail"]?.GetValue<string>(), o["permille"]?.GetValue<int?>())
            : null;

    public string? NoDataReason(string name, string label)
    {
        if (Unreadable(name) is not null) return L.F("data.unreadable", label);
        if (SyncedAt(name) is not null) return null;
        if (Syncing(name)) return L.F("data.loading", label);
        if (Error(name) is { } err) return L.F("data.failed", label, ErrorKind(name) is { } k ? L.F(SyncErrorText.LangKey(k), label) : err);
        return L.F("data.none", label);
    }

    [GeneratedRegex("hết hạn|chưa đăng nhập|cần đăng nhập|chưa sẵn sàng|invalidtoken", RegexOptions.IgnoreCase)] private static partial Regex LoginError();

    private bool NeedsLogin(string name) =>
        ErrorKind(name) is { } k ? k == SyncErrorKind.SessionExpired : Error(name) is { } e && LoginError().IsMatch(e);

    public AccountNeed Account
    {
        get
        {
            var lms = Source(SourceIds.Lms) is { } l && (l["connected"]?.GetValue<bool>() != true || NeedsLogin(SourceIds.Lms));
            var mybk = Settings.Sync.MybkEnabled && NeedsLogin(SourceIds.Mybk);
            return (lms, mybk) switch { (true, true) => AccountNeed.Both, (true, _) => AccountNeed.Lms, (_, true) => AccountNeed.Mybk, _ => AccountNeed.None };
        }
    }

    [GeneratedRegex(@"(\d{1,2})/(\d{1,2})/(\d{4})")] private static partial Regex DateInName();

    public static (long Named, string Text)? QuizAnomaly(LmsQuiz q)
    {
        var m = DateInName().Match(q.Name);
        var refTime = q.Open ?? q.Close;
        if (!m.Success || refTime is null) return null;
        try
        {
            var named = Format.Sec(new DateTime(int.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 0, 1, 0));
            return Math.Abs(named - refTime.Value) >= 30 * 86400 ? (named, m.Value) : null;
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    public static string LmsLabel(LmsEvent e) =>
        e.Kind == "assign" && e.Label == global::SoHocTap.Sources.Lms.LmsSource.DueLabel ? L.T("timeline.dueLabel") : e.Label;

    public static (bool Opens, long? Due) Opening(LmsEvent e, LmsData lms)
    {
        var quiz = e.Kind == "quiz" ? lms.Quizzes.FirstOrDefault(q => q.Name == e.Name && q.Course == e.Course) : null;
        var opens = e.Phase == "open" || (e.Kind == "quiz" && e.Phase is null && quiz?.Open == e.Time && quiz.Close != e.Time);
        if (!opens) return (false, null);
        var close = lms.Events.FirstOrDefault(x => x.Kind == e.Kind && x.Phase == "close" && x.Name == e.Name && x.Course == e.Course)?.Time;
        return (true, close ?? quiz?.Close);
    }

    private List<TimelineItem> BuildTimeline()
    {
        var output = new List<TimelineItem>();
        if (Lms is { } lms)
        {
            var seen = new HashSet<string>();
            foreach (var e in lms.Events)
            {
                var (opens, due) = Opening(e, lms);
                output.Add(new TimelineItem(e.Id, e.Kind, e.Name, e.Subject, e.Time, LmsLabel(e), e.Url, SourceIds.Lms, Done: e.Done, Course: e.Course, Opens: opens, Due: due,
                    State: e.Kind == "assign" ? SubmissionStates.Parse(e.State) : SubmissionState.Unknown));
                seen.Add(e.Name + "|" + e.Time);
            }
            foreach (var q in lms.Quizzes)
            {
                var odd = QuizAnomaly(q);
                string? warn = odd is { } o ? L.F("timeline.quizWarn", o.Text, Format.Local(q.Open ?? q.Close ?? 0)) : null;
                if (odd is { } a && q.Attempts.Count == 0)
                    output.Add(new TimelineItem($"qz{q.Id}named", "quiz", q.Name, q.Subject, a.Named, L.T("timeline.byName"), q.Url, SourceIds.Lms, Warn: warn, Course: q.Course));
                foreach (var (t, label, close) in new[] { (q.Open, L.T("timeline.quizOpen"), false), (q.Close, L.T("timeline.quizClose"), true) })
                    if (t is { } time && !seen.Contains(q.Name + "|" + time))
                        output.Add(new TimelineItem($"qz{q.Id}{(close ? "close" : "open")}", "quiz", q.Name, q.Subject, time, label, q.Url, SourceIds.Lms,
                            Done: close && q.Attempts.Count > 0, Warn: warn, Course: q.Course, Opens: !close, Due: close ? null : q.Close));
            }
        }
        if (Mybk is { } M)
        {
            foreach (var e in M.Exams)
            {
                var type = e.Type == "GK" ? L.T("timeline.examMid") : e.Type == "CK" ? L.T("timeline.examFinal") : "";
                var title = type.Length > 0 ? L.F("timeline.examTyped", type, e.Name) : L.F("timeline.exam", e.Name);
                var day = VnTime.ParseDate(e.Date);
                var clock = VnTime.ParseClock(e.Time);
                var time = day is { } d0 ? VnTime.FromWall(d0.AddMinutes(clock ?? 0)) : TimelineItem.UndatedTime;
                var raw = day is null ? $"{e.Date} {e.Time}".Trim() is { Length: > 0 } x ? x : L.T("timeline.undated")
                    : clock is null ? $"{Format.DateLong(day.Value)} {e.Time}".Trim() : null;
                output.Add(new TimelineItem($"mx-{e.Code}{e.Type}", "exam", Regex.Replace(title, @"\s+", " ").Trim(), e.Name,
                    time, e.Minutes is { } m ? L.F("timeline.roomMinutes", e.Room, m) : L.F("timeline.room", e.Room), null, SourceIds.Mybk, RawWhen: raw));
            }
            var today = VnTime.Today;
            foreach (var c in M.Schedule)
            {
                var clock = VnTime.ParseClock(c.Start);
                foreach (var d in Ics.ClassDates(c.Year, c.Weeks ?? [], c.Day, today))
                    output.Add(new TimelineItem($"cl-{c.Code}-{d:yyyyMMdd}-{c.Start}", "class", c.Name, c.Name, VnTime.FromWall(d.AddMinutes(clock ?? 0)),
                        L.F("timeline.classRoom", c.Room, c.Start, c.End), null, SourceIds.Mybk, Done: true, RawWhen: clock is null ? $"{Format.DateLong(d)} {c.Start}".Trim() : null));
            }
            var url = Config.Str("sources.mybk.registration");
            foreach (var r in M.Registration ?? [])
            {
                output.Add(new TimelineItem("rg-o-" + r.Code, "reg", r.Name, "", r.Start, L.F("timeline.regOpen", r.Code), url, SourceIds.Mybk));
                output.Add(new TimelineItem("rg-c-" + r.Code, "reg", r.Name, "", r.End, L.F("timeline.regClose", r.Code), url, SourceIds.Mybk));
            }
        }
        AddCustomEvents(output);
        var codesOf = GroupFilter.Matcher(Settings.Lms.GroupPattern);
        return [.. output.Select(i => i with { ClassGroup = string.Join(", ", codesOf(i.Name)) }).OrderBy(i => i.Time)];
    }

    public void CustomEventsChanged()
    {
        Timeline = BuildTimeline();
        Changed?.Invoke();
    }

    public List<(string Code, string Name)> Courses() =>
        [.. (Mybk?.Schedule ?? []).Select(c => (c.Code, c.Name)).Where(c => c.Code.Length > 0 || c.Name.Length > 0).Distinct()];

    public static string CustomLabel(CustomEvent e) => string.Join(", ", new[] { e.Location, CustomTime(e) }.Where(x => x.Length > 0));

    public static string CustomTime(CustomEvent e) => e.End.Length > 0 ? $"{e.Start}-{e.End}" : e.Start;

    private void AddCustomEvents(List<TimelineItem> output)
    {
        var courses = Courses();
        foreach (var e in CustomEventsData.Store.All())
        {
            if (CustomEvents.StartTime(e) is not { } time) continue;
            var subject = e.IsMakeup && CustomEvents.MatchCourse(e.Title, courses) is { } c ? c.Name : "";
            output.Add(new TimelineItem(TimelineItem.CustomPrefix + e.Id, e.IsMakeup ? "makeup" : "custom", e.Title, subject, time, CustomLabel(e), null, "custom"));
        }
    }

    public IEnumerable<TimelineItem> Upcoming(double hours) => Timeline.Where(e => e.Time >= Format.Now && e.Time <= Format.Now + hours * 3600);
}
