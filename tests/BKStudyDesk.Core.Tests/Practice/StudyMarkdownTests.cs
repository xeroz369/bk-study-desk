using System.Text.Json.Nodes;
using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

// Gói kỳ vọng sinh bằng parseMarkdown của markdown.ts (Node, 07/10/2026): md-expected/parse-<ca>.md và .json.
public class MdParseTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "Practice", "md-expected");

    private static string Read(string name) => File.ReadAllText(Path.Combine(Dir, name));

    private static JsonNode Issues(IEnumerable<MdIssue> xs) =>
        new JsonArray([.. xs.Select(x => (JsonNode)new JsonObject { ["line"] = x.Line, ["message"] = x.Message })]);

    [Theory]
    [InlineData("vi-du")]
    [InlineData("types")]
    [InlineData("errors")]
    [InlineData("no-unit")]
    public void Same_pack_as_typescript(string name)
    {
        var images = File.Exists(Path.Combine(Dir, $"parse-{name}.images.json"))
            ? JsonNode.Parse(Read($"parse-{name}.images.json"))!.AsObject().ToDictionary(kv => kv.Key, kv => (string)kv.Value!)
            : null;
        var r = StudyMarkdown.Parse(Read($"parse-{name}.md"), images);
        var want = JsonNode.Parse(Read($"parse-{name}.json"))!;
        Assert.Equal(want["errors"]!.ToJsonString(), Issues(r.Errors).ToJsonString());
        Assert.Equal(want["warnings"]!.ToJsonString(), Issues(r.Warnings).ToJsonString());
        var got = JsonNode.Parse(r.Pack.ToJson())!;
        Assert.True(JsonNode.DeepEquals(want["pack"], got), $"khác bản TS:\n{want["pack"]!.ToJsonString()}\n{got.ToJsonString()}");
    }

    // Đề thật (hcmut-*) chỉ có ở bản private: kỳ vọng sinh ngoài repo, thư mục đặt qua BK_MD_EXPECTED (<tên>.md, <tên>.json).
    [Fact]
    public void Private_examples_match_typescript()
    {
        var dir = Environment.GetEnvironmentVariable("BK_MD_EXPECTED");
        if (string.IsNullOrEmpty(dir)) return;
        var files = Directory.GetFiles(dir, "*.md");
        Assert.NotEmpty(files);
        foreach (var md in files)
        {
            var want = JsonNode.Parse(File.ReadAllText(Path.ChangeExtension(md, ".json")))!;
            var r = StudyMarkdown.Parse(File.ReadAllText(md));
            Assert.Equal(want["errors"]!.ToJsonString(), Issues(r.Errors).ToJsonString());
            Assert.True(JsonNode.DeepEquals(want["pack"], JsonNode.Parse(r.Pack.ToJson())), Path.GetFileName(md));
        }
    }

    [Fact]
    public void Zip_reads_markdown_and_images()
    {
        using var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var w = new StreamWriter(zip.CreateEntry("goi/bai.md").Open()))
                w.Write(string.Join("\n", "---", "mon: MT1009 X", "tac-gia: A", "---", "# C", "## B", "### Câu 1", "Hình ![a](img/1.png)", "= 2", "> g"));
            using (var s = zip.CreateEntry("goi/img/1.png").Open()) s.Write([1, 2, 3]);
        }
        // Đường nhập của Soạn: đọc zip (có giới hạn) rồi đọc Markdown kèm ảnh.
        var (text, images) = Transfer.ReadFile("goi.zip", ms.ToArray())!.Value;
        var r = StudyMarkdown.Parse(text, images);
        Assert.DoesNotContain(r.Warnings, w => w.Message.Contains("img/1.png"));
        Assert.Contains("data:image/png;base64,AQID", r.Pack.Units[0].Lessons[0].Questions![0].Prompt);
    }

    [Fact]
    public void Vi_du_has_four_types()
    {
        var qs = StudyMarkdown.Parse(Read("parse-vi-du.md")).Pack.Units.SelectMany(u => u.Lessons).SelectMany(l => l.Questions!).ToList();
        Assert.Equal(["single", "multi", "truefalse", "numeric"], qs.Select(q => q.Type ?? "single"));
        Assert.Equal(1, qs[0].AnswerNumber);
        Assert.Equal([0, 1], qs[1].Answers!);
        Assert.True(qs[2].AnswerBool);
        Assert.Equal((1, 0.001), (qs[3].AnswerNumber!.Value, qs[3].Tolerance!.Value));
    }

    [Fact]
    public void Crlf_and_bom()
    {
        var md = Read("parse-types.md");
        var a = StudyMarkdown.Parse(md).Pack.ToJson();
        var b = StudyMarkdown.Parse(((char)0xFEFF) + md.Replace("\n", "\r\n")).Pack.ToJson();
        Assert.Equal(a, b);
    }

    [Fact]
    public void Missing_image_is_warning()
    {
        var r = StudyMarkdown.Parse(Read("parse-types.md"));
        Assert.Contains(r.Warnings, w => w.Message.Contains("img/a.png"));
        var q6 = r.Pack.Units[0].Lessons[0].Questions![5];
        Assert.Contains("[thiếu ảnh: hình]", q6.Prompt);
    }

    // Mở rộng v1 (SPEC 2.6): dòng id ngay dưới ## và ###, dòng "Câu:" của đề.
    [Fact]
    public void Id_lines_and_exam_refs()
    {
        var md = string.Join("\n",
            "---", "mon: MT1009 X", "tac-gia: A", "---",
            "# Chương", "## Bài hai", "id: ppt-02", "Kiến thức.",
            "### Câu 1", "id: rieng", "Đề", "- [x] a", "- [ ] b", "> g",
            "### Câu 2", "Đề hai", "- [x] a", "- [ ] b", "> g",
            "# Đề thi thử", "## GK251 chia đôi", "id: ppt-gk251-chia-doi", "Thời gian: 12 phút · Đúng: 0.5 · Sai: -0.1", "Câu: ppt-02.rieng, ppt-02.q2");
        var r = StudyMarkdown.Parse(md, validate: false);
        Assert.Empty(r.Errors);
        var l = r.Pack.Units[0].Lessons[0];
        Assert.Equal("ppt-02", l.Id);
        Assert.Equal("<p>Kiến thức.</p>", Assert.Single(l.Sections!).Body);
        Assert.Equal(["rieng", "q2"], l.Questions!.Select(q => q.Id));
        Assert.Equal("<p>Đề</p>", l.Questions![0].Prompt);
        var x = Assert.Single(r.Pack.Exams!);
        Assert.Equal(("ppt-gk251-chia-doi", 12.0), (x.Id, x.Minutes));
        Assert.Equal(["ppt-02.rieng", "ppt-02.q2"], x.QuestionRefs!);
        Assert.Equal(new Scoring(2, 0.5, -0.1), x.Scoring);
    }
}

