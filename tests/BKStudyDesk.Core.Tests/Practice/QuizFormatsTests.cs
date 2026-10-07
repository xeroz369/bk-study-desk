using System.Text.Json;
using System.Text.Json.Nodes;
using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

// Port formats.test.ts. Kỳ vọng sinh bằng formats.ts (Node, jsdom cho Moodle XML, 07/10/2026): fmt-expected/.
public class QuizFormatsTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "Practice", "fmt-expected");
    private static readonly JsonNode Samples = JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, "samples.json")))!;
    private static readonly JsonNode Want = JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, "expected.json")))!;

    private static StudyPack ViDu() => StudyMarkdown.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Practice", "md-expected", "parse-vi-du.md"))).Pack;

    private static void SameAsTs(string key, Converted got)
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(new { lessons = got.Lessons, notes = got.Notes }, StudyPack.JsonOptions))!;
        Assert.True(JsonNode.DeepEquals(Want[key], json), $"{Want[key]!.ToJsonString()}\n{json.ToJsonString()}");
    }

    private static string Key(PackQuestion q) => string.Join("|", q.Type ?? "single", q.Answer?.ToJsonString(), string.Join(",", q.Answers ?? []),
        string.Join(",", q.Accept ?? []), q.Tolerance ?? 0, q.Options?.Count);

    private static List<PackQuestion> LessonQs(StudyPack p) => [.. p.Units.SelectMany(u => u.Lessons).SelectMany(l => l.Questions ?? [])];

    public static TheoryData<string> DetectCases() => [.. Samples["detect"]!.AsObject().Select(kv => kv.Key)];

    [Theory]
    [MemberData(nameof(DetectCases))]
    public void Detect_like_typescript(string name)
    {
        var want = (string?)Want["detect"]![name];
        var got = QuizFormats.Detect((string)Samples["detect"]![name]!);
        Assert.Equal(want, got is { } f ? QuizFormats.Name(f) : null);
    }

    [Fact]
    public void Aiken_like_typescript() => SameAsTs("aiken", QuizFormats.ParseAiken((string)Samples["aiken"]!));

    [Fact]
    public void Gift_like_typescript() => SameAsTs("gift", QuizFormats.ParseGift((string)Samples["gift"]!));

    [Fact]
    public void Gift_escapes_and_tex()
    {
        var qs = QuizFormats.ParseGift((string)Samples["gift"]!).Lessons.SelectMany(l => l.Questions!).ToList();
        var tex = qs.Single(q => q.Tag == "tex");
        Assert.Equal("<p>Tính {a} và = dấu</p>", tex.Prompt);
        Assert.Equal(["1", "2"], tex.Options!);
    }

    [Fact]
    public void ToGift_like_typescript() => Assert.Equal((string)Want["toGift"]!, QuizFormats.ToGift(ViDu()));

    [Fact]
    public void Gift_round_trip_keeps_answers()
    {
        var p = ViDu();
        var back = QuizFormats.ParseGift(QuizFormats.ToGift(p)).Lessons.SelectMany(l => l.Questions ?? []);
        Assert.Equal(LessonQs(p).Select(Key), back.Select(Key));
    }

    [Fact]
    public void Moodle_xml_like_typescript() => SameAsTs("xml", QuizFormats.ParseMoodleXml((string)Samples["xml"]!));

    [Fact]
    public void Broken_xml_is_note()
    {
        SameAsTs("broken", QuizFormats.ParseMoodleXml("<quiz><question"));
        Assert.Single(QuizFormats.ParseMoodleXml("").Notes);
    }

    [Fact]
    public void ToMoodleXml_like_typescript() => Assert.Equal((string)Want["toMoodleXml"]!, QuizFormats.ToMoodleXml(ViDu()));

    [Fact]
    public void Moodle_round_trip_keeps_answers_and_lessons()
    {
        var p = ViDu();
        var r = QuizFormats.ParseMoodleXml(QuizFormats.ToMoodleXml(p));
        Assert.Equal(p.Units.SelectMany(u => u.Lessons).Select(l => l.Title), r.Lessons.Select(l => l.Title));
        Assert.Equal(LessonQs(p).Select(Key), r.Lessons.SelectMany(l => l.Questions ?? []).Select(Key));
    }

    [Fact]
    public void Images_round_trip()
    {
        const string img = "data:image/png;base64,iVBORw0KGgo=";
        var p = ViDu();
        p.Units = [new PackUnit { Title = "C", Lessons = [new PackLesson { Id = "b1", Title = "B", Questions =
            [new PackQuestion { Prompt = $"<p>Hình <img src=\"{img}\"></p>", Options = ["a", "b"], Answer = JsonValue.Create(1), Solution = "x" }] }] }];
        var xml = QuizFormats.ToMoodleXml(p);
        Assert.Contains("@@PLUGINFILE@@/img1.png", xml);
        Assert.Contains(img, QuizFormats.ParseMoodleXml(xml).Lessons[0].Questions![0].Prompt);
    }

    [Fact]
    public void Aiken_pack_passes_validator()
    {
        var qs = QuizFormats.ParseAiken("Đề?\nA. x\nB. y\nANSWER: B").Lessons[0].Questions!;
        var p = ViDu();
        p.Units = [new PackUnit { Title = "C", Lessons = [new PackLesson { Id = "aiken", Title = "B", Questions = qs }] }];
        Assert.Empty(PackValidator.Validate(JsonNode.Parse(p.ToJson())).Errors);
    }
}
