using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

public class QuizSessionTests
{
    private static readonly TimeZoneInfo Hcm = TimeZoneInfo.CreateCustomTimeZone("utc+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");

    private static (StudyRegistry Study, PracticeProgress P) Make()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero), Hcm);
        var study = new StudyRegistry(clock: clock);
        study.Manifest.Courses.Add(new Course { Id = "ppt", Name = "PPT", Units = [new Unit { Title = "C1", Lessons = [new LessonEntry("b1", "Bài 1")] }] });
        study.AddLesson(new Lesson
        {
            Id = "b1", Title = "Bài 1",
            Questions = [.. Enumerable.Range(1, 6).Select(i => new Question { Prompt = $"Câu {i}", Options = ["a", "b", "c", "d"], Answer = 1 })],
        });
        return (study, new PracticeProgress(study, new MemoryStorage(), clock));
    }

    [Fact]
    public void Lesson_mode_records_answer_and_wrong_allows_retry()
    {
        var (study, p) = Make();
        var q = study.Question("b1.q1")!;
        var s = new QuizSession(p, RunMode.Lesson, [q]);
        Assert.False(s.Answer(q, Answer.Index(0)));
        Assert.False(s.Solved(q));
        Assert.True(s.Answer(q, Answer.Index(1)));
        Assert.True(s.Solved(q));
        Assert.Equal(2, p.Q(q.Id)!.Tries);
        Assert.False(p.Q(q.Id)!.FirstTry);
        Assert.Equal(1, s.Done);
    }

    [Fact]
    public void Blank_answer_is_ignored()
    {
        var (study, p) = Make();
        var q = study.Question("b1.q1")!;
        q.Type = "short";
        q.Accept = ["x"];
        var s = new QuizSession(p, RunMode.Lesson, [q]);
        Assert.False(s.Answer(q, Answer.Of("  ")));
        Assert.Null(p.Q(q.Id));
        Assert.Equal(0, s.Done);
    }

    [Fact]
    public void Review_mode_marks_reviewed_only_when_right()
    {
        var (study, p) = Make();
        var q = study.Question("b1.q1")!;
        p.RecordAnswer(q.Id, Answer.Index(0), false);
        var s = new QuizSession(p, RunMode.Review, [q]);
        s.Answer(q, Answer.Index(2));
        Assert.True(p.NeedsReview(q.Id));
        s.Answer(q, Answer.Index(1));
        Assert.False(p.NeedsReview(q.Id));
    }

    [Fact]
    public void Due_mode_marks_reviewed_when_right()
    {
        var (study, p) = Make();
        var q = study.Question("b1.q1")!;
        p.RecordAnswer(q.Id, Answer.Index(0), false);
        new QuizSession(p, RunMode.Due, [q]).Answer(q, Answer.Index(1));
        Assert.False(p.NeedsReview(q.Id));
    }

    [Fact]
    public void Mix_mode_does_not_mark_reviewed()
    {
        var (study, p) = Make();
        var q = study.Question("b1.q1")!;
        p.RecordAnswer(q.Id, Answer.Index(0), false);
        new QuizSession(p, RunMode.Mix, [q]).Answer(q, Answer.Index(1));
        Assert.True(p.NeedsReview(q.Id));
    }

    [Fact]
    public void Time_is_recorded_on_first_try_only()
    {
        var (study, p) = Make();
        var q = study.Question("b1.q1")!;
        var s = new QuizSession(p, RunMode.Lesson, [q]);
        s.Answer(q, Answer.Index(0), seconds: 12.4);
        s.Answer(q, Answer.Index(1), seconds: 40);
        Assert.Equal(12, p.State.Times[q.Fp!]);
    }

    [Fact]
    public void Shuffled_options_keep_original_answer_index()
    {
        var (study, p) = Make();
        var qs = study.Lessons["b1"].Questions!;
        var s = new QuizSession(p, RunMode.Lesson, qs, new ShufflePrefs(true, true), seed: 42);
        Assert.Equal(qs.Select(q => q.Id).Order(), s.Shown.Select(q => q.Id).Order());
        Assert.NotEqual(qs.Select(q => q.Id), s.Shown.Select(q => q.Id));
        var q1 = s.Shown[0];
        var order = s.Order(q1)!;
        Assert.Equal([0, 1, 2, 3], order.Order());
        // Đáp án gốc là phương án 1; hiện ở vị trí order.IndexOf(1).
        Assert.Equal("ABCD"[Array.IndexOf(order, 1)].ToString(), s.RightText(q1));
        Assert.True(s.Answer(q1, Answer.Index(1)));
        // Cùng seed thì cùng thứ tự (trang vẽ lại không nhảy).
        Assert.Equal(order, new QuizSession(p, RunMode.Lesson, qs, new ShufflePrefs(true, true), seed: 42).Order(q1));
    }

    [Fact]
    public void No_shuffle_keeps_order_and_letters()
    {
        var (study, p) = Make();
        var qs = study.Lessons["b1"].Questions!;
        var s = new QuizSession(p, RunMode.Lesson, qs);
        Assert.Equal(qs, s.Shown);
        Assert.Null(s.Order(qs[0]));
        Assert.Equal("B", s.RightText(qs[0]));
    }
}

