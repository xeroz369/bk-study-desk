using SoHocTap.Core;
using System.Text.Json;

namespace SoHocTap.Presentation.Practice;

public static class ContentLibrary
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { TypeInfoResolver = PracticeJson.Default };

    internal sealed record ExamEntry(string Id, string File, string CourseId, List<SourceRef>? Sources);

    internal sealed record LessonFile(string Id, string Title, string? File, List<SourceRef>? Sources);

    internal sealed record UnitFile(string Title, List<LessonFile> Lessons);

    internal sealed record CourseFile(string Id, string Name, string? Code, ExamDate? Exam, Scoring? Scoring, Blueprint? Blueprint, List<UnitFile> Units);

    internal sealed record ManifestFile(string Term, List<string>? Priority, List<PageEntry>? Pages, List<ExamEntry>? Exams, List<CourseFile> Courses);

    public static List<string> Load(string contentDir, StudyRegistry study)
    {
        var warnings = new List<string>();
        var root = Directory.GetParent(Path.GetFullPath(contentDir))!.FullName;
        string? Read(string rel)
        {
            var p = Path.Combine(root, rel);
            if (File.Exists(p)) return File.ReadAllText(p);
            warnings.Add($"thiếu file {rel}");
            return null;
        }

        ManifestFile? m;
        try { m = Js.Deserialize<ManifestFile>(Read(Path.Combine("content", "manifest.json")) ?? "null", Json); }
        catch (JsonException e)
        {
            warnings.Add($"manifest.json: {e.Message}");
            return warnings;
        }
        if (m is null) return warnings;

        study.Manifest = new Manifest
        {
            Term = m.Term,
            Priority = m.Priority,
            Pages = m.Pages,
            Courses = m.Courses.Select(c => new Course
            {
                Id = c.Id, Name = c.Name, Code = c.Code, Exam = c.Exam, Scoring = c.Scoring, Blueprint = c.Blueprint,
                Units = c.Units.Select(u => new Unit { Title = u.Title, Lessons = u.Lessons.Select(e => new LessonEntry(e.Id, e.Title, e.File, e.Sources)).ToList() }).ToList(),
            }).ToList(),
        };

        foreach (var e in m.Courses.SelectMany(c => c.Units).SelectMany(u => u.Lessons).Where(e => e.File != null))
        {
            if (Parse(Read(e.File!), e.File!, warnings) is not { } pack) continue;
            if (pack.Units.SelectMany(u => u.Lessons).FirstOrDefault() is not { } l)
            {
                warnings.Add($"{e.File}: không có bài (##)");
                continue;
            }
            if (l.Id != e.Id) warnings.Add($"{e.File}: id bài {l.Id} khác mục lục ({e.Id})");
            study.AddLesson(new Lesson
            {
                Id = e.Id,
                Title = l.Title,
                Sources = e.Sources,
                Sections = l.Sections?.Select(s => new Section(s.Title, Html.Sanitize(s.Body))).ToList(),
                Questions = Questions(l.Questions, e.Id),
            });
        }

        foreach (var x in m.Exams ?? [])
        {
            if (Parse(Read(x.File), x.File, warnings) is not { } pack) continue;
            if (pack.Exams?.FirstOrDefault() is not { } ex)
            {
                warnings.Add($"{x.File}: không có đề");
                continue;
            }
            study.AddExam(new Exam
            {
                Id = x.Id,
                CourseId = x.CourseId,
                Title = ex.Title,
                Minutes = ex.Minutes,
                Scoring = ex.Scoring,
                Sources = x.Sources,
                QuestionIds = ex.QuestionRefs,
                Questions = Questions(ex.Questions, x.Id),
            });
        }

        foreach (var p in m.Pages ?? [])
            if (Read(p.File) is { } md) study.AddPage(new Page(p.Id, Html.Sanitize(StudyMarkdown.MdToHtml(md, headings: true), headings: true)));
        return warnings;
    }

    private static StudyPack? Parse(string? md, string file, List<string> warnings)
    {
        if (md is null) return null;
        var r = StudyMarkdown.Parse(md, validate: false);
        warnings.AddRange(r.Errors.Select(e => $"{file}:{e.Line} {e.Message}"));
        return r.Pack;
    }

    private static List<Question> Questions(List<PackQuestion>? qs, string owner) =>
        (qs ?? []).Select(pq =>
        {
            var q = StudyRegistry.FromPack(pq, $"{owner}.{pq.Id}", null, s => s);
            q.Fp = Fingerprint.Of(q);
            q.Prompt = Html.Sanitize(q.Prompt);
            q.Solution = Html.Sanitize(q.Solution);
            q.Options = q.Options.Select(o => Html.Sanitize(o)).ToList();
            return q;
        }).ToList();
}
