using System.Text.Json.Nodes;
using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

// Port src/ui/src/lib/study/pack.test.ts. Bản private có gói đề thật; repo public không có, nên dùng gói mẫu dựng ở đây.
public class PackValidatorTests
{
    private const string Minimal = """
        {
          "format": "studypack/1", "id": "vi-du-mau", "title": "Ví dụ", "version": "1.0.0",
          "course": { "code": "MT1009", "name": "Phương pháp tính" },
          "authors": [{ "name": "xeroz369" }], "license": "CC-BY-SA-4.0", "verified": { "method": "python" },
          "units": [{ "title": "Chương 1", "lessons": [{ "id": "bai-1", "title": "Bài 1",
            "sections": [{ "title": "Ý chính", "body": "<p>Định lý \\( f(a)f(b)<0 \\)</p>" }],
            "questions": [
              { "prompt": "Câu một", "options": ["a", "b", "c"], "answer": 1, "solution": "vì b" },
              { "type": "truefalse", "prompt": "Câu hai", "answer": true, "solution": "đúng" },
              { "type": "numeric", "prompt": "Câu ba", "answer": 1.5, "tolerance": 0.01, "solution": "1,5" },
              { "type": "short", "prompt": "Câu bốn", "accept": ["Newton"], "solution": "Newton" },
              { "type": "multi", "prompt": "Câu năm", "options": ["x", "y", "z"], "answers": [0, 2], "solution": "x, z" }
            ] }] }],
          "exams": [{ "id": "de-1", "title": "Đề 1", "minutes": 30, "questionRefs": ["bai-1.q1"] }]
        }
        """;

    private static JsonObject Example() =>
        JsonNode.Parse(Examples.Read("hcmut-mt1009-chia-doi.studypack.json") ?? Minimal)!.AsObject();

    private static string[] Paths(JsonNode? p) => [.. PackValidator.Validate(p).Errors.Select(e => e.Path)];

    private static JsonObject FirstQuestion(JsonObject p) => p["units"]![0]!["lessons"]![0]!["questions"]![0]!.AsObject();

    [Fact]
    public void Example_is_valid_and_counted()
    {
        foreach (var p in new[] { Example(), JsonNode.Parse(Minimal)!.AsObject() })
        {
            var r = PackValidator.Validate(p);
            Assert.Empty(r.Errors);
            Assert.True(r.Ok);
            Assert.True(r.Stats.Questions > 0);
            Assert.Equal(r.Stats.Questions, PackValidator.CountQuestions(StudyPack.Parse(p.ToJsonString())!));
        }
    }

    [Fact]
    public void Minimal_has_no_warnings_and_counts_all()
    {
        var r = PackValidator.Validate(JsonNode.Parse(Minimal));
        Assert.Empty(r.Warnings);
        Assert.Equal(new PackStats(1, 5, 1), r.Stats);
    }

    [Fact]
    public void Not_an_object_fails_at_once()
    {
        Assert.False(PackValidator.Validate(null).Ok);
        Assert.Contains("JSON object", PackValidator.Validate(new JsonArray()).Errors[0].Message);
    }

    [Fact]
    public void Frame_errors()
    {
        var p = Example();
        p["format"] = "x";
        p["id"] = "Có Dấu";
        p["version"] = "1";
        p["authors"] = new JsonArray();
        p["units"] = new JsonArray();
        var got = Paths(p);
        foreach (var path in new[] { "format", "id", "version", "authors", "units" }) Assert.Contains(path, got);
    }

    [Fact]
    public void Question_errors_in_order()
    {
        var p = Example();
        p.Remove("exams");
        p["units"] = JsonNode.Parse("""
            [{ "title": "C", "lessons": [{ "id": "b1", "title": "B", "questions": [
              { "id": "q1", "prompt": "a", "options": ["x", "y"], "answer": 5, "solution": "s" },
              { "id": "q1", "prompt": "b", "type": "multi", "options": ["x", "y"], "answers": [], "solution": "s" },
              { "prompt": "c", "type": "numeric", "answer": true, "solution": "s" },
              { "prompt": "d", "type": "short", "accept": [], "solution": "s" }
            ] }] }]
            """);
        Assert.Equal(
        [
            "units[0].lessons[0].questions[0].answer",
            "units[0].lessons[0].questions[1].id",
            "units[0].lessons[0].questions[1].answers",
            "units[0].lessons[0].questions[2].answer",
            "units[0].lessons[0].questions[3].accept",
        ], Paths(p));
    }

    [Fact]
    public void Blocks_script_external_image_and_event_attributes()
    {
        var p = Example();
        var q = FirstQuestion(p);
        q["prompt"] = "<script>alert(1)</script>";
        q["solution"] = "<img src=\"https://x/y.png\">";
        q["options"] = new JsonArray("<b onclick=\"x()\">a</b>", "b");
        var got = Paths(p);
        Assert.Contains("units[0].lessons[0].questions[0].prompt", got);
        Assert.Contains("units[0].lessons[0].questions[0].solution", got);
        Assert.Contains("units[0].lessons[0].questions[0].options[0]", got);
    }

    [Fact]
    public void Group_optional_but_not_blank()
    {
        var p = Example();
        var q = FirstQuestion(p);
        q["group"] = "f1";
        Assert.True(PackValidator.Validate(p).Ok);
        q["group"] = " ";
        Assert.Equal(["units[0].lessons[0].questions[0].group"], Paths(p));
    }

    [Fact]
    public void Exam_ref_must_exist()
    {
        var p = Example();
        p["exams"] = JsonNode.Parse("""[{ "id": "de-1", "title": "Đề", "minutes": 30, "questionRefs": ["khong-co.q1"] }]""");
        Assert.Equal(["exams[0].questionRefs[0]"], Paths(p));
    }

    // JSON hiểu "\f", "\t" là ký tự điều khiển: dấu hiệu quên viết "\\frac", "\\times".
    [Fact]
    public void Control_chars_mean_unescaped_tex()
    {
        var p = Example();
        FirstQuestion(p)["prompt"] = "x " + (char)12 + "rac12";
        Assert.Equal(["units[0].lessons[0].questions[0].prompt"], Paths(p));
    }

    [Fact]
    public void Every_example_json_pack_is_valid()
    {
        if (Examples.Dir() is not { } dir) return;
        foreach (var f in Directory.EnumerateFiles(dir, "*.json"))
        {
            var r = PackValidator.Validate(JsonNode.Parse(File.ReadAllText(f)), new FileInfo(f).Length);
            Assert.True(r.Ok, $"{Path.GetFileName(f)}: {string.Join("; ", r.Errors.Select(e => e.Path + " " + e.Message))}");
        }
    }
}
