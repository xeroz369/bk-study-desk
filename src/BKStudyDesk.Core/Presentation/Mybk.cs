using System.Globalization;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>Một ô số liệu đầu trang MyBK (GPA, tín chỉ...). Tip: dòng phụ (kỳ, số tín chỉ còn thiếu).</summary>
public sealed record MybkStat(string Label, string Value, string? Tip);

/// <summary>Một môn trong bảng điểm MyBK; Group là tên kỳ (nhóm của DataGrid), Fail tô màu Danger.</summary>
public sealed record MybkGradeRow(string Group, string TermSort, string Code, string Name, string Parts, string Credits, string Score, double SortScore,
    string Letter, string Result, bool Fail);

/// <summary>Một môn của chương trình đào tạo; Group là tiêu đề khối; Dim: chưa học hay tự chọn (View làm nhạt).</summary>
public sealed record ProgramRow(string Group, string Code, string Name, string Credits, string Score, string Letter, string Status, bool Dim);

public sealed record RegisteredRow(string Code, string Name, string ClassGroup, string Round, string Result);

/// <summary>Một buổi dạy trong tuần, nhóm theo giảng viên.</summary>
public sealed record TeacherRow(string Teacher, string Name, string Code, string Group, string Day, int DaySort, string Time, string Room);

public sealed record FeeRow(string Content, string Remaining, long RemainingValue, string Due);

public sealed record ActivityRow(string Name, string Date, string Days);

public sealed record DecisionRow(string Date, string Kind, string Reason, string Term, string Status);

/// <summary>Một dịch vụ của trường (sources.mybk.services trong config); Site là tên hệ thống, không phải đường dẫn.</summary>
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

/// <summary>Một bộ lọc của chương trình đào tạo (rỗng là tất cả).</summary>
public sealed record ProgramFilter(string Label, CourseStatus[] Match)
{
    public override string ToString() => Label;
}

/// <summary>
/// Trang MyBK gom mọi mục học vụ một chỗ (cùng quy tắc với bản 1.x, Ui/Pages/GradesPage và ServicesPage): số liệu, bảng điểm theo kỳ,
/// chương trình đào tạo, đăng ký môn, giảng viên, học phí, công tác xã hội, quyết định, dịch vụ của trường.
/// </summary>
public static class MybkPresenter
{
    public static IReadOnlyList<ProgramFilter> Filters { get; } =
    [
        new(L.T("grades.filter.all"), []), new(L.T("grades.filter.retake"), [CourseStatus.HocLai, CourseStatus.Rut]),
        new(L.T("grades.filter.studying"), [CourseStatus.DangHoc]), new(L.T("grades.filter.notTaken"), [CourseStatus.ChuaHoc]),
        new(L.T("grades.filter.passed"), [CourseStatus.Dat, CourseStatus.Mien]),
    ];

    /// <summary>Dòng dưới tiêu đề trang: MSSV, lớp, học kỳ.</summary>
    public static string Subtitle(MybkData? m) => m is null ? L.T("grades.noData") : string.Join(", ", new[] { m.Student.Mssv, m.Student.Class ?? "", m.Term.Name }.Where(x => x.Length > 0));

    public static IReadOnlyList<MybkStat> Stats(MybkData? m)
    {
        var view = m is null ? null : CurriculumView.From(m);
        var real = (m?.GradeTerms ?? []).Where(t => t.Code != "BL").ToList();
        var latest = real.FirstOrDefault();
        var latestCredits = real.FirstOrDefault(t => double.TryParse(t.CreditsTerm.Text(), out var c) && c > 0);
        // MyBK trả GPA dạng chuỗi kiểu Mỹ ("8.12"): đọc ra số rồi in theo ngôn ngữ đang dùng.
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

    /// <summary>Bảng điểm: kỳ mới nhất trước, nhóm chuyển điểm, miễn (BL) cuối; ghép điểm thành phần chính thức.</summary>
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

    /// <summary>Chương trình đào tạo theo khối, lọc theo trạng thái (Filters). Null khi MyBK chưa có chương trình.</summary>
    public static IReadOnlyList<ProgramRow>? Program(MybkData? m, ProgramFilter filter)
    {
        if (m is null || CurriculumView.From(m) is not { } view) return null;
        return [.. view.Blocks.SelectMany(b => b.Courses.Where(c => filter.Match.Length == 0 || filter.Match.Contains(c.Status))
            .Select(c => new ProgramRow(b.Title, c.Code, c.Name, c.Credits is { } cr ? Format.Score(cr) : "", c.Score, c.Letter, c.StatusText,
                c.Status is CourseStatus.ChuaHoc or CourseStatus.LuaChon)))];
    }

    /// <summary>Ghi chú khi chương trình đào tạo trên MyBK cũ hơn bảng điểm.</summary>
    public static string ProgramNote(MybkData? m) => m is not null && CurriculumView.From(m) is { Stale: true } ? L.F("grades.stale", m.Curriculum?.Updated) : "";

    public static IReadOnlyList<RegisteredRow> Registered(MybkData? m) =>
        [.. (m?.Registered ?? []).Select(r => new RegisteredRow(r.Code, r.Name, r.ClassGroup, r.Round, r.Result))];

    /// <summary>
    /// Mỗi buổi trong thời khóa biểu của kỳ là một dòng, nhóm theo giảng viên (thứ trong tuần tăng dần). Nhóm lớp lấy từ kết quả
    /// đăng ký nếu có. Buổi không có giờ cố định ghi riêng.
    /// </summary>
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
        [.. (m?.Fees ?? []).Select(f => new FeeRow(f.Content, Format.Money(f.Remaining), f.Remaining, f.Due))];

    public static IReadOnlyList<ActivityRow> Social(MybkData? m) =>
        [.. (m?.SocialWork?.Activities ?? []).Select(a => new ActivityRow(a.Name, a.DateText, a.DaysText))];

    public static IReadOnlyList<DecisionRow> Decisions(MybkData? m) =>
        [.. (m?.Decisions ?? []).Select(d => new DecisionRow(d.Date ?? "", d.Type ?? "", d.Reason ?? "", d.Term ?? "", d.Status ?? ""))];

    /// <summary>Dịch vụ của trường, lọc theo tên hay nhóm (không phân biệt hoa thường).</summary>
    public static IReadOnlyList<ServiceRow> Services(string q)
    {
        var all = (Config.Node("sources.mybk.services") as JsonArray ?? []).OfType<JsonObject>()
            .Select(o => new ServiceRow(o["group"]?.GetValue<string>() ?? L.T("services.other"), o["name"]?.GetValue<string>() ?? "", o["url"]?.GetValue<string>() ?? ""))
            .Where(s => s.Url.Length > 0);
        q = q.Trim();
        return [.. q.Length == 0 ? all : all.Where(s => s.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || s.Group.Contains(q, StringComparison.CurrentCultureIgnoreCase))];
    }
}
