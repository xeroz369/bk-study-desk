using System.Globalization;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public sealed record MybkStat(string Label, string Value, string? Tip);

public sealed record MybkGradeRow(string Group, string TermSort, string Code, string Name, string Parts, string Credits, string Score, double SortScore,
    string Letter, string Result, bool Fail);

public sealed record ProgramRow(string Group, string Code, string Name, string Credits, string Score, string Letter, string Status, bool Dim);

public sealed record RegisteredRow(string Code, string Name, string ClassGroup, string Round, string Result);

public sealed record TeacherRow(string Teacher, string Name, string Code, string Group, string Day, int DaySort, string Time, string Room);

public sealed record FeeRow(string Content, string Amount, long AmountValue, string Remaining, long RemainingValue, string Due, string Status, bool Owed);

public sealed record ActivityRow(string Name, string Date, string Days);

public sealed record DecisionRow(string Date, string Kind, string Reason, string Term, string Status);

public sealed record ServiceRow(string Group, string Name, string Url)
{
    public string Site => Uri.TryCreate(Url, UriKind.Absolute, out var u) ? u.Host.Split('.')[0].ToLowerInvariant() switch
    {
        "mybk" => "MyBK",
        "lms" => "BK-LMS",
        "bkpay" => "BKPay",
        "account" => L.T("services.siteAccount"),
        "wiki" => "Wiki",
        var h => h,
    } : "";
}

public sealed record ProgramFilter(string Label, CourseStatus[] Match)
{
    public override string ToString() => Label;
}

public static class MybkPresenter
{
    public static IReadOnlyList<ProgramFilter> Filters { get; } =
    [
        new(L.T("grades.filter.all"), []),
        new(L.T("grades.filter.notPassed"), [CourseStatus.DangHoc, CourseStatus.Rut, CourseStatus.HocLai, CourseStatus.ChuaHoc]),
        new(L.T("grades.filter.retake"), [CourseStatus.HocLai, CourseStatus.Rut]),
        new(L.T("grades.filter.studying"), [CourseStatus.DangHoc]), new(L.T("grades.filter.notTaken"), [CourseStatus.ChuaHoc]),
        new(L.T("grades.filter.passed"), [CourseStatus.Dat, CourseStatus.Mien]),
    ];

    public static string Subtitle(MybkData? m) => m is null ? L.T("grades.noData") : string.Join(", ", new[] { m.Student.Mssv, m.Student.Class ?? "", m.Term.Name }.Where(x => x.Length > 0));

    public static IReadOnlyList<MybkStat> Stats(MybkData? m)
    {
        var view = m is null ? null : CurriculumView.From(m);
        var real = (m?.GradeTerms ?? []).Where(t => t.Code != "BL").ToList();
        var latest = real.FirstOrDefault();
        var latestCredits = real.FirstOrDefault(t => double.TryParse(t.CreditsTerm.Text(), out var c) && c > 0);
        static string Gpa(string? v) => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0 ? Format.Num(d) : "-";
        return
        [
            new(L.T("grades.gpaAll"), Gpa(latest?.GpaAll), latest?.Name),
            new(L.T("grades.credits"), m?.Curriculum is { } c ? $"{c.CreditsDone:0}/{c.CreditsNeed:0}" : "-", view?.Missing is { } miss ? L.F("grades.missing", miss) : null),
            new(L.T("grades.gpaTerm"), Gpa(latestCredits?.GpaTerm), latestCredits?.Name),
            new(L.T("grades.retake"), view?.Retake.ToString(CultureInfo.InvariantCulture) ?? "-", null),
            new(L.T("grades.social"), m?.SocialWork?.Days is { } days ? Format.Score(days) : "-", null),
        ];
    }

    public static IReadOnlyList<MybkGradeRow> Grades(MybkData? m)
    {
        if (m is null) return [];
        var termNames = m.GradeTerms.Where(t => t.Code is not null).DistinctBy(t => t.Code).ToDictionary(t => t.Code, t => t.Name);
        var comps = (m.Components ?? []).GroupBy(c => (c.TermId, c.CourseId)).ToDictionary(g => g.Key, g => g.First());
        string Parts(MybkGrade g) => g.TermId is { } t && g.CourseId is { } c && comps.TryGetValue((t, c), out var x)
            ? string.Join(", ", x.Items.Where(i => i.Weight > 0).Select(i => $"{i.Name} {i.Weight:0}%: {i.Special ?? Format.Score(i.Score)}")) : "";
        string Result(MybkGrade g) => g.Result == 1 ? L.T("grades.pass") : g.Special ?? L.T(g.Result == 0 ? "grades.fail" : "grades.notCounted");
        return [.. m.Grades.Select(g => new MybkGradeRow(termNames.GetValueOrDefault(g.Term, g.Term == "BL" ? L.T("grades.reserved") : g.Term),
                g.Term == "BL" ? "0" : g.Term, g.Code, g.Name, Parts(g), g.Credits is { } cr ? Format.Score(cr) : "", g.Special ?? Format.Score(g.Score),
                g.Score ?? -1, g.Letter ?? "", Result(g), g.Result == 0))
            .OrderByDescending(r => r.TermSort, StringComparer.Ordinal)];
    }

