using System.Text.Json.Nodes;
using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

// Trang xem lại mẫu tự soạn theo HTML của Moodle 4 (không dùng dữ liệu LMS thật). Kỳ vọng sinh bằng lmsquiz.ts chạy trong
// jsdom (Node, 07/10/2026): lms-expected/*-expected.json.
public class LmsQuizTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "Practice", "lms-expected");

    private static JsonNode Load(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, name)))!;

    private static readonly TimeZoneInfo Hcm = TimeZoneInfo.CreateCustomTimeZone("utc+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");

    public static TheoryData<string> Reviews() => [.. Load("reviews.json").AsObject().Select(kv => kv.Key)];

    [Theory]
    [MemberData(nameof(Reviews))]
    public void Same_question_as_typescript(string name)
    {
        var got = LmsQuiz.ParseReview((string)Load("reviews.json")[name]!);
        var want = Load("reviews-expected.json")[name];
        if (want is null)
        {
            Assert.Null(got);
            return;
        }
        Assert.NotNull(got);
        var json = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(got, StudyPack.JsonOptions))!;
        Assert.True(JsonNode.DeepEquals(want, json), $"{want.ToJsonString()}\n{json.ToJsonString()}");
    }

    [Fact]
    public void Hidden_answer_is_skipped()
    {
        Assert.Null(LmsQuiz.ParseReview((string)Load("reviews.json")["hidden"]!));
        var (_, info) = LmsQuiz.QuizPacks(SavedQuiz.ListFromJson(File.ReadAllText(Path.Combine(Dir, "saved.json"))), DateTimeOffset.FromUnixTimeSeconds(1760000000), Hcm);
        var q1 = info.Single(i => i.Quiz.File == "1-1");
        Assert.Equal((2, 1), (q1.Usable, q1.Unknown));
    }

    [Fact]
    public void Same_packs_as_typescript()
    {
        var want = Load("saved-expected.json");
        var (packs, info) = LmsQuiz.QuizPacks(SavedQuiz.ListFromJson(File.ReadAllText(Path.Combine(Dir, "saved.json"))), DateTimeOffset.FromUnixTimeSeconds(1760000000), Hcm);
        var gotPacks = new JsonArray([.. packs.Select(p => JsonNode.Parse(p.ToJson()))]);
        Assert.True(JsonNode.DeepEquals(want["packs"], gotPacks), $"{want["packs"]!.ToJsonString()}\n{gotPacks.ToJsonString()}");
        var wantInfo = want["info"]!.AsArray().Select(i => ((string)i!["quiz"]!["file"]!, (string)i["lessonId"]!, (int)i["usable"]!, (int)i["unknown"]!, (bool)i["shareable"]!));
        Assert.Equal(wantInfo, info.Select(i => (i.Quiz.File, i.LessonId, i.Usable, i.Unknown, i.Shareable)));
    }

    [Fact]
    public void Ids_are_slugs()
    {
        var (packs, info) = LmsQuiz.QuizPacks(SavedQuiz.ListFromJson(File.ReadAllText(Path.Combine(Dir, "saved.json"))), DateTimeOffset.FromUnixTimeSeconds(1760000000), Hcm);
        Assert.Equal("quiz-lms-mt1005", Assert.Single(packs).Id);
        // Tên chương là dữ liệu (khớp với chương ghi lại câu còn nhớ), giống bản TS, không dịch theo ngôn ngữ giao diện.
        Assert.Equal(("Quiz LMS đã lưu", LmsQuiz.UnitTitle), (LmsQuiz.UnitTitle, packs[0].Units[0].Title));
        Assert.All(packs[0].Units[0].Lessons, l => Assert.Matches("^[a-z0-9][a-z0-9-]{1,63}$", l.Id));
        Assert.True(PackValidator.Validate(JsonNode.Parse(packs[0].ToJson())).Ok);
        Assert.True(LmsQuiz.IsLmsQuizLesson(info.First(i => i.LessonId.Length > 0).LessonId));
        Assert.True(LmsQuiz.IsRecallLesson(LmsQuiz.RecallPrefix + "mt1005.x"));
    }
}
