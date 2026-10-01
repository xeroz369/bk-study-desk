using System.Globalization;
using System.Text.RegularExpressions;

namespace SoHocTap.Ui;

/// <summary>Trạng thái một môn trong chương trình đào tạo (quy tắc cố vấn học vụ, DESIGN.md mục 5).</summary>
public enum CourseStatus { DangHoc, Dat, Mien, Rut, HocLai, ChuaHoc, LuaChon }

public sealed record CourseView(MybkCurriculumCourse Course, CourseStatus Status, int Fails, MybkGrade? FromGrades)
{
    public static string Text(CourseStatus s) => L.T(s switch
    {
        CourseStatus.DangHoc => "curriculum.studying",
        CourseStatus.Dat => "curriculum.passed",
        CourseStatus.Mien => "curriculum.exempt",
        CourseStatus.Rut => "curriculum.withdrawn",
        CourseStatus.HocLai => "curriculum.retake",
        CourseStatus.ChuaHoc => "curriculum.notTaken",
        _ => "curriculum.elective",
    });
    public string StatusText => Text(Status) + (FromGrades is null ? "" : " " + L.T("curriculum.fromGrades"))
                                + (Fails > 0 && Status is not (CourseStatus.Dat or CourseStatus.Mien) ? " · " + L.F("curriculum.fails", Fails) : "");
    public string Code => Course.Code;
    public string Name => Course.Name + (string.IsNullOrWhiteSpace(Course.Equiv) ? "" : $" ({Course.Equiv.Trim()})");
    public double? Credits => Course.Credits;
    public string Score => FromGrades?.Score is { } g ? Format.Score(g) : Course.Score is { } s ? Format.Score(s) : Course.Special ?? "";
    public string Letter => FromGrades?.Letter ?? Course.Letter ?? "";
}

public sealed record BlockView(MybkBlock Block, List<CourseView> Courses)
{
    public string Title => $"{Block.Name} · {L.T(Block.Required ? "curriculum.required" : "curriculum.optional")}" +
                           (Block.CreditsNeed > 0 ? " · " + L.F("curriculum.credits", Block.CreditsDone, Block.CreditsNeed) : Block.Complete ? " · " + L.T("curriculum.complete") : "");
}

public sealed record CurriculumView(List<BlockView> Blocks, List<CourseView> Courses, int Retake, int Studying, double? Missing, bool Stale)
{
    public static CurriculumView? From(MybkData m)
    {
        if (m.Curriculum is not { } c) return null;
        var inSchedule = m.Schedule.Select(x => x.Code ?? "").ToHashSet();
        var byCode = m.Grades.Where(g => g.Code is not null).GroupBy(g => g.Code).ToDictionary(g => g.Key, g => g.ToList());
        var courses = c.Courses.Select(x =>
        {
            var grades = x.Code is null ? [] : byCode.GetValueOrDefault(x.Code) ?? [];
            var passed = grades.FirstOrDefault(g => g.Result == 1);
            var fails = grades.Count(g => g.Result == 0);
            var exempt = x.Special is "MT" or "DT";
            var status = inSchedule.Contains(x.Code ?? "") ? CourseStatus.DangHoc
                : x.Result == 1 || passed is not null ? (exempt ? CourseStatus.Mien : CourseStatus.Dat)
                : exempt ? CourseStatus.Mien
                : x.Special is "RT" or "VT" ? CourseStatus.Rut
                : x.Attempted ? CourseStatus.HocLai : CourseStatus.ChuaHoc;
            return new CourseView(x, status, fails, x.Result != 1 ? passed : null);
        }).ToList();

        var blocks = c.Blocks.Select(b =>
        {
            var list = courses.Where(x => x.Course.Block == b.Id).ToList();
            // Khối tự chọn đã đủ tín chỉ: môn chưa học chỉ là lựa chọn, không phải nợ.
            if (!b.Required && b.CreditsNeed > 0 && b.CreditsDone >= b.CreditsNeed)
                list = [.. list.Select(x => x.Status == CourseStatus.ChuaHoc ? x with { Status = CourseStatus.LuaChon } : x)];
            return new BlockView(b, list);
        }).OrderBy(b => b.Block.Complete).ThenBy(b => b.Block.Order).ToList();
        var all = blocks.SelectMany(b => b.Courses).ToList();

        var latestGrades = m.GradeTerms.Select(t => ParseVn(t.Updated)).Where(d => d is not null).Max();
        var updated = ParseVn(c.Updated);
        return new CurriculumView(blocks, all,
            all.Count(x => x.Status is CourseStatus.HocLai or CourseStatus.Rut), all.Count(x => x.Status == CourseStatus.DangHoc),
            c.CreditsNeed - c.CreditsDone, updated is not null && latestGrades is not null && latestGrades > updated);
    }

    private static DateTime? ParseVn(string? s)
    {
        var m = Regex.Match(s ?? "", @"(\d{2})/(\d{2})/(\d{4})");
        return m.Success ? new DateTime(int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) : null;
    }
}
