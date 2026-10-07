using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

public class ExamSessionTests
{
    private static readonly TimeZoneInfo Hcm = TimeZoneInfo.CreateCustomTimeZone("utc+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");

    // Đề KTS mẫu tự dựng: 5 câu, 10 phút, đúng +0,4, sai -0,2.
    private static (StudyRegistry Study, PracticeProgress P, FixedClock Clock, Exam Exam) Make()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero), Hcm);
        var study = new StudyRegistry(clock: clock);
        study.Manifest.Courses.Add(new Course { Id = "kts", Name = "Kỹ thuật số", Scoring = new Scoring(25, 0.4, -0.2) });
        study.AddExam(new Exam
        {
            Id = "kts-de", CourseId = "kts", Title = "Đề KTS", Minutes = 10,
            Questions = [.. Enumerable.Range(1, 5).Select(i => new Question { Prompt = $"Câu {i}", Options = ["0", "1"], Answer = 1 })],
        });
        return (study, new PracticeProgress(study, new MemoryStorage(), clock), clock, study.Exams["kts-de"]);
    }

    private static ExamSession Session((StudyRegistry Study, PracticeProgress P, FixedClock Clock, Exam Exam) m) =>
        new(m.P, m.Exam, m.Study.ExamQuestions(m.Exam), ExamSession.ScoringFor(m.Study, m.Exam, 5), clock: m.Clock);

    [Fact]
    public void Scores_with_negative_marking_and_blank_zero()
    {
        var m = Make();
        var s = Session(m);
        var q = s.Shown;
        s.Pick(q[0], Answer.Index(1));
        s.Pick(q[1], Answer.Index(1));
        s.Pick(q[2], Answer.Index(0));
        var r = s.Finish();
        Assert.Equal((2, 1, 2), (r.Right, r.Wrong, r.Blank));
        Assert.Equal(0.6, r.Score, 9);
        Assert.Equal(2, r.Max, 9);
        Assert.Equal(3, r.Ten, 9);
    }

    [Fact]
    public void Finish_twice_records_one_attempt()
    {
        var m = Make();
        var s = Session(m);
        s.Pick(s.Shown[0], Answer.Index(1));
        var a = s.Finish();
        var b = s.Finish();
        Assert.Same(a, b);
        var x = Assert.Single(m.P.State.Exams);
        Assert.Equal(("kts-de", "Đề KTS", 1d), (x.ExamId, x.Title, x.Right));
        Assert.Equal("2026-10-07T03:00:00.000Z", x.StartedAt);
    }

    [Fact]
    public void Picking_again_with_null_makes_blank()
    {
        var m = Make();
        var s = Session(m);
        s.Pick(s.Shown[0], Answer.Index(1));
        s.Pick(s.Shown[0], null);
        Assert.Null(s.Picked(s.Shown[0]));
        Assert.Equal(0, s.PickedCount);
        Assert.Equal(5, s.Finish().Blank);
    }

    [Fact]
    public void Time_left_counts_from_deadline_and_expires()
    {
        var m = Make();
        var s = Session(m);
        Assert.Equal(600, s.Left);
        m.Clock.Now += TimeSpan.FromSeconds(90.4);
        Assert.Equal(510, s.Left);
        Assert.False(s.Expired);
        m.Clock.Now += TimeSpan.FromMinutes(9);
        Assert.Equal(0, s.Left);
        Assert.True(s.Expired);
    }

    [Fact]
    public void Times_add_up_between_picks_and_slow_questions_are_listed()
    {
        var m = Make();
        var s = Session(m);
        Assert.Equal(120, s.Pace, 9);
        m.Clock.Now += TimeSpan.FromSeconds(30);
        s.Pick(s.Shown[0], Answer.Index(1));
        m.Clock.Now += TimeSpan.FromSeconds(200);
        s.Pick(s.Shown[1], Answer.Index(0));
        m.Clock.Now += TimeSpan.FromSeconds(10);
        s.Pick(s.Shown[1], Answer.Index(1));
        Assert.Equal(30, s.Seconds(s.Shown[0]));
        Assert.Equal(210, s.Seconds(s.Shown[1]));
        Assert.Equal([2], s.Slow());
        var x = s.Finish();
        Assert.Equal(240, x.Seconds);
        Assert.Equal(210, m.P.State.Exams[0].Times![s.Shown[1].Id]);
    }

    [Fact]
    public void Finish_feeds_review_schedule_blank_as_wrong()
    {
        var m = Make();
        var s = Session(m);
        s.Pick(s.Shown[0], Answer.Index(1));
        s.Finish();
        Assert.Equal(2, m.P.State.Srs[s.Shown[0].Fp!].Box);
        Assert.Equal(1, m.P.State.Srs[s.Shown[1].Fp!].Box);
    }

    [Fact]
    public void Scoring_falls_back_to_one_point_per_question()
    {
        var m = Make();
        m.Study.Manifest.Courses[0].Scoring = null;
        Assert.Equal(new Scoring(5, 1, 0), ExamSession.ScoringFor(m.Study, m.Exam, 5));
        m.Exam.Scoring = new Scoring(5, 2, -1);
        Assert.Equal(new Scoring(5, 2, -1), ExamSession.ScoringFor(m.Study, m.Exam, 5));
    }
}

public class ExamClockTests
{
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(59.6, "01:00")]
    [InlineData(600, "10:00")]
    [InlineData(3725, "62:05")]
    [InlineData(-3, "00:00")]
    public void Clock_is_minutes_and_seconds(double s, string text) => Assert.Equal(text, ExamSession.Clock(s));
}
