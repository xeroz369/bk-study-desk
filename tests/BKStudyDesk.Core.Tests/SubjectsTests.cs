using System.Text;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests;

/// <summary>Trang Môn học (SubjectsPresenter): môn nào hiện, khớp mốc, điểm, tài liệu.</summary>
public class SubjectsTests
{
    private static LmsCourse Course(long id, string subject, string term = "HK261") =>
        new(id, subject + " (L01)", subject, "MT1005_" + id, null, term, "Cô A", "https://lms.example/course/" + id, subject);

    private static LmsData Lms(IEnumerable<LmsCourse> courses, List<LmsGradeBook>? grades = null) =>
        new(0, "SV", "HK261", [.. courses], [], [], [], [], grades ?? []);

    private static JsonArray Scan(params (string Name, int Files)[] dirs) =>
        [.. dirs.Select(d => (JsonNode)new JsonObject { ["name"] = d.Name, ["files"] = d.Files })];

    [Fact]
    public void Recent_rows_mark_read_and_filter_unread()
    {
        var recent = JsonNode.Parse("""{"files":[{"name":"a.pdf","folder":".","path":"M/a.pdf","size":1,"modified":10},{"name":"b.pdf","folder":"C1","path":"M/C1/b.pdf","size":2,"modified":20}]}""")!.AsObject();
        var marks = new SoHocTap.Files.ReadMarks();
        marks.Set("M/a.pdf", 10, read: true);
        var rows = SubjectsPresenter.RecentRows(recent, marks);
        Assert.True(rows[0].Read);
        Assert.Equal("", rows[0].ReadText);
        Assert.False(rows[1].Read);
        Assert.Equal(["b.pdf"], SubjectsPresenter.RecentRows(recent, marks, unreadOnly: true).Select(r => r.Name));
    }

    [Fact]
    public void Subjects_LocalOnly_NotOnLms()
    {
        var rows = SubjectsPresenter.Rows(Lms([]), Scan(("Vật lý", 3)));
        var r = Assert.Single(rows);
        Assert.Empty(r.Courses);
        Assert.False(r.OnLms);
    }

    [Fact]
    public void Subjects_NfdNfc_OneRow()
    {
        var nfd = "Giải tích 2".Normalize(NormalizationForm.FormD);
        var rows = SubjectsPresenter.Rows(Lms([Course(1, "Giải tích 2")]), Scan((nfd, 5)));
        var r = Assert.Single(rows);
        Assert.Equal(5, r.Files);
        Assert.True(r.Current);
    }

    [Fact]
    public void Subjects_Mine_ExactSubject()
    {
        var row = SubjectsPresenter.Rows(Lms([]), Scan(("Giải tích 1", 1)))[0];
        TimelineItem Exam(string subject) => new("e", "exam", "Thi", subject, Format.Now + 86400, "", null, SourceIds.Mybk);
        Assert.True(SubjectsPresenter.Mine(Exam("Giải tích 1"), row));
        Assert.False(SubjectsPresenter.Mine(Exam("Giải tích 12"), row));
    }

    [Fact]
    public void Subjects_PastTerm_ShownAfterCurrent()
    {
        var lms = Lms([Course(1, "Hóa đại cương", "HK252"), Course(2, "Giải tích 2")]);
        var rows = SubjectsPresenter.Rows(lms, Scan());
        Assert.Equal(["Giải tích 2", "Hóa đại cương"], rows.Select(r => r.Name));
        Assert.False(rows[1].Current);
        Assert.Equal("HK252", rows[1].Term);
    }

    [Fact]
    public void Crumbs_from_subject_folder_down()
    {
        var c = SubjectsPresenter.Crumbs("Môn học/Giải tích 2", "Môn học/Giải tích 2/Tài liệu LMS/Chương 1");
        Assert.Equal(["Giải tích 2", "Tài liệu LMS", "Chương 1"], c.Select(x => x.Name));
        Assert.Equal("Môn học/Giải tích 2/Tài liệu LMS", c[1].Path);
        Assert.Single(SubjectsPresenter.Crumbs("Môn học/Giải tích 2", "Môn học/Giải tích 2"));
        Assert.Equal("Giải tích 22", Assert.Single(SubjectsPresenter.Crumbs("Môn học/Giải tích 2", "Môn học/Giải tích 22")).Name);   // không nhận nhầm tiền tố
    }

    [Fact]
    public void Subjects_grouped_by_latest_term_current_first_then_local()
    {
        var lms = Lms([Course(1, "Hóa đại cương", "HK241"), Course(2, "Giải tích 2"), Course(3, "Vật lý 1", "HK252"), Course(4, "Vật lý 1", "HK241"),
            Course(5, "Lịch sử Đảng", "HK253")]);
        var groups = SubjectsPresenter.Grouped(SubjectsPresenter.Rows(lms, Scan(("Ghi chú riêng", 2))));
        Assert.Equal(["Học kỳ 1, 2026-2027 (đang học)", "Giải tích 2", "Học kỳ hè, 2025-2026", "Lịch sử Đảng", "Học kỳ 2, 2025-2026", "Vật lý 1",
            "Học kỳ 1, 2024-2025", "Hóa đại cương", "Chỉ có thư mục trên máy", "Ghi chú riêng"], groups.Select(x => x.ToString()));
        Assert.IsType<SubjectTermHeader>(groups[0]);
    }

    [Fact]
    public void Subjects_Filter_Folded()
    {
        var rows = SubjectsPresenter.Rows(Lms([Course(1, "Giải tích 2"), Course(2, "Kỹ thuật số")]), Scan());
        Assert.Equal(["Giải tích 2"], SubjectsPresenter.Filter(rows, "giai tich").Select(r => r.Name));
        Assert.Equal(2, SubjectsPresenter.Filter(rows, " ").Count);
    }

    [Fact]
    public void Subjects_Grades_TotalFlag()
    {
        var book = new LmsGradeBook(1, "Giải tích 2", null, [new("Quiz 1", "mod", 9, null, 10, "90 %", null), new("Tổng", "course", 8.5, null, 10, "85 %", null)]);
        var lms = Lms([Course(1, "Giải tích 2")], [book]);
        var g = SubjectsPresenter.Grades(lms, SubjectsPresenter.Rows(lms, Scan())[0]);
        Assert.Equal(2, g.Count);
        Assert.False(g[0].Total);
        Assert.True(g[1].Total);
        Assert.Equal("Lý thuyết", g[0].Part);
    }

    [Fact]
    public void Subjects_FileRows_DirsFirst()
    {
        var dir = new JsonObject
        {
            ["dirs"] = new JsonArray(new JsonObject { ["name"] = "Slide", ["count"] = 4 }),
            ["files"] = new JsonArray(new JsonObject { ["name"] = "a.pdf", ["ext"] = ".pdf", ["size"] = 2048L, ["modified"] = 0L }),
        };
        var rows = SubjectsPresenter.FileRows(dir);
        Assert.Equal(["Slide", "a.pdf"], rows.Select(r => r.Name));
        Assert.True(rows[0].Dir);
        Assert.Equal("Thư mục", rows[0].Kind);
    }
}
