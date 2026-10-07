using System.Globalization;
using SoHocTap.Ui;

namespace SoHocTap.Presentation.Practice.Pages;

/// <summary>Một gói trong content/packs: nạp được (Pack) hoặc lỗi (Error).</summary>
public sealed record InstalledPack(string File, StudyPack? Pack, string? Error, int Questions);

/// <param name="Due">Câu tới hạn ôn hôm nay (lịch ôn lặp lại).</param>
/// <param name="Review">Câu sai lần đầu, chưa làm lại đúng.</param>
/// <param name="Next">Bài kế tiếp theo thứ tự ưu tiên môn.</param>
public sealed record PracticeToday(int Due, int Review, Entry? Next);

public sealed record PracticeLessonRow(string Id, string Title, string Sub, LessonStatus Status, string StatusText);

public sealed record PracticeUnitRow(string Title, List<PracticeLessonRow> Lessons);

public sealed record PracticeCourseRow(string Id, string Name, string? Code, int Done, int Total, int Questions, bool CanRandomExam, bool CanMix, string? Exam, string Sub,
    string ExamText,
    List<PracticeUnitRow> Units);

/// <param name="LessonId">Bài trong sổ để mở (quiz LMS), rỗng với gói.</param>
public sealed record PracticeQuizRow(string Title, string Sub, bool Broken, string LessonId, string? File);

public sealed record PracticeResultRow(string ExamId, string Title, string When, string Score, string Sub);

/// <param name="Tone">Màu chữ trạng thái: ok, soon (cảnh báo) hay rỗng.</param>
/// <param name="LessonId">Bài để Ôn lại; rỗng thì chỉ Ghi lại câu còn nhớ.</param>
public sealed record PracticeArchiveRow(string Title, string Sub, string Status, string Tone, string LessonId, string? Code, string Quiz);

/// <summary>Kho quiz LMS: mỗi môn một nhóm, quiz mới trước.</summary>
public sealed record PracticeArchiveGroup(string Subject, string Meta, List<PracticeArchiveRow> Rows);

/// <summary>Một dòng của thẻ Tiến độ từng môn (tab Kết quả), chữ đã định dạng.</summary>
public sealed record PracticeCourseProgress(string Id, string Name, string Done, string Answered, string FirstOk);

/// <param name="Flagged">Số câu có ghi chú hay cờ nghi sai (dòng Đánh dấu).</param>
public sealed record PracticeHomeModel(PracticeToday Today, List<PracticeCourseRow> Courses, List<PracticeQuizRow> Quizzes, List<PracticeResultRow> Results,
    int Flagged, List<PracticeCourseProgress> Progress, List<PageEntry> Pages, List<PracticeArchiveGroup> Archive);

