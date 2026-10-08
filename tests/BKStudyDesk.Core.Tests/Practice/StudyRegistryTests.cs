using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

/// <summary>Đồng hồ cố định cho test (TimeProvider của .NET, không cần package).</summary>
internal sealed class FixedClock(DateTimeOffset now, TimeZoneInfo? zone = null) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
    public override TimeZoneInfo LocalTimeZone { get; } = zone ?? TimeZoneInfo.Utc;
}

public class StudyRegistryTests
{
    private const string PackJson = """
        {
          "format": "studypack/1", "id": "goi-a", "title": "Gói A", "version": "1.0.0",
          "course": { "code": "mt1009", "name": "Phương pháp tính" },
          "authors": [{ "name": "An" }, { "name": "Bình" }],
          "settings": { "shuffleQuestions": true, "shuffleOptions": false },
          "units": [{ "title": "Chương 1", "lessons": [{ "id": "bai-1", "title": "Bài một",
            "sources": [{ "title": "Slide", "pages": "5" }],
            "sections": [{ "title": "Ý", "body": "<p>x</p><script>bad()</script>" }],
            "questions": [
              { "prompt": "Câu một", "options": ["a", "b", "c"], "answer": 1, "solution": "s" },
              { "type": "truefalse", "prompt": "Câu hai", "answer": false, "solution": "s" },
              { "id": "so", "type": "numeric", "prompt": "Câu ba", "answer": 1.5, "tolerance": 0.01, "unit": "m", "solution": "s" },
              { "type": "multi", "prompt": "Câu bốn", "options": ["x", "y", "z"], "answers": [0, 2], "solution": "s" },
              { "type": "short", "prompt": "Câu năm", "accept": ["Newton"], "solution": "s" }
            ] }] }],
          "exams": [{ "id": "de-1", "title": "Đề 1", "minutes": 30, "questionRefs": ["bai-1.q1"],
            "questions": [{ "prompt": "Câu riêng của đề", "options": ["1", "2"], "answer": 0, "solution": "s" }] }]
        }
        """;

    private static string Strip(string html) => html.Replace("<script>bad()</script>", "");

