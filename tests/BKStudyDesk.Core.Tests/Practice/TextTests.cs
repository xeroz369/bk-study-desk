using System.Text.Json;
using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

public class TextTests
{
    [Fact]
    public void PlainText_removes_nested_tags()
    {
        var s = Text.PlainText("<scr<script>ipt>x<b>y</b>");
        Assert.DoesNotContain('<', s);
        Assert.DoesNotContain('>', s);
        Assert.EndsWith("xy", s);
    }

    [Theory]
    [InlineData("  TIẾP   TUYẾN ", "tiếp tuyến")]
    [InlineData("a  b", "a b")]
    [InlineData("Newton-Raphson", "newton-raphson")]
    public void NormText_lowercases_and_collapses_spaces(string input, string expected) => Assert.Equal(expected, Text.NormText(input));

    [Fact]
    public void NormText_composes_vietnamese_marks() => Assert.Equal("tiếp", Text.NormText("tiếp"));

    // String(number) của JS: ghép vào fingerprint câu numeric và chữ đáp án, phải giống hệt bản TS.
    [Theory]
    [InlineData(2, "2")]
    [InlineData(0.1, "0.1")]
    [InlineData(0.30000000000000004, "0.30000000000000004")]
    [InlineData(1e-7, "1e-7")]
    [InlineData(0.000001, "0.000001")]
    [InlineData(1e21, "1e+21")]
    [InlineData(1.5e21, "1.5e+21")]
    [InlineData(123456789012, "123456789012")]
    [InlineData(1e20, "100000000000000000000")]
    [InlineData(-0.5, "-0.5")]
    [InlineData(1.414, "1.414")]
    [InlineData(0, "0")]
    public void JsNumber_matches_javascript(double value, string expected) => Assert.Equal(expected, JsNumber.Format(value));
}

public class AnswerTests
{
    private static Answer RoundTrip(string json) => JsonSerializer.Deserialize<Answer>(json);

    [Theory]
    [InlineData("1")]
    [InlineData("[0,2]")]
    [InlineData("\"1,5\"")]
    [InlineData("2.5")]
    public void Json_round_trips(string json) => Assert.Equal(json, JsonSerializer.Serialize(RoundTrip(json)));

    [Fact]
    public void Reads_each_shape()
    {
        Assert.Equal(1, RoundTrip("1").Number);
        Assert.Equal([0, 2], RoundTrip("[0,2]").Indices!);
        Assert.Equal("1,5", RoundTrip("\"1,5\"").Text);
    }

    [Fact]
    public void Blank_rules_match_typescript()
    {
        Assert.True(Answer.IsBlankValue(null));
        Assert.True(Answer.IsBlankValue(Answer.Many()));
        Assert.True(Answer.IsBlankValue(Answer.Of("  ")));
        Assert.False(Answer.IsBlankValue(Answer.Index(0)));
        Assert.False(Answer.IsBlankValue(Answer.Many(0)));
    }
}
