using System.Text.Json.Nodes;
using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

// Fingerprint là khóa của lịch ôn, ghi chú, thời gian làm câu trong ket-qua.json: đổi giá trị là mất tiến độ đã lưu.
// Giá trị chụp từ bản 1.1.6 (src/ui/src/lib/study/fingerprint.test.ts), chỉ sửa khi cố ý đổi cách tính (kèm migrate dữ liệu).
public class FingerprintTests
{
    private static Question Q(string prompt, string[] options, int answer = -1, string? type = null, double? value = null, string[]? accept = null, int[]? answers = null) =>
        new() { Prompt = prompt, Options = [.. options], Answer = answer, Type = type, Value = value, Accept = accept, Answers = answers };

    private static readonly Question[] Samples =
    [
        Q(@"<p>Tính \( \int_0^1 x\,dx \)</p>", ["<p>0,5</p>", "1", "2", "Cả A và B"], 0),
        Q("Đúng hay sai: 1 &lt; 2&nbsp;?", ["Đúng", "Sai"], 0),
        Q("Nghiệm của x - 2 = 0", [], type: "numeric", value: 2),
        Q("Tên phương pháp?", [], type: "short", accept: ["Newton", "  Newton-Raphson "]),
        Q("<p>Ảnh <img src=\"data:image/png;base64,AAAA\"> ở đây</p>", ["A", "B", "C"], 2),
        Q("Chọn các số chẵn", ["2", "3", "4"], type: "multi", answers: [0, 2]),
    ];

    private static readonly string[] Pinned = ["d3453892e0d90eb9", "b37705ec71120755", "dd4e9d0bede23820", "f78cc7320c99af49", "3457123acc27bccb", "c96afee3fb683078"];

    private static readonly string[] ExamplePinned = ["f3abc540f5f37b4b", "b977fed184d6411c", "0f510dad8343d3da", "4c226cd145af52bc", "02bf2f69145bd7a0", "ac38ffc90f31e34c"];

    [Fact]
    public void Keeps_saved_values() => Assert.Equal(Pinned, Samples.Select(Fingerprint.Of));

    [Fact]
    public void Keeps_values_for_example_pack()
    {
        var json = Examples.Read("hcmut-mt1009-chia-doi.studypack.json");
        if (json is null) return;
        var fps = JsonNode.Parse(json)!["units"]!.AsArray()
            .SelectMany(u => u!["lessons"]!.AsArray())
            .SelectMany(l => l!["questions"]?.AsArray() ?? [])
            .Select(q => Fingerprint.Of(
                (string?)q!["type"],
                (string)q["prompt"]!,
                q["options"]?.AsArray().Select(o => (string)o!).ToList(),
                q["answer"] is JsonValue v && v.TryGetValue<double>(out var n) ? n : null,
                q["accept"]?.AsArray().Select(o => (string)o!).ToList()));
        Assert.Equal(ExamplePinned, fps);
    }

    [Fact]
    public void Ignores_option_order_spacing_and_tags() =>
        Assert.Equal(Fingerprint.Of(Q("<p>Câu  hỏi</p>", ["x", "y", "z"])), Fingerprint.Of(Q("Câu hỏi", ["z", "<b>x</b>", "y "])));

    [Fact]
    public void Different_prompt_differs() =>
        Assert.NotEqual(Fingerprint.Of(Q("a", ["1", "2"])), Fingerprint.Of(Q("b", ["1", "2"])));

    [Fact]
    public void FpText_drops_tags_entities_spaces()
    {
        Assert.Equal("a<&>[img]b", Fingerprint.FpText("<p>A&nbsp;&lt;&amp;&gt; <img src=\"x\"> B</p>"));
        Assert.Equal(@"\(ab\)", Fingerprint.FpText(@"\( a\,b \)"));
    }
}

public class GradeTests
{
    private static Question Base() => new() { Id = "q", Prompt = "p", Options = ["a", "b", "c"], Answer = 1 };

    [Theory]
    [InlineData("1,5", 1.5)]
    [InlineData("1.5", 1.5)]
    [InlineData(" -2 ", -2)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("1 000", 1000)]
    [InlineData("1e-3", 0.001)]
    public void ParseNumber_reads_vietnamese_and_english(string s, double n) => Assert.Equal(n, Grade.ParseNumber(s));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("1,2,3x")]
    public void ParseNumber_rejects_non_numbers(string s) => Assert.Null(Grade.ParseNumber(s));

    [Fact]
    public void Single_matches_index()
    {
        Assert.True(Grade.IsCorrect(Base(), Answer.Index(1)));
        Assert.False(Grade.IsCorrect(Base(), Answer.Index(0)));
        Assert.False(Grade.IsCorrect(Base(), null));
        Assert.False(Grade.IsCorrect(Base(), Answer.Of("1")));
    }

    [Fact]
    public void Multi_needs_exact_set_any_order()
    {
        var q = Base();
        q.Type = "multi";
        q.Answer = -1;
        q.Answers = [0, 2];
        Assert.True(Grade.IsCorrect(q, Answer.Many(2, 0)));
        Assert.False(Grade.IsCorrect(q, Answer.Many(0)));
        Assert.False(Grade.IsCorrect(q, Answer.Many(0, 1, 2)));
    }

    [Fact]
    public void Numeric_uses_tolerance_and_vietnamese_comma()
    {
        var q = new Question { Type = "numeric", Answer = -1, Value = 1.414, Tolerance = 0.001 };
        Assert.True(Grade.IsCorrect(q, Answer.Of("1,4145")));
        Assert.True(Grade.IsCorrect(q, Answer.Of(1.413)));
        Assert.False(Grade.IsCorrect(q, Answer.Of("1.42")));
        Assert.False(Grade.IsCorrect(q, Answer.Of("abc")));
        Assert.True(Grade.IsCorrect(new Question { Type = "numeric", Answer = -1, Value = 0.1 + 0.2 }, Answer.Of("0.3")));
    }

    [Fact]
    public void Short_ignores_case_and_spaces_keeps_marks()
    {
        var q = new Question { Type = "short", Answer = -1, Accept = ["Newton-Raphson", "Tiếp tuyến"] };
        Assert.True(Grade.IsCorrect(q, Answer.Of("  newton-raphson ")));
        Assert.True(Grade.IsCorrect(q, Answer.Of("TIẾP   TUYẾN")));
        Assert.False(Grade.IsCorrect(q, Answer.Of("tiep tuyen")));
    }

    [Fact]
    public void AnswerText_by_type()
    {
        Assert.Equal("B", Grade.AnswerText(Base()));
        Assert.Equal("A, C", Grade.AnswerText(new Question { Type = "multi", Answers = [0, 2] }));
        Assert.Equal("2 ± 0.1 m", Grade.AnswerText(new Question { Type = "numeric", Value = 2, Tolerance = 0.1, Unit = "m" }));
        Assert.Equal("a / b", Grade.AnswerText(new Question { Type = "short", Accept = ["a", "b"] }));
    }
}
