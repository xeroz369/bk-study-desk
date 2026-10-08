using System.Text.Json.Nodes;
using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

public sealed class ContentLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bk-content-" + Guid.NewGuid().ToString("N"));
    private string Content => Path.Combine(_root, "content");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private void Put(string rel, string text)
    {
        var p = Path.Combine(_root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, text);
    }

    private void Sample()
    {
        Put("content/manifest.json", """
            { "term": "HK261", "priority": ["ppt"],
              "pages": [{ "id": "lo-trinh", "title": "Lộ trình", "file": "content/pages/lo-trinh.md" }],
              "exams": [{ "id": "de-a", "file": "content/exams/de-a.md", "courseId": "ppt", "sources": [{ "file": "BKeL/GK251.pdf" }] }],
              "courses": [{ "id": "ppt", "code": "MT1009", "name": "Phương pháp tính",
                "scoring": { "count": 16, "right": 0.625, "wrong": -0.125 },
                "blueprint": { "minutes": 50, "units": [2], "scoring": { "count": 2, "right": 5, "wrong": -1 } },
                "exam": { "date": "2026-10-15", "time": "9g00" },
                "units": [{ "title": "Chương 1", "lessons": [
                  { "id": "ppt-02", "title": "Chia đôi", "file": "content/ppt/ppt-02.md", "sources": [{ "file": "slide", "pages": "20" }] },
                  { "id": "ppt-09", "title": "Chưa soạn" } ] }] }] }
            """);
        Put("content/ppt/ppt-02.md", string.Join("\n",
            "---", "mon: MT1009 Phương pháp tính", "---", "# Chương 1", "## Chia đôi", "id: ppt-02", "",
            "#### Cách làm", "Mỗi bước \\(x_n\\).", "<div class=\"warn\" onclick=\"x()\">Chú ý</div>", "",
            "### Câu 1 · Slide tr.36", "\\(f(0)<0\\) thì", "- [ ] a", "- [x] \\(r&lt;38\\)", "> giải", "",
            "### Câu 2", "Đề hai", "= Đúng", "> g"));
        Put("content/exams/de-a.md", string.Join("\n",
            "---", "mon: MT1009 Phương pháp tính", "---", "# Đề thi thử", "## Đề A", "id: de-a", "Thời gian: 12 phút · Đúng: 10/17 · Sai: -2/17",
            "Câu: ppt-02.q1, ppt-02.q2", "", "### Câu 1", "Đề riêng", "- [x] 1", "- [ ] 2", "> g"));
        Put("content/pages/lo-trinh.md", "## Lộ trình\n\nHọc **đều**.\n\n<p class=\"src\">nguồn</p>\n");
    }

    [Fact]
    public void Loads_manifest_lessons_exams_pages()
    {
        Sample();
        var study = new StudyRegistry();
        var warnings = ContentLibrary.Load(Content, study);
        Assert.Empty(warnings);
        var c = Assert.Single(study.Manifest.Courses);
        Assert.Equal(("MT1009", 50.0, 2), (c.Code, c.Blueprint!.Minutes, c.Blueprint.Units![0]));
        Assert.Equal(new ExamDate("2026-10-15", "9g00"), c.Exam);
        Assert.Equal(["ppt-02", "ppt-09"], c.Units[0].Lessons.Select(e => e.Id));
        Assert.Equal(["ppt"], study.Manifest.Priority!);

        var l = study.Lessons["ppt-02"];
        Assert.Equal(new SourceRef("slide", "20"), Assert.Single(l.Sources!));
        Assert.Equal("Cách làm", Assert.Single(l.Sections!).Title);
        Assert.Contains("<div class=\"warn\">Chú ý</div>", l.Sections![0].Html);
        Assert.Equal(["ppt-02.q1", "ppt-02.q2"], l.Questions!.Select(q => q.Id));
        Assert.Equal(["Đúng", "Sai"], l.Questions![1].Options);
        Assert.False(study.Lessons.ContainsKey("ppt-09"));

        var x = study.Exams["de-a"];
        Assert.Equal(("ppt", 12.0), (x.CourseId, x.Minutes));
        Assert.Equal(["ppt-02.q1", "ppt-02.q2"], x.QuestionIds!);
        Assert.Equal("de-a.q1", Assert.Single(x.Questions!).Id);
        Assert.Equal(new Scoring(3, 10.0 / 17, -2.0 / 17), x.Scoring);
        Assert.Equal(3, study.ExamQuestions(x).Count);

        var page = study.Pages["lo-trinh"];
        Assert.StartsWith("<h2>Lộ trình</h2><p>Học <b>đều</b>.</p>", page.Html);
        Assert.Equal("Lộ trình", Assert.Single(study.Manifest.Pages!).Title);
    }

    // Fingerprint tính trên HTML chưa lọc (như bản TS với content/*.js): lọc đổi "<" thành &lt; sẽ làm khác chữ băm.
    [Fact]
    public void Fingerprint_before_sanitize()
    {
        Sample();
        var study = new StudyRegistry();
        ContentLibrary.Load(Content, study);
        var q = study.Lessons["ppt-02"].Questions![0];
        var raw = StudyMarkdown.Parse(File.ReadAllText(Path.Combine(Content, "ppt", "ppt-02.md")), validate: false).Pack.Units[0].Lessons[0].Questions![0];
        Assert.Equal(Fingerprint.Of("single", raw.Prompt, raw.Options, null, null), q.Fp);
        Assert.Contains("&lt;", q.Prompt);   // hiển thị: đã lọc, "<" được escape
    }

    [Fact]
    public void Missing_file_is_warning_not_crash()
    {
        Sample();
        File.Delete(Path.Combine(Content, "ppt", "ppt-02.md"));
        var study = new StudyRegistry();
        var warnings = ContentLibrary.Load(Content, study);
        Assert.Contains(warnings, w => w.Contains("ppt-02.md"));
        Assert.Single(study.Manifest.Courses);
    }

    // Nội dung riêng (content/ của người dùng): .ids.json do content-to-md.mjs ghi bằng bản TS trên file .js gốc.
    [Fact]
    public void Private_content_keeps_ids_and_fingerprints()
    {
        var dir = Environment.GetEnvironmentVariable("BK_CONTENT");
        if (string.IsNullOrEmpty(dir)) return;
        var ids = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, ".ids.json")))!;
        var study = new StudyRegistry();
        var warnings = ContentLibrary.Load(dir, study);
        Assert.Empty(warnings);
        var got = study.LessonQuestions().Concat(study.Exams.Values.SelectMany(x => x.Questions ?? [])).ToDictionary(q => q.Id, q => q.Fp);
        var want = ids["questions"]!.AsObject().ToDictionary(kv => kv.Key, kv => (string?)kv.Value);
        Assert.Equal(want.Keys.Order(StringComparer.Ordinal), got.Keys.Order(StringComparer.Ordinal));
        var diff = want.Where(kv => got[kv.Key] != kv.Value).Select(kv => kv.Key).ToList();
        Assert.True(diff.Count == 0, "khác fingerprint: " + string.Join(", ", diff));
        foreach (var (id, sections) in ids["sections"]!.AsObject())
            Assert.Equal(sections!.AsArray().Count, study.Lessons[id].Sections?.Count ?? 0);
        foreach (var (id, x) in ids["exams"]!.AsObject())
        {
            Assert.Equal(x!["refs"]!.AsArray().Select(r => (string)r!), study.Exams[id].QuestionIds ?? []);
            Assert.Equal((double)x["minutes"]!, study.Exams[id].Minutes);
            if (x["scoring"] is JsonObject s)
                Assert.Equal(((double)s["right"]!, (double)s["wrong"]!), (study.Exams[id].Scoring!.Right, study.Exams[id].Scoring!.Wrong));
        }
    }
}