/// <summary>Trang chính Luyện tập: tab Hôm nay, Môn học, Quiz của bạn, Kết quả. Chỉ dựng dữ liệu để hiện, không đụng control.</summary>
public static class PracticeHome
{
    public static PracticeHomeModel Build(StudyRegistry study, PracticeProgress progress, IReadOnlyList<InstalledPack> packs, IReadOnlyList<QuizInfo> quizzes)
    {
        var today = new PracticeToday(progress.DueQuestions(limit: int.MaxValue).Count, progress.ReviewCount(), progress.NextLesson());
        var courses = study.Manifest.Courses.Select(c =>
        {
            var stats = progress.CourseStats(c.Id);
            var units = study.UnitQuestions(c);
            var questions = study.CourseQuestionCount(c);
            var exam = c.Exam is { } e ? $"{DateText(e.Date)} {e.Time}".Trim() : null;
            return new PracticeCourseRow(c.Id, c.Name, c.Code, stats.Done, stats.Total, questions, study.CanRandomExam(c), Interleave.CanMix(units),
                exam,
                L.F("practice.course.sub", stats.Done, stats.Total, questions),
                exam is null ? L.T("practice.exams.noDate") : L.F("practice.exams.examSub", exam),
                c.Units.Select(u => new PracticeUnitRow(u.Title, u.Lessons.Select(e =>
                {
                    var status = progress.Status(e.Id);
                    var n = study.Lessons.TryGetValue(e.Id, out var l) ? l.Questions?.Count ?? 0 : 0;
                    var sub = status == LessonStatus.NotWritten ? L.T("practice.lesson.notWritten") : L.F("practice.lesson.sub", n);
                    return new PracticeLessonRow(e.Id, e.Title, sub, status, status.Label());
                }).ToList())).ToList());
        }).ToList();
        var quizRows = packs.Select(p => p.Pack is { } pk
                ? new PracticeQuizRow(pk.Title, L.F("practice.quiz.packSub", p.Questions, string.Join(", ", pk.Authors.Select(a => a.Name))), false, "", p.File)
                : new PracticeQuizRow(p.File, L.F("practice.quiz.brokenSub", p.Error ?? ""), true, "", p.File))
            .ToList();
        var results = progress.State.Exams
            .OrderByDescending(x => x.StartedAt, StringComparer.Ordinal)
            .Select(x => new PracticeResultRow(
                x.ExamId,
                x.Title ?? (study.Exams.TryGetValue(x.ExamId, out var ex) ? ex.Title : x.ExamId),
                When(x.StartedAt, progress.Zone),
                L.F("practice.result.score", x.Score.ToString("0.##", L.Culture), x.Max.ToString("0.##", L.Culture)),
                L.F("practice.result.sub", x.Right, x.Wrong, x.Blank)))
            .ToList();
        var rows = study.Manifest.Courses.Select(c =>
        {
            var s = progress.CourseStats(c.Id);
            return new PracticeCourseProgress(c.Id, c.Name, $"{s.Done}/{s.Total}", s.Answered.ToString(L.Culture),
                s.Answered > 0 ? $"{Math.Round(100.0 * s.FirstOk / s.Answered, MidpointRounding.AwayFromZero).ToString(L.Culture)}%" : L.T("practice.progress.none"));
        }).ToList();
        return new PracticeHomeModel(today, courses, quizRows, results, PracticeActions.Noted(study, progress).Count, rows,
            [.. (study.Manifest.Pages ?? []).Where(p => study.Pages.ContainsKey(p.Id))], Archive(quizzes, progress.Zone));
    }

    /// <summary>Kho quiz LMS (port QuizArchive.svelte): nhóm theo môn, quiz làm gần nhất trước; trạng thái đáp án, ngày, điểm, khi nào chia sẻ được.</summary>
    private static List<PracticeArchiveGroup> Archive(IReadOnlyList<QuizInfo> quizzes, TimeZoneInfo zone)
    {
        string Day(long? t) => t is { } s
            ? TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(s), zone).ToString(L.T("quiz.dateFormat"), CultureInfo.InvariantCulture)
            : "";
        PracticeArchiveRow Row(QuizInfo i)
        {
            var q = i.Quiz;
            var (status, tone) = q.NoReview == true ? (L.T("practice.archive.noReview"), "")
                : i.Usable == 0 ? (L.T("practice.archive.noKey"), "soon")
                : i.Unknown > 0 ? (L.F("practice.archive.unknown", i.Unknown), "soon")
                : (L.T("practice.archive.ok"), "ok");
            var sub = new[]
            {
                q.Finished is { } f ? L.F("quiz.doneOn", Day(f)) : "",
                q.Grade is { } g ? L.F("practice.archive.grade", g.ToString()) : "",
                i.Usable > 0 ? L.F("practice.archive.usable", i.Usable) : "",
                q.Answers == false && q.NoReview != true && q.ClosesAt is { } c ? L.F("practice.archive.keysAfter", Day(c)) : "",
                i.LessonId.Length > 0 && !i.Shareable ? (q.ClosesAt is { } c2 ? L.F("practice.archive.shareAfter", Day(c2)) : L.T("practice.archive.shareClosed")) : "",
            };
            return new PracticeArchiveRow(q.Quiz, string.Join(", ", sub.Where(s => s.Length > 0)), status, tone, i.LessonId, q.Code, q.Quiz);
        }
        return [.. quizzes.Reverse()
            .OrderByDescending(i => i.Quiz.Finished ?? 0)
            .GroupBy(i => i.Quiz.Subject)
            .Select(g => new PracticeArchiveGroup(g.Key, L.F("practice.archive.meta", g.Count()), [.. g.Select(Row)]))];
    }

    private static string DateText(string iso) =>
        DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : iso;

    private static string When(string iso, TimeZoneInfo zone) =>
        DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t)
            ? TimeZoneInfo.ConvertTime(t, zone).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : iso;
}
