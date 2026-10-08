using System.Text.Json.Nodes;
using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

/// <summary>Chỗ lưu trong bộ nhớ: không đụng data của người dùng.</summary>
internal sealed class MemoryStorage(string? json = null) : IProgressStorage
{
    public string? Json { get; set; } = json;
    public bool FailWrites { get; set; }
    public int Writes { get; private set; }

    public bool FailReads { get; set; }

    public Task<string?> ReadAsync(CancellationToken ct) => FailReads ? throw new IOException("GET state: HTTP 500") : Task.FromResult(Json);

    public Task<bool> WriteAsync(string json, CancellationToken ct)
    {
        if (FailWrites) return Task.FromResult(false);
        Json = json;
        Writes++;
        return Task.FromResult(true);
    }
}

public class ProgressTests
{
    private static readonly TimeZoneInfo Hcm = TimeZoneInfo.CreateCustomTimeZone("utc+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");

    // 07/10/2026 10:00 giờ Việt Nam.
    private static FixedClock Clock() => new(new DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero), Hcm);

    private static (StudyRegistry Study, PracticeProgress P, MemoryStorage Store, FixedClock Clock) Make(string? json = null, int questions = 3)
    {
        var clock = Clock();
        var study = new StudyRegistry(clock: clock);
        study.Manifest.Courses.Add(new Course
        {
            Id = "ppt", Name = "Phương pháp tính",
            Units = [new Unit { Title = "C1", Lessons = [new LessonEntry("b1", "Bài 1"), new LessonEntry("b2", "Bài 2"), new LessonEntry("b3", "Bài 3")] }],
        });
        study.AddLesson(new Lesson { Id = "b1", Title = "Bài 1", Questions = [.. Enumerable.Range(1, questions).Select(i => new Question { Prompt = $"Câu {i}", Options = ["a", "b"] })] });
        study.AddLesson(new Lesson { Id = "b2", Title = "Bài 2", Questions = [new Question { Prompt = "Câu riêng b2", Options = ["a", "b"] }] });
        var store = new MemoryStorage(json);
        return (study, new PracticeProgress(study, store, clock), store, clock);
    }

    [Fact]
    public void Today_uses_local_calendar_day()
    {
        var (_, p, _, clock) = Make();
        clock.Now = new DateTimeOffset(2026, 10, 7, 23, 30, 0, TimeSpan.Zero);
        Assert.Equal("2026-10-08", p.Today());
        Assert.Equal("2026-10-15", p.Today(7));
    }

    [Fact]
    public async Task Reads_state_written_by_1x()
    {
        // Bản 1.x cũ: chưa có srs, notes, times; exams sai kiểu; khóa lạ; record thiếu trường.
        const string old = """
            { "version": 1, "updatedAt": "2026-09-30T08:00:00", "questions": { "b1.q1": { "tries": 2, "firstTry": false, "correct": true,
              "lastCorrect": true, "reviewedOk": false, "chosen": 1, "at": "2026-09-30T08:00:00.000Z" }, "b1.q2": { "chosen": [0, 1] } },
              "lessons": { "b1": { "openedAt": "2026-09-30T07:00:00.000Z" } }, "exams": {}, "khac": 5 }
            """;
        var (_, p, store, _) = Make(old);
        await p.LoadAsync();
        Assert.Equal(2, p.Q("b1.q1")!.Tries);
        Assert.Equal(Answer.Index(1), p.Q("b1.q1")!.Chosen);
        Assert.Null(p.Q("b1.q2")!.Tries);
        Assert.Empty(p.State.Exams);
        Assert.Empty(p.State.Srs);
        p.Save();
        Assert.True(p.Pending);
        await p.FlushAsync();
        Assert.False(p.Pending);
        var written = JsonNode.Parse(store.Json!)!.AsObject();
        Assert.Equal(1, (int)written["version"]!);
        Assert.Equal(1, (int)written["questions"]!["b1.q1"]!["chosen"]!);
        Assert.Equal(2, written["questions"]!["b1.q2"]!["chosen"]!.AsArray().Count);
        Assert.IsType<JsonArray>(written["exams"]);
        Assert.IsType<JsonObject>(written["srs"]);
        // Khóa camelCase như bản TS, đọc lại được.
        Assert.True(written["questions"]!["b1.q1"]!.AsObject().ContainsKey("firstTry"));
        Assert.Equal(p.State.Questions.Count, ProgressState.Normalize(written).Questions.Count);
    }

    // Một bản ghi hỏng chỉ mất bản ghi đó: không được kéo cả nhóm về rỗng (lần ghi sau sẽ xóa hết lịch sử).
    [Fact]
    public void One_bad_record_does_not_drop_the_rest()
    {
        var s = ProgressState.Normalize(JsonNode.Parse("""
            { "questions": { "ok": { "tries": 1, "chosen": 0, "at": "a" }, "bad": { "tries": "x" }, "nul": { "chosen": null } },
              "exams": [ { "examId": "e1", "startedAt": "1", "answers": { "q": 1 } }, { "examId": 5, "answers": { "q": {} } },
                         { "examId": "e2", "startedAt": "2", "answers": { "q": null } } ],
              "srs": { "f": { "box": 1, "due": "d", "last": "l" }, "g": 3 },
              "times": { "f": 2, "g": "x" } }
            """));
        Assert.Equal(["ok", "nul"], s.Questions.Keys);
        Assert.Equal(["e1", "e2"], s.Exams.Select(e => e.ExamId));
        Assert.Equal(["f"], s.Srs.Keys);
        Assert.Equal(["f"], s.Times.Keys);
    }

    [Fact]
    public async Task Unreadable_file_keeps_empty_state_and_reports()
    {
        var (_, p, _, _) = Make("{hỏng");
        await p.LoadAsync();
        Assert.Empty(p.State.Questions);
        Assert.NotEqual("", p.SaveError);
    }

    [Fact]
    public async Task Failed_write_stays_pending()
    {
        var (_, p, store, _) = Make();
        store.FailWrites = true;
        p.RecordAnswer("b1.q1", Answer.Index(0), true);
        await p.FlushAsync();
        Assert.True(p.Pending);
        Assert.NotEqual("", p.SaveError);
        store.FailWrites = false;
        await p.FlushAsync();
        Assert.False(p.Pending);
        Assert.Equal("", p.SaveError);
    }

    [Fact]
    public void RecordAnswer_tracks_first_try_and_correct()
    {
        var (_, p, _, _) = Make();
        p.RecordAnswer("b1.q1", Answer.Index(1), false);
        p.RecordAnswer("b1.q1", Answer.Index(0), true);
        var r = p.Q("b1.q1")!;
        Assert.Equal((2, false, true, true), (r.Tries!.Value, r.FirstTry, r.Correct, r.LastCorrect));
        Assert.True(p.NeedsReview("b1.q1"));
        p.MarkReviewed("b1.q1", true);
        Assert.False(p.NeedsReview("b1.q1"));
        Assert.Equal("2026-10-07T03:00:00.000Z", r.At);
    }

    [Fact]
    public void Srs_leitner_boxes_first_answer_of_day_only()
    {
        var (study, p, _, clock) = Make();
        var fp = study.Question("b1.q1")!.Fp!;
        p.SrsAnswer(fp, true);
        Assert.Equal((2, "2026-10-10", 1, 0), (p.State.Srs[fp].Box, p.State.Srs[fp].Due, p.State.Srs[fp].Seen, p.State.Srs[fp].Wrong));
        p.SrsAnswer(fp, true);   // cùng ngày: không lên hộp
        Assert.Equal(2, p.State.Srs[fp].Box);
        p.SrsAnswer(fp, false);  // sai trong ngày: về hộp 1, mai ôn
        Assert.Equal((1, "2026-10-08"), (p.State.Srs[fp].Box, p.State.Srs[fp].Due));
        int[] boxes = [2, 3, 4, 5, 5];
        string[] due = ["2026-10-11", "2026-10-16", "2026-10-24", "2026-11-10", "2026-11-11"];
        for (var i = 0; i < boxes.Length; i++)
        {
            clock.Now = clock.Now.AddDays(1);
            p.SrsAnswer(fp, true);
            Assert.Equal((boxes[i], due[i]), (p.State.Srs[fp].Box, p.State.Srs[fp].Due));
        }
        Assert.Equal(6, p.State.Srs[fp].Seen);
    }

    [Fact]
    public void DueQuestions_hardest_first_one_per_fingerprint()
    {
        var (study, p, _, clock) = Make();
        var q1 = study.Question("b1.q1")!;
        var q2 = study.Question("b1.q2")!;
        p.SrsAnswer(q1.Fp!, true);   // hộp 2, 3 ngày sau
        p.SrsAnswer(q2.Fp!, false);  // hộp 1, mai
        Assert.Empty(p.DueQuestions());
        clock.Now = clock.Now.AddDays(3);
        Assert.Equal(["b1.q2", "b1.q1"], p.DueQuestions().Select(q => q.Id));
        Assert.Single(p.DueQuestions("ppt", limit: 1));
        Assert.Empty(p.DueQuestions("khac"));
    }

    [Fact]
    public void Merge_newer_wins_but_first_try_from_older()
    {
        var a = ProgressState.Normalize(JsonNode.Parse("""
            { "questions": { "x": { "tries": 1, "firstTry": false, "correct": false, "lastCorrect": false, "reviewedOk": false, "chosen": 0, "at": "2026-10-01T00:00:00.000Z" } },
              "exams": [{ "examId": "e", "startedAt": "2026-10-02", "seconds": 1, "answers": {}, "right": 0, "wrong": 0, "blank": 0, "score": 0, "max": 10 }],
              "srs": { "f": { "box": 1, "due": "2026-10-02", "last": "2026-10-01", "seen": 1, "wrong": 1 } },
              "notes": { "f": { "text": "cũ", "at": "2026-10-01" } }, "times": { "f": 5 } }
            """));
        var b = ProgressState.Normalize(JsonNode.Parse("""
            { "questions": { "x": { "tries": 3, "firstTry": true, "correct": true, "lastCorrect": true, "reviewedOk": true, "chosen": 1, "at": "2026-10-05T00:00:00.000Z" } },
              "exams": [{ "examId": "e", "startedAt": "2026-10-02", "seconds": 1, "answers": {}, "right": 0, "wrong": 0, "blank": 0, "score": 0, "max": 10 },
                        { "examId": "e", "startedAt": "2026-10-01", "seconds": 1, "answers": {}, "right": 0, "wrong": 0, "blank": 0, "score": 0, "max": 10 }],
              "srs": { "f": { "box": 3, "due": "2026-10-09", "last": "2026-10-02", "seen": 2, "wrong": 1 } },
              "notes": { "f": { "text": "", "at": "2026-10-03" } }, "times": { "f": 7 } }
            """));
        var m = ProgressState.Merge(a, b);
        var x = m.Questions["x"];
        Assert.Equal((false, true, 3, true, Answer.Index(1)), (x.FirstTry, x.Correct, x.Tries!.Value, x.ReviewedOk, x.Chosen));
        Assert.Equal(["2026-10-01", "2026-10-02"], m.Exams.Select(e => e.StartedAt));
        Assert.Equal(3, m.Srs["f"].Box);
        Assert.Equal("", m.Notes["f"].Text);
        Assert.Equal(7, m.Times["f"]);
    }

    [Fact]
    public void Notes_cleared_stay_as_marker()
    {
        var (_, p, _, _) = Make();
        p.SetNote("f", text: "nhớ đổi dấu");
        p.SetNote("f", flag: true);
        Assert.Equal(("nhớ đổi dấu", true), (p.Note("f")!.Text, p.Note("f")!.Flag));
        p.SetNote("f", text: "", flag: false);
        Assert.Empty(p.ActiveNotes());
        Assert.NotNull(p.Note("f"));
        p.SetNote(null, text: "x");
        Assert.Single(p.State.Notes);
    }

    [Fact]
    public void RecordTime_rounds_and_ignores_outliers()
    {
        var (_, p, _, _) = Make();
        p.RecordTime("f", 12.5);
        p.RecordTime("g", 0);
        p.RecordTime("h", 3601);
        p.RecordTime(null, 5);
        Assert.Equal(13, p.State.Times["f"]);
        Assert.Single(p.State.Times);
    }

    [Fact]
    public void MigrateIds_swaps_and_drops()
    {
        var (_, p, _, _) = Make();
        p.RecordAnswer("old.q1", Answer.Index(0), true);
        p.RecordAnswer("old.q2", Answer.Index(1), false);
        p.RecordAnswer("old.q3", Answer.Index(1), false);
        var moved = p.MigrateIds(new Dictionary<string, string> { ["old.q1"] = "old.q2", ["old.q2"] = "old.q1" }, ["old.q1", "old.q2", "old.q3"]);
        Assert.Equal(3, moved);
        Assert.True(p.Q("old.q2")!.Correct);
        Assert.False(p.Q("old.q1")!.Correct);
        Assert.Null(p.Q("old.q3"));
    }

    [Fact]
    public void Status_stats_and_next_lesson()
    {
        var (_, p, _, _) = Make(questions: 2);
        Assert.Equal(LessonStatus.NotStarted, p.Status("b1"));
        Assert.Equal(LessonStatus.NotWritten, p.Status("b3"));
        p.RecordAnswer("b1.q1", Answer.Index(0), true);
        Assert.Equal(LessonStatus.InProgress, p.Status("b1"));
        Assert.Equal("b1", p.NextLesson()!.Id);
        p.RecordAnswer("b1.q2", Answer.Index(0), true);
        Assert.Equal(LessonStatus.Done, p.Status("b1"));
        Assert.Equal("b2", p.NextLesson()!.Id);
        Assert.Equal(new CourseStats(3, 2, 1, 2, 2), p.CourseStats("ppt"));
        Assert.Equal("Xong", LessonStatus.Done.Label());
    }

    [Fact]
    public void SeedSchedule_starts_from_past_answers()
    {
        var (study, p, _, _) = Make();
        p.RecordAnswer("b1.q1", Answer.Index(0), true);
        p.RecordAnswer("b1.q2", Answer.Index(0), false);
        p.State.Srs.Clear();
        p.SeedSchedule();
        Assert.Equal((2, "2026-10-10"), (p.State.Srs[study.Question("b1.q1")!.Fp!].Box, p.State.Srs[study.Question("b1.q1")!.Fp!].Due));
        Assert.Equal((1, "2026-10-07"), (p.State.Srs[study.Question("b1.q2")!.Fp!].Box, p.State.Srs[study.Question("b1.q2")!.Fp!].Due));
    }
}