public class PracticeActionsTests
{
    private static StudyRegistry Make(string? pages)
    {
        var study = new StudyRegistry();
        study.Manifest.Courses.Add(new Course { Id = "ppt", Name = "Phương pháp tính", Units = [new Unit { Title = "C1", Lessons = [new LessonEntry("b1", "Bài 1")] }] });
        study.AddLesson(new Lesson
        {
            Id = "b1", Title = "Bài 1", Sources = pages is null ? null : [new SourceRef("ghi-chu.pdf"), new SourceRef("slide-c1.pdf", pages)],
            Questions = [new Question { Prompt = "a", Options = ["x", "y"], Tag = "GK251 tr.12" }, new Question { Prompt = "b", Options = ["x", "y"] }],
        });
        return study;
    }

    [Fact]
    public void Slide_prefers_page_in_tag_then_first_page_of_source()
    {
        var study = Make("5-9");
        Assert.Equal(new SlideRef("Phương pháp tính", "slide-c1.pdf", 12), PracticeActions.Slide(study, study.Question("b1.q1")!));
        Assert.Equal(5, PracticeActions.Slide(study, study.Question("b1.q2")!)!.Page);
    }

    [Fact]
    public void Slide_is_null_without_numbered_source()
    {
        var study = Make(null);
        Assert.Null(PracticeActions.Slide(study, study.Question("b1.q1")!));
        Assert.Null(PracticeActions.Slide(study, new Question { Prompt = "x" }));
    }

    [Fact]
    public void Where_names_course_and_lesson()
    {
        var study = Make(null);
        Assert.Equal("Phương pháp tính, Bài 1", PracticeActions.Where(study, study.Question("b1.q1")!));
        Assert.Equal("", PracticeActions.Where(study, new Question()));
    }

    [Fact]
    public void Noted_puts_flagged_first()
    {
        var study = Make(null);
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero));
        var p = new PracticeProgress(study, new MemoryStorage(), clock);
        p.SetNote(study.Question("b1.q1")!.Fp, text: "ghi chú");
        clock.Now += TimeSpan.FromMinutes(1);
        p.SetNote(study.Question("b1.q2")!.Fp, flag: true);
        Assert.Equal(["b1.q2", "b1.q1"], PracticeActions.Noted(study, p).Select(q => q.Id));
    }
}

public class LessonNeighborsTests
{
    [Fact]
    public void Neighbors_follow_entries_across_courses()
    {
        var study = new StudyRegistry();
        study.Manifest.Courses.Add(new Course { Id = "a", Name = "A", Units = [new Unit { Title = "C1", Lessons = [new LessonEntry("a1", "A1"), new LessonEntry("a2", "A2")] }] });
        study.Manifest.Courses.Add(new Course { Id = "b", Name = "B", Units = [new Unit { Title = "C1", Lessons = [new LessonEntry("b1", "B1")] }] });
        Assert.Equal((null, "a2"), Ids(PracticeActions.Neighbors(study, "a1")));
        Assert.Equal(("a2", null), Ids(PracticeActions.Neighbors(study, "b1")));
        Assert.Equal((null, null), Ids(PracticeActions.Neighbors(study, "khong-co")));
    }

    private static (string?, string?) Ids((Entry? Prev, Entry? Next) n) => (n.Prev?.Id, n.Next?.Id);
}

public class QuizSessionTriedTests
{
    [Fact]
    public void Tried_is_null_before_answering()
    {
        var study = new StudyRegistry();
        study.AddLesson(new Lesson { Id = "b1", Title = "B", Questions = [new Question { Prompt = "x", Options = ["a", "b"], Answer = 1 }] });
        var q = study.Question("b1.q1")!;
        var s = new QuizSession(new PracticeProgress(study, new MemoryStorage()), RunMode.Lesson, [q]);
        Assert.Null(s.Tried(q));
        Assert.False(s.Solved(q));
    }
}
