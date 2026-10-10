using SoHocTap.Data;
using SoHocTap.Presentation;
using SoHocTap.Sources.Lms;
using Kind = SoHocTap.Sources.Lms.LmsSource.FileKind;

namespace BKStudyDesk.Core.Tests;

/// <summary>Cửa sổ Tải tài liệu nhiều môn (discussion #42): cây học kỳ, môn, mục; chỉ đếm file chưa có; cảnh báo khi lớn.</summary>
public class DownloadTests
{
    private static readonly HashSet<Kind> All = [Kind.Pdf, Kind.Slide, Kind.Other];

    private static LmsCourse C(long id, string subject, string term, string? part = null) => new(id, subject, subject, "", part, term, "", "", "");

    private static LmsSource.SectionInfo S(int i, params (Kind Kind, long Bytes, bool Have)[] files) =>
        new(i, "Mục " + i, [.. files.Select(f => new LmsSource.FileStat(f.Kind, f.Bytes, f.Have))]);

    private static CourseRow Ready(LmsCourse c, bool wanted, params LmsSource.SectionInfo[] sections)
    {
        var row = new CourseRow(c, () => All) { Checked = wanted };
        row.Begin();
        row.Loaded(sections);
        return row;
    }

    [Fact]
    public void Build_puts_current_term_first_then_newest()
    {
        var terms = DownloadPlan.Build([C(1, "Giải tích 1", "251"), C(2, "Giải tích 2", "261"), C(3, "Vật lý 1", "252"), C(4, "Phương pháp tính", "261")], "261", () => All);
        Assert.Equal(["261", "252", "251"], terms.Select(t => t.Term));
        Assert.True(terms[0].Current);
        Assert.Equal(["Giải tích 2", "Phương pháp tính"], terms[0].Courses.Select(c => c.Name));
    }

    [Fact]
    public void Course_name_keeps_part() => Assert.Equal("Vật lý 1, Thí nghiệm", new CourseRow(C(1, "Vật lý 1", "261", "Thí nghiệm"), () => All).Name);

    [Fact]
    public void Wanted_course_keeps_default_sections_with_missing_files()
    {
        var row = Ready(C(1, "PPT", "261"), wanted: true, S(0, (Kind.Pdf, 10, true)), S(1, (Kind.Pdf, 20, false)));
        Assert.Equal([false, true], row.Sections.Select(s => s.Selected));
        Assert.Null(row.Checked);   // một phần
        Assert.Equal(20, row.Pending.Sum(f => f.Bytes));
    }

    [Fact]
    public void Unwanted_course_loads_with_nothing_picked()
    {
        var row = Ready(C(1, "PPT", "252"), wanted: false, S(0, (Kind.Pdf, 20, false)));
        Assert.False(row.Checked);
        Assert.Empty(row.Pending);
    }

    [Fact]
    public void Ticking_ready_course_picks_all_sections_but_counts_only_missing_files()
    {
        var row = Ready(C(1, "PPT", "261"), wanted: false, S(0, (Kind.Pdf, 10, true)), S(1, (Kind.Slide, 20, false), (Kind.Pdf, 5, false)));
        row.Checked = true;
        Assert.True(row.Checked);
        Assert.Equal(2, row.Pending.Count);
        Assert.Equal(25, row.Pending.Sum(f => f.Bytes));
    }

    [Fact]
    public void Course_without_files_cannot_be_ticked()
    {
        var row = Ready(C(1, "PPT", "261"), wanted: true);
        row.Checked = true;
        Assert.False(row.Checked);
    }

    [Fact]
    public void Unread_course_remembers_wish_until_read()
    {
        var row = new CourseRow(C(1, "PPT", "252"), () => All);
        Assert.Equal(LoadState.Unread, row.State);
        row.Checked = true;
        Assert.True(row.Checked);
        Assert.Empty(row.Pending);   // chưa đọc thì chưa có gì để tải
    }

    [Fact]
    public void Failed_read_keeps_wish_and_shows_reason()
    {
        var row = new CourseRow(C(1, "PPT", "252"), () => All) { Checked = true };
        row.Begin();
        row.Failed("mất mạng");
        Assert.Equal(LoadState.Failed, row.State);
        Assert.True(row.Checked);
        Assert.Contains("mất mạng", row.Meta);
    }

    [Fact]
    public void Term_is_tristate_over_courses_and_ticks_them_all()
    {
        var a = Ready(C(1, "A", "261"), wanted: true, S(0, (Kind.Pdf, 1, false)));
        var b = Ready(C(2, "B", "261"), wanted: false, S(0, (Kind.Pdf, 1, false)));
        var term = new TermRow("261", true, [a, b]);
        Assert.Null(term.Checked);
        term.Checked = true;
        Assert.True(a.Checked);
        Assert.True(b.Checked);
        Assert.True(term.Checked);
        term.Checked = false;
        Assert.False(term.Checked);
    }

    [Fact]
    public void Term_ignores_courses_without_documents()
    {
        var a = Ready(C(1, "A", "261"), wanted: true, S(0, (Kind.Pdf, 1, false)));
        var empty = Ready(C(2, "B", "261"), wanted: true);
        Assert.True(new TermRow("261", true, [a, empty]).Checked);
        Assert.False(new TermRow("261", true, [empty]).Checked);
    }

    [Fact]
    public void Kind_filter_changes_what_is_pending()
    {
        var kinds = new HashSet<Kind>(All);
        var row = new CourseRow(C(1, "PPT", "261"), () => kinds) { Checked = true };
        row.Begin();
        row.Loaded([S(0, (Kind.Slide, 50, false), (Kind.Pdf, 5, false))]);
        kinds.Remove(Kind.Slide);
        Assert.Equal(5, row.Pending.Sum(f => f.Bytes));
    }

    [Theory]
    [InlineData(2048L, "2 KB")]
    [InlineData(5L * 1024 * 1024, "5,0 MB")]
    [InlineData(3L * 1024 * 1024 * 1024 / 2, "1,5 GB")]
    public void Size_reads_in_gigabytes_when_large(long bytes, string expected) => Assert.Equal(expected, SoHocTap.Ui.Format.Size(bytes));

    [Theory]
    [InlineData(0, 0L, null, DownloadPlan.Verdict.Nothing)]
    [InlineData(3, 100L, 1000L, DownloadPlan.Verdict.Ok)]
    [InlineData(3, 2000L, 1000L, DownloadPlan.Verdict.NoSpace)]
    [InlineData(3, 600L, null, DownloadPlan.Verdict.Large)]
    [InlineData(250, 100L, 10000L, DownloadPlan.Verdict.Large)]
    public void Judge_warns_on_large_and_blocks_without_space(int files, long bytes, long? free, DownloadPlan.Verdict expected) =>
        Assert.Equal(expected, DownloadPlan.Judge(files, bytes, free, warnBytes: 500, warnFiles: 200));
}