    private static StudyRegistry WithCourse(int questions, Blueprint? bp = null)
    {
        var reg = new StudyRegistry(clock: new FixedClock(DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000)));
        reg.Manifest.Courses.Add(new Course
        {
            Id = "ppt", Name = "Phương pháp tính", Code = "MT1009", Blueprint = bp,
            Units = [new Unit { Title = "Chương 1", Lessons = [new LessonEntry("ppt-1", "Bài 1")] }],
        });
        reg.AddLesson(new Lesson { Id = "ppt-1", Title = "Bài 1", Questions = [.. Enumerable.Range(1, questions).Select(i => new Question { Prompt = $"Câu {i}", Options = ["a", "b"] })] });
        return reg;
    }

    [Fact]
    public void AddLesson_sets_id_lesson_and_fingerprint()
    {
        var reg = WithCourse(2);
        var qs = reg.Lessons["ppt-1"].Questions!;
        Assert.Equal(["ppt-1.q1", "ppt-1.q2"], qs.Select(q => q.Id));
        Assert.All(qs, q => Assert.Equal("ppt-1", q.LessonId));
        Assert.Equal(Fingerprint.Of(qs[0]), qs[0].Fp);
        Assert.Same(qs[1], reg.Question("ppt-1.q2"));
        Assert.Equal(["ppt-1"], reg.Entries("ppt").Select(e => e.Id));
        Assert.Equal("Chương 1", reg.Entry("ppt-1")!.UnitTitle);
    }

    [Fact]
    public void AddPack_joins_course_by_code_and_prefixes_ids()
    {
        var reg = new StudyRegistry(Strip);
        reg.AddPack(StudyPack.Parse(PackJson)!);
        var c = Assert.Single(reg.Manifest.Courses);
        Assert.Equal(("mon-mt1009", "MT1009"), (c.Id, c.Code));
        var unit = Assert.Single(c.Units);
        Assert.Equal(new PackInfo("goi-a", "Gói A", "An, Bình"), unit.Pack);
        var lesson = reg.Lessons["goi-a.bai-1"];
        Assert.Equal("<p>x</p>", lesson.Sections![0].Html);
        Assert.Equal(new SourceRef("Slide", "5"), lesson.Sources![0]);
        Assert.Equal(new ShuffleSetting(true, false), lesson.Shuffle);
        var qs = lesson.Questions!;
        Assert.Equal(["goi-a.bai-1.q1", "goi-a.bai-1.q2", "goi-a.bai-1.so", "goi-a.bai-1.q4", "goi-a.bai-1.q5"], qs.Select(q => q.Id));
        Assert.All(qs, q => Assert.Equal("goi-a", q.PackId));
        // Đúng, Sai thành trắc nghiệm hai phương án (chữ cố định: là dữ liệu, nằm trong fingerprint).
        Assert.Equal(["Đúng", "Sai"], qs[1].Options);
        Assert.Equal(1, qs[1].Answer);
        Assert.Equal((1.5, 0.01, "m", -1), (qs[2].Value!.Value, qs[2].Tolerance!.Value, qs[2].Unit, qs[2].Answer));
        Assert.Equal([0, 2], qs[3].Answers!);
        Assert.Equal(["Newton"], qs[4].Accept!);
        var exam = reg.Exams["goi-a.de-1"];
        Assert.Equal(["goi-a.bai-1.q1"], exam.QuestionIds!);
        Assert.Equal("goi-a.de-1.q1", exam.Questions![0].Id);
        Assert.Equal(c.Id, exam.CourseId);
        Assert.Equal(2, reg.ExamQuestions(exam).Count);
    }

    [Fact]
    public void AddPack_merges_same_question_once()
    {
        var reg = WithCourse(1);
        // Gói cùng mã môn, chương và bài trùng tên (khác hoa thường, khoảng trắng), có lại "Câu 1" và một câu mới.
        var json = PackJson.Replace("\"Chương 1\"", "\"  chương   1 \"").Replace("\"Bài một\"", "\"BÀI 1\"").Replace("\"Câu một\", \"options\": [\"a\", \"b\", \"c\"], \"answer\": 1", "\"Câu 1\", \"options\": [\"a\", \"b\"], \"answer\": 0");
        reg.AddPack(StudyPack.Parse(json)!);
        var c = Assert.Single(reg.Manifest.Courses);
        Assert.Single(c.Units);
        Assert.Single(c.Units[0].Lessons);
        var qs = reg.Lessons["ppt-1"].Questions!;
        Assert.Equal(5, qs.Count);
        Assert.Equal(1, qs.Count(q => q.Prompt == "Câu 1"));
        Assert.All(qs, q => Assert.Equal("ppt-1", q.LessonId));
        Assert.Equal(2, reg.Lessons["ppt-1"].Sections!.Count + 1);
    }

    [Fact]
    public void RandomExam_scales_time_when_short()
    {
        var reg = WithCourse(4, new Blueprint(50, [20]));
        var exam = reg.RandomExam("ppt", random: Shuffle.Rng(1))!;
        Assert.Equal(4, exam.QuestionIds!.Count);
        Assert.Equal(10, exam.Minutes);
        Assert.Equal(new Scoring(4, 2.5, -0.5), exam.Scoring);
        Assert.Equal(new ShuffleSetting(false, true), exam.Shuffle);
        Assert.Equal("ngau-nhien-ppt-1700000000000", exam.Id);
        Assert.Contains("Phương pháp tính", exam.Title);
        Assert.Same(exam, reg.Exams[exam.Id]);
    }

    [Fact]
    public void RandomExam_minimum_five_minutes_and_none_when_empty()
    {
        var reg = WithCourse(1, new Blueprint(50, [100]));
        Assert.Equal(5, reg.RandomExam("ppt", random: Shuffle.Rng(1))!.Minutes);
        Assert.Null(WithCourse(0).RandomExam("ppt"));
        Assert.Null(WithCourse(3).RandomExam("khong-co"));
    }

    [Fact]
    public void CanRandomExam_rule()
    {
        Assert.False(WithCourse(9).CanRandomExam(WithCourse(9).Course("ppt")!));
        var reg = WithCourse(10);
        Assert.True(reg.CanRandomExam(reg.Course("ppt")!));
        var bp = WithCourse(1, new Blueprint(30));
        Assert.True(bp.CanRandomExam(bp.Course("ppt")!));
    }

    [Fact]
    public void EnsureCourse_makes_draft_once()
    {
        var reg = WithCourse(1);
        Assert.Equal("ppt", reg.EnsureCourse("mt1009", "x").Id);
        var draft = reg.EnsureCourse("co1007", "Cấu trúc rời rạc");
        Assert.Equal(("mon-co1007", "CO1007"), (draft.Id, draft.Code));
        Assert.Same(draft, reg.EnsureCourse("CO1007", "y"));
        Assert.Same(draft, reg.Course("mon-co1007"));
        Assert.Single(reg.Manifest.Courses);
    }
}
