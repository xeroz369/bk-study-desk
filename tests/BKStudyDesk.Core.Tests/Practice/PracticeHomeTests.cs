using SoHocTap.Presentation.Practice;
using SoHocTap.Presentation.Practice.Pages;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests.Practice;

public class PracticeHomeTests
{
    private static readonly TimeZoneInfo Hcm = TimeZoneInfo.CreateCustomTimeZone("utc+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");

    private static (StudyRegistry, PracticeProgress) Make()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero), Hcm);
        var study = new StudyRegistry(clock: clock);
        study.Manifest.Priority = ["ppt"];
        study.Manifest.Courses.Add(new Course
        {
            Id = "ppt", Name = "Phương pháp tính", Code = "MT1009", Exam = new ExamDate("2026-10-15", "9g00"),
            Units =
            [
                new Unit { Title = "C1", Lessons = [new LessonEntry("b1", "Bài 1"), new LessonEntry("b2", "Bài 2")] },
                new Unit { Title = "C2", Lessons = [new LessonEntry("b3", "Bài 3"), new LessonEntry("b4", "Chưa soạn")] },
            ],
        });
        foreach (var id in new[] { "b1", "b2", "b3" })
            study.AddLesson(new Lesson { Id = id, Title = id, Questions = [.. Enumerable.Range(1, 4).Select(i => new Question { Prompt = $"{id} câu {i}", Options = ["a", "b"] })] });
        return (study, new PracticeProgress(study, new MemoryStorage(), clock));
    }

    [Fact]
    public void Today_counts_due_review_and_next_lesson()
    {
        var (study, p) = Make();
        p.RecordAnswer("b1.q1", Answer.Index(1), false);   // sai lần đầu: cần ôn, tới hạn mai
        foreach (var i in Enumerable.Range(1, 4)) p.RecordAnswer($"b1.q{i}", Answer.Index(0), true);
        var m = PracticeHome.Build(study, p, [], []);
        Assert.Equal(1, m.Today.Review);
        Assert.Equal(0, m.Today.Due);
        Assert.Equal("b2", m.Today.Next!.Id);
    }

    [Fact]
    public void Courses_show_progress_and_actions()
    {
        var (study, p) = Make();
        foreach (var i in Enumerable.Range(1, 4)) p.RecordAnswer($"b1.q{i}", Answer.Index(0), true);
        var c = Assert.Single(PracticeHome.Build(study, p, [], []).Courses);
        Assert.Equal(("ppt", 1, 4, 12), (c.Id, c.Done, c.Total, c.Questions));
        Assert.True(c.CanRandomExam);
        Assert.True(c.CanMix);
        Assert.Equal("15/10/2026 9g00", c.Exam);
        Assert.Equal(["C1", "C2"], c.Units.Select(u => u.Title));
        Assert.Equal([LessonStatus.Done, LessonStatus.NotStarted], c.Units[0].Lessons.Select(l => l.Status));
        Assert.Equal(LessonStatus.NotWritten, c.Units[1].Lessons[1].Status);
        Assert.Equal("4 câu", c.Units[0].Lessons[1].Sub);
    }

    [Fact]
    public void Results_newest_first_with_score()
    {
        var (study, p) = Make();
        p.AddExamAttempt(new ExamAttempt { ExamId = "x", Title = "Đề A", StartedAt = "2026-10-01T02:00:00.000Z", Right = 15, Wrong = 2, Blank = 0, Score = 9.375, Max = 10 });
        p.AddExamAttempt(new ExamAttempt { ExamId = "y", StartedAt = "2026-10-05T02:00:00.000Z", Right = 1, Score = 0.5, Max = 10 });
        var r = PracticeHome.Build(study, p, [], []).Results;
        Assert.Equal(["y", "Đề A"], r.Select(x => x.Title));
        Assert.Equal("9,38/10", r[1].Score);
        Assert.Equal("01/10/2026", r[1].When);
    }

    [Fact]
    public void Quizzes_list_packs_broken_and_lms()
    {
        var (study, p) = Make();
        var ok = StudyPack.Parse("""{"format":"studypack/1","id":"goi-a","title":"Gói A","version":"1.0.0","course":{"code":"MT1009","name":"PPT"},"authors":[{"name":"An"}],"units":[]}""")!;
        List<InstalledPack> packs = [new("goi-a.json", ok, null, 3), new("hong.json", null, "không đọc được file", 0)];
        var info = new QuizInfo(new SavedQuiz("1-1", "Quiz 1", "Giải tích 2", "MT1005", null, 1, null, null, null, null, null, null, []), "quiz-lms-mt1005.quiz-1-1", 5, 1, false);
        var m = PracticeHome.Build(study, p, packs, [info]);
        var q = m.Quizzes;
        Assert.Equal(["Gói A", "hong.json"], q.Select(x => x.Title));
        Assert.False(q[0].Broken);
        Assert.True(q[1].Broken);
        Assert.Contains("không đọc được", q[1].Sub);
        var g = Assert.Single(m.Archive);
        Assert.Equal("Giải tích 2", g.Subject);
        Assert.Equal("quiz-lms-mt1005.quiz-1-1", Assert.Single(g.Rows).LessonId);
    }

    private static SavedQuiz Saved(string file, string subject, long? finished = null, bool? answers = null, bool? noReview = null, long? closes = null, string? grade = null) =>
        new(file, "Quiz " + file, subject, "MT1005", null, 1, finished, grade is null ? null : System.Text.Json.Nodes.JsonValue.Create(grade), closes, null, answers, noReview, []);

    [Fact]
    public void Archive_groups_by_subject_newest_first_with_status()
    {
        var (study, p) = Make();
        // 01/10/2026 và 05/10/2026 giờ Việt Nam.
        long d1 = new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), d5 = d1 + 4 * 86400;
        List<QuizInfo> infos =
        [
            new(Saved("a", "GT2", d1, grade: "9,00"), "les-a", 10, 0, true),
            new(Saved("b", "GT2", d5, answers: false, closes: d5 + 86400), "les-b", 0, 0, false),
            new(Saved("c", "KTS", d5, noReview: true), "", 0, 0, false),
            new(Saved("d", "KTS", d5), "les-d", 8, 2, false),
        ];
        var a = PracticeHome.Build(study, p, [], infos).Archive;
        Assert.Equal(["KTS", "GT2"], a.Select(x => x.Subject));
        Assert.Equal(["Quiz d", "Quiz c"], a[0].Rows.Select(r => r.Title));
        Assert.Equal((L.F("practice.archive.unknown", 2), "soon"), (a[0].Rows[0].Status, a[0].Rows[0].Tone));
        Assert.Equal((L.T("practice.archive.noReview"), ""), (a[0].Rows[1].Status, a[0].Rows[1].Tone));
        var gt2 = a[1].Rows;
        Assert.Equal(["Quiz b", "Quiz a"], gt2.Select(r => r.Title));
        Assert.Equal(L.T("practice.archive.noKey"), gt2[0].Status);
        Assert.Contains(L.F("practice.archive.keysAfter", "6/10/2026"), gt2[0].Sub);
        Assert.Contains(L.F("practice.archive.shareAfter", "6/10/2026"), gt2[0].Sub);
        Assert.Equal(string.Join(", ", L.F("quiz.doneOn", "1/10/2026"), L.F("practice.archive.grade", "9,00"), L.F("practice.archive.usable", 10)), gt2[1].Sub);
        Assert.Equal((L.T("practice.archive.ok"), "ok"), (gt2[1].Status, gt2[1].Tone));
        Assert.Equal(L.F("practice.archive.meta", 2), a[1].Meta);
    }

    [Fact]
    public void Flagged_counts_each_fingerprint_once()
    {
        var (study, p) = Make();
        var q1 = study.Question("b1.q1")!;
        p.SetNote(q1.Fp, flag: true);
        p.SetNote(study.Question("b2.q2")!.Fp, text: "nhớ đổi dấu");
        p.SetNote(study.Question("b3.q1")!.Fp, text: " ");   // ghi chú đã xóa: không tính
        p.SetNote("khong-co-cau", flag: true);                // câu không còn trong sổ: không tính
        Assert.Equal(2, PracticeHome.Build(study, p, [], []).Flagged);
    }

    [Fact]
    public void Progress_rows_per_course()
    {
        var (study, p) = Make();
        var none = PracticeHome.Build(study, p, [], []).Progress.Single();
        Assert.Equal(("Phương pháp tính", "0/4", "0"), (none.Name, none.Done, none.Answered));
        Assert.Equal(L.T("practice.progress.none"), none.FirstOk);
        p.RecordAnswer("b1.q1", Answer.Index(0), true);
        p.RecordAnswer("b1.q2", Answer.Index(1), false);
        p.RecordAnswer("b1.q3", Answer.Index(0), true);
        Assert.Equal("67%", PracticeHome.Build(study, p, [], []).Progress[0].FirstOk);
    }

    [Fact]
    public void Fresh_puts_unscheduled_and_due_first()
    {
        var (study, p) = Make();
        var fresh = PracticeActions.Fresh(p);
        var q1 = study.Question("b1.q1")!;
        var q2 = study.Question("b1.q2")!;
        Assert.Equal(0, fresh(q1));
        p.RecordAnswer(q2.Id, Answer.Index(0), true);   // đúng: hẹn ôn 3 ngày sau
        Assert.Equal(1, fresh(q2));
        p.RecordAnswer(q1.Id, Answer.Index(1), false);  // sai: mai ôn
        Assert.Equal(1, fresh(q1));
    }

    [Fact]
    public void Empty_registry_is_empty_model()
    {
        var m = PracticeHome.Build(new StudyRegistry(), new PracticeProgress(new StudyRegistry(), new MemoryStorage()), [], []);
        Assert.Empty(m.Courses);
        Assert.Null(m.Today.Next);
        Assert.Equal((0, 0), (m.Today.Due, m.Today.Review));
    }
}
