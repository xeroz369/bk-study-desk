using System.Globalization;
using System.Text.RegularExpressions;
using SoHocTap.Ui;

namespace SoHocTap.Presentation.Practice;

public static partial class PracticeActions
{
    public static Func<Question, double> Fresh(PracticeProgress progress) => q =>
    {
        var r = q.Fp is { } fp ? progress.State.Srs.GetValueOrDefault(fp) : null;
        return r is null || string.CompareOrdinal(r.Due, progress.Today()) <= 0 ? 0 : 1;
    };

    public static SlideRef? Slide(StudyRegistry study, Question q)
    {
        if (q.LessonId is not { } lid || study.Entry(lid) is not { } entry) return null;
        var sources = (study.Lessons.TryGetValue(lid, out var l) ? l.Sources : null) ?? entry.Sources ?? [];
        if (sources.FirstOrDefault(s => s.Pages is { Length: > 0 } p && char.IsAsciiDigit(p[0])) is not { } src) return null;
        var tag = TagPage().Match(q.Tag ?? "");
        var page = tag.Success ? int.Parse(tag.Groups[1].Value, CultureInfo.InvariantCulture)
            : LeadingNumber().Match(src.Pages!) is { Success: true } m ? int.Parse(m.Value, CultureInfo.InvariantCulture) : 0;
        return new SlideRef(entry.CourseName, src.File, page);
    }

    public static string Where(StudyRegistry study, Question q)
    {
        if (q.LessonId is { } lid && study.Entry(lid) is { } e) return L.F("practice.run.where", e.CourseName, e.Title);
        if (q.ExamId is { } xid && study.Exams.TryGetValue(xid, out var x)) return L.F("practice.run.where", study.Course(x.CourseId)?.Name ?? "", x.Title);
        return "";
    }

    public static (Entry? Prev, Entry? Next) Neighbors(StudyRegistry study, string lessonId)
    {
        var list = study.Entries();
        var i = list.FindIndex(e => e.Id == lessonId);
        return i < 0 ? (null, null) : (i > 0 ? list[i - 1] : null, i < list.Count - 1 ? list[i + 1] : null);
    }

    public static string ReportText(StudyRegistry study, PracticeProgress progress, IEnumerable<Question> questions) =>
        string.Join("\n\n", questions.Select(q =>
        {
            var n = progress.Note(q.Fp);
            var where = Where(study, q) + (q.PackId is { } pk ? L.F("practice.report.pack", pk) : "");
            var head = new List<string> { $"<!-- {where} -->" };
            if (n?.Flag == true) head.Add($"<!-- {L.T("practice.report.flag")} -->");
            if (!string.IsNullOrWhiteSpace(n?.Text)) head.Add($"<!-- {L.F("practice.report.note", n.Text.Replace("-->", "- ->", StringComparison.Ordinal))} -->");
            return string.Join('\n', [.. head, Transfer.QuestionMarkdown(q)]);
        }));

    [GeneratedRegex(@"tr\.?\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TagPage();

    [GeneratedRegex(@"^\d+")]
    private static partial Regex LeadingNumber();

    public static List<Question> Noted(StudyRegistry study, PracticeProgress progress)
    {
        var active = progress.ActiveNotes().Select(kv => kv.Key).ToHashSet(StringComparer.Ordinal);
        var byFp = new Dictionary<string, Question>(StringComparer.Ordinal);
        foreach (var q in study.LessonQuestions().Concat(study.Exams.Values.SelectMany(x => x.Questions ?? [])))
            if (q.Fp is { } fp && active.Contains(fp)) byFp.TryAdd(fp, q);
        return [.. byFp.Values
            .OrderByDescending(q => progress.Note(q.Fp)?.Flag == true)
            .ThenByDescending(q => progress.Note(q.Fp)?.At ?? "", StringComparer.Ordinal)];
    }
}

public sealed record SlideRef(string Subject, string File, int Page);