public class MdWriteTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "Practice", "md-expected");

    private static List<PackQuestion> All(StudyPack p) =>
        [.. p.Units.SelectMany(u => u.Lessons).SelectMany(l => l.Questions ?? []), .. (p.Exams ?? []).SelectMany(x => x.Questions ?? [])];

    private static string Key(PackQuestion q) => string.Join("|", q.Type ?? "single", q.Answer?.ToJsonString(), string.Join(",", q.Answers ?? []),
        string.Join(",", q.Accept ?? []), q.Tolerance, q.Options?.Count, q.KeepOrder, q.Group, q.Tag);

    private static string Words(string html) => Text.CollapseJs(Text.PlainText(html), "");

    [Theory]
    [InlineData("vi-du")]
    [InlineData("types")]
    public void Same_markdown_as_typescript(string name)
    {
        var images = File.Exists(Path.Combine(Dir, $"parse-{name}.images.json"))
            ? JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, $"parse-{name}.images.json")))!.AsObject().ToDictionary(kv => kv.Key, kv => (string)kv.Value!)
            : null;
        var want = JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, $"write-{name}.json")))!;
        var (md, imgs) = StudyMarkdown.Write(StudyMarkdown.Parse(File.ReadAllText(Path.Combine(Dir, $"parse-{name}.md")), images).Pack);
        Assert.Equal((string)want["md"]!, md);
        Assert.Equal(want["images"]!.AsObject().Select(kv => kv.Key), imgs.Keys);
    }

    // Như markdown.test.ts: đọc, ghi, đọc lại giữ mọi câu (chạy trên mọi file ví dụ có mặt).
    [Theory]
    [InlineData("vi-du.md")]
    [InlineData("hcmut-ee1009-giua-ky.md")]
    [InlineData("hcmut-mt1009-giua-ky.md")]
    [InlineData("hcmut-mt1009-chia-doi.md")]
    public void Round_trip_keeps_every_question(string file)
    {
        if (Examples.Read(file) is not { } md) return;
        var a = StudyMarkdown.Parse(md);
        Assert.Empty(a.Errors);
        Assert.NotEmpty(All(a.Pack));
        var (output, images) = StudyMarkdown.Write(a.Pack);
        var b = StudyMarkdown.Parse(output, images);
        Assert.Empty(b.Errors);
        Assert.Equal(All(a.Pack).Select(Key), All(b.Pack).Select(Key));
        Assert.Equal(All(a.Pack).Select(q => Words(q.Prompt)), All(b.Pack).Select(q => Words(q.Prompt)));
        Assert.Equal(a.Pack.Course, b.Pack.Course);
        Assert.Equal(a.Pack.Units.Select(u => u.Title), b.Pack.Units.Select(u => u.Title));
    }

    [Fact]
    public void Ids_and_refs_survive_round_trip()
    {
        var md = string.Join("\n",
            "---", "mon: MT1009 X", "tac-gia: A", "---",
            "# Chương", "## Bài hai", "id: ppt-02", "### Câu 1", "id: rieng", "Đề", "- [x] a", "- [ ] b", "> g",
            "### Câu 2", "Đề hai", "- [x] a", "- [ ] b", "> g",
            "## Bài ba", "### Câu 1", "Đề ba", "= 2", "> g",
            "# Đề thi thử", "## GK251 chia đôi", "id: ppt-gk251", "Thời gian: 12 phút", "Câu: ppt-02.rieng, ppt-02.q2");
        var (output, _) = StudyMarkdown.Write(StudyMarkdown.Parse(md, validate: false).Pack);
        Assert.Contains("id: ppt-02", output);
        Assert.Contains("id: rieng", output);
        Assert.DoesNotContain("id: q2", output);
        Assert.DoesNotContain("id: bai-ba", output);
        Assert.Contains("Câu: ppt-02.rieng, ppt-02.q2", output);
        var b = StudyMarkdown.Parse(output, validate: false).Pack;
        Assert.Equal(["ppt-02", "bai-ba"], b.Units[0].Lessons.Select(l => l.Id));
        Assert.Equal(["rieng", "q2"], b.Units[0].Lessons[0].Questions!.Select(q => q.Id));
        Assert.Equal(("ppt-gk251", 12.0), (b.Exams![0].Id, b.Exams[0].Minutes));
        Assert.Equal(["ppt-02.rieng", "ppt-02.q2"], b.Exams[0].QuestionRefs!);
    }
}