public class ProgressReadFailureTests
{
    [Fact]
    public async Task Unreadable_results_are_never_overwritten()
    {
        var study = new StudyRegistry();
        study.AddLesson(new Lesson { Id = "b1", Title = "B", Questions = [new Question { Prompt = "x", Options = ["a", "b"], Answer = 1 }] });
        var store = new MemoryStorage("{ hỏng") { FailReads = true };
        var p = new PracticeProgress(study, store);
        await p.LoadAsync();
        Assert.True(p.ReadFailed);
        Assert.NotEqual("", p.SaveError);
        p.RecordAnswer("b1.q1", Answer.Index(1), true);   // vẫn làm câu được, giữ trong app
        await p.FlushAsync();
        Assert.Equal(0, store.Writes);
        Assert.True(p.Pending);
        Assert.NotEqual("", p.SaveError);
        // Đọc lại được (người dùng sửa file, bấm Thử lại): ghi bình thường.
        store.FailReads = false;
        store.Json = "{\"questions\":{}}";
        await p.LoadAsync();
        Assert.False(p.ReadFailed);
        p.RecordAnswer("b1.q1", Answer.Index(1), true);
        await p.FlushAsync();
        Assert.Equal(1, store.Writes);
    }
}

public class ProgressAbsorbTests
{
    [Fact]
    public async Task Reload_keeps_answers_made_while_the_file_was_unreadable()
    {
        var study = new StudyRegistry();
        study.AddLesson(new Lesson { Id = "b1", Title = "B", Questions = [new Question { Prompt = "x", Options = ["a", "b"], Answer = 1 }] });
        var store = new MemoryStorage("{ hỏng") { FailReads = true };
        var old = new PracticeProgress(study, store);
        await old.LoadAsync();
        old.RecordAnswer("b1.q1", Answer.Index(1), true);
        // Nạp lại cả sổ (Thử lại) tạo PracticeProgress mới: phải mang câu vừa làm sang.
        store.FailReads = false;
        store.Json = "{\"questions\":{}}";
        var fresh = new PracticeProgress(study, store);
        await fresh.LoadAsync();
        fresh.Absorb(old);
        Assert.NotNull(fresh.Q("b1.q1"));
        Assert.True(fresh.Pending);
        var stillBroken = new PracticeProgress(study, new MemoryStorage { FailReads = true });
        await stillBroken.LoadAsync();
        stillBroken.Absorb(old);
        Assert.NotNull(stillBroken.Q("b1.q1"));
    }
}