    public static IReadOnlyList<MybkGradeRow> SearchGrades(IReadOnlyList<MybkGradeRow> rows, string query) =>
        string.IsNullOrWhiteSpace(query) ? rows
            : [.. rows.Where(r => Files.NameMatch.ContainsFolded(r.Code, query.Trim()) || Files.NameMatch.ContainsFolded(r.Name, query.Trim()))];

    public static IReadOnlyList<ProgramRow>? Program(MybkData? m, ProgramFilter filter)
    {
        if (m is null || CurriculumView.From(m) is not { } view) return null;
        return [.. view.Blocks.SelectMany(b => b.Courses.Where(c => filter.Match.Length == 0 || filter.Match.Contains(c.Status))
            .Select(c => new ProgramRow(b.Title, c.Code, c.Name, c.Credits is { } cr ? Format.Score(cr) : "", c.Score, c.Letter, c.StatusText,
                c.Status is CourseStatus.ChuaHoc or CourseStatus.LuaChon)))];
    }

    public static string ProgramEmpty(MybkData? m) =>
        m?.Curriculum?.Missing is { Length: > 0 } why ? L.F("grades.programMissing", why) : L.T("grades.programEmpty");

    public static string ProgramNote(MybkData? m) => m is not null && CurriculumView.From(m) is { Stale: true } ? L.F("grades.stale", m.Curriculum?.Updated) : "";

    public static IReadOnlyList<RegisteredRow> Registered(MybkData? m) =>
        [.. (m?.Registered ?? []).Select(r => new RegisteredRow(r.Code, r.Name, r.ClassGroup, r.Round, r.Result))];

    public static IReadOnlyList<TeacherRow> Teachers(MybkData? m)
    {
        if (m is null) return [];
        string GroupOf(MybkClass c) => m.Registered?.FirstOrDefault(r => r.Code == c.Code)?.ClassGroup is { Length: > 0 } g ? g : c.Group ?? "";
        return [.. m.Schedule.Select(c =>
            {
                var timed = c.Day is >= 2 and <= 8;
                return new TeacherRow(string.IsNullOrWhiteSpace(c.Teacher) ? L.T("grades.noTeacher") : c.Teacher!, c.Name, c.Code, GroupOf(c),
                    timed ? Format.MybkDays.GetValueOrDefault(c.Day) ?? "" : L.T("grades.noFixedTime"), timed ? c.Day : 99, timed ? $"{c.Start}-{c.End}" : "", c.Room);
            })
            .DistinctBy(t => (t.Teacher, t.Code, t.Group, t.DaySort, t.Time, t.Room))
            .OrderBy(t => t.Teacher, StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), true)).ThenBy(t => t.DaySort)];
    }

    public static IReadOnlyList<FeeRow> Fees(MybkData? m) =>
        [.. (m?.Fees ?? []).OrderByDescending(f => f.Remaining > 0).ThenByDescending(f => f.Due, StringComparer.Ordinal)
            .Select(f => new FeeRow(f.Content, Format.Money(f.Amount), f.Amount, Format.Money(f.Remaining), f.Remaining, f.Due,
                L.T(f.Remaining > 0 ? "grades.feeOwed" : "grades.feePaid"), f.Remaining > 0))];

    public static IReadOnlyList<ActivityRow> Social(MybkData? m) =>
        [.. (m?.SocialWork?.Activities ?? []).Select(a => new ActivityRow(a.Name, a.DateText, a.DaysText))];

    public static IReadOnlyList<DecisionRow> Decisions(MybkData? m) =>
        [.. (m?.Decisions ?? []).Select(d => new DecisionRow(d.Date ?? "", d.Type ?? "", d.Reason ?? "", d.Term ?? "", d.Status ?? ""))];

    public static IReadOnlyList<ServiceRow> Services(string q)
    {
        var all = (Config.Node("sources.mybk.services") as JsonArray ?? []).OfType<JsonObject>()
            .Select(o => new ServiceRow(o["group"]?.GetValue<string>() ?? L.T("services.other"), o["name"]?.GetValue<string>() ?? "", o["url"]?.GetValue<string>() ?? ""))
            .Where(s => s.Url.Length > 0);
        q = q.Trim();
        return [.. q.Length == 0 ? all : all.Where(s => s.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || s.Group.Contains(q, StringComparison.CurrentCultureIgnoreCase))];
    }
}