// Chuỗi kỳ vọng sinh bằng chính markdown.ts (Node, 07/10/2026), lưu ở md-expected/.
public class MdToHtmlTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "Practice", "md-expected");

    public static TheoryData<int> Samples() => [.. Enumerable.Range(0, JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, "mdtohtml.json")))!.AsArray().Count)];

    [Theory]
    [MemberData(nameof(Samples))]
    public void Same_html_as_typescript(int i)
    {
        var s = JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, "mdtohtml.json")))![i]!;
        Assert.Equal((string)s["html"]!, StudyMarkdown.MdToHtml((string)s["md"]!, src => "data:" + src));
    }

    [Fact]
    public void Headings_only_for_pages()
    {
        Assert.Equal("<h2>Tiêu đề</h2><p>chữ</p>", StudyMarkdown.MdToHtml("## Tiêu đề\nchữ", headings: true));
        Assert.Equal("<h3><b>Nhỏ</b></h3>", StudyMarkdown.MdToHtml("### **Nhỏ**", headings: true));
    }

    [Theory]
    [InlineData("Phương pháp chia đôi", "x", "phuong-phap-chia-doi")]
    [InlineData("Đề GK251: câu 8–11", "x", "de-gk251-cau-8-11")]
    [InlineData("!", "bai", "bai")]
    public void Slug_like_typescript(string s, string fallback, string expected) => Assert.Equal(expected, StudyMarkdown.Slug(s, fallback));
}
