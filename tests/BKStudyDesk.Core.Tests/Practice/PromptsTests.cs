using System.Text.Json.Nodes;
using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

// Chuỗi kỳ vọng sinh bằng chính buildPrompt của prompts.ts (Node, 07/10/2026): prompts-expected/<ca>.txt, đầu vào ở cases.json.
public class PromptsTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "Practice", "prompts-expected");

    public static TheoryData<string> Cases() => [.. JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, "cases.json")))!.AsObject().Select(kv => kv.Key)];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Same_text_as_typescript(string name)
    {
        var c = JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, "cases.json")))![name]!.AsArray();
        var p = c[1]!;
        var input = new PromptInput(
            (string)p["course"]!,
            (int)p["count"]!,
            (string)p["extra"]!,
            Unit: (string?)p["unit"],
            Lesson: (string?)p["lesson"],
            Units: p["units"]?.AsArray().Select(u => (string)u!).ToList(),
            Question: (string?)p["question"],
            Pack: (string?)p["pack"]);
        var mode = Prompts.ParseMode((string)c[0]!);
        Assert.Equal(File.ReadAllText(Path.Combine(Dir, name + ".txt")).ReplaceLineEndings("\n"), Prompts.Build(mode, input));
    }

    [Fact]
    public void Format_is_study_markdown_template() => Assert.StartsWith("---\nmon: ", Prompts.Format);
}
