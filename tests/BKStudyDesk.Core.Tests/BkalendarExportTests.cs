using System.Text.RegularExpressions;
using SoHocTap.Data;
using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

/// <summary>Bảng thời khóa biểu dán vào BKalendar: đúng khuôn parser student_2024 của bkalendar/core.</summary>
public class BkalendarExportTests
{
    private static MybkClass C(string code, int day, int[] weeks, string start = "7:00", string end = "8:50", int lesson = 2, int lessons = 2) =>
        new(code, "Môn " + code, "L99", day, [.. weeks], start, end, "A1-001", lesson, lessons, null, 2026);

    private static MybkData Data(params MybkClass[] classes) =>
        new(0, new MybkStudent("A", "2000000", null), new MybkTerm("20261", "Học kỳ 1 Năm học 2026 - 2027"), [.. classes], [], [], [], null, null, null, null, null, null, null);

    [Fact]
    public void Table_matches_bkalendar_student_2024_shape()
    {
        var text = BkalendarExport.Text(Data(C("EE1009", 2, [35, 37, 38]), C("EE1010", 8, [39, 41], "13:00", "17:50", 8, 5)))!;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var header = Array.IndexOf(lines, BkalendarExport.Header);
        Assert.True(header >= 1);
        Assert.Matches(new Regex(@"Học kỳ ([123]) Năm học (\d+) - (\d+)"), lines[header - 1]);
        var rows = lines[(header + 1)..];
        Assert.All(rows, r => Assert.Equal(12, r.Split('\t').Length));
        var first = rows[0].Split('\t');
        Assert.Equal(["2", "2 - 3", "7:00 - 8:50"], first[6..9]);
        Assert.Equal("35|--|37|38|", first[11]);   // ô đầu là tuần đầu học kỳ (35)
        var sunday = rows[1].Split('\t');
        Assert.Equal("CN", sunday[6]);
        Assert.Equal("--|--|--|--|39|--|41|", sunday[11]);   // cùng mốc tuần 35
    }

    [Fact]
    public void Term_across_new_year_keeps_one_start()
    {
        var text = BkalendarExport.Text(Data(C("A", 3, [52, 53, 1, 2])))!;
        var weeks = text.Split('\n')[2].Split('\t')[11];
        Assert.Equal("52|53|01|02|", weeks);   // 2026 có 53 tuần ISO
    }

    [Fact]
    public void No_schedule_gives_null()
    {
        Assert.Null(BkalendarExport.Text(null));
        Assert.Null(BkalendarExport.Text(Data()));
        Assert.Null(BkalendarExport.Text(Data(C("A", 2, []))));
    }

    [Fact]
    public void Bad_time_is_left_out_not_breaking_the_paste()
    {
        // BKalendar throws on a time it cannot split, which drops the whole paste: the class is left out instead.
        var text = BkalendarExport.Text(Data(C("OK", 2, [35]), C("BAD", 3, [35], "", ""), C("BACK", 4, [35], "9:00", "8:00")))!;
        var rows = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)[2..];
        Assert.Equal("OK", Assert.Single(rows).Split('\t')[1]);
        Assert.Null(BkalendarExport.Text(Data(C("BAD", 3, [35], "x", "y"))));
    }

    [Fact]
    public void Time_is_written_as_H_mm()
    {
        var text = BkalendarExport.Text(Data(C("A", 2, [35], "07:05", "08:50")))!;
        Assert.Equal("7:05 - 8:50", text.Split('\n')[2].Split('\t')[8]);
    }

    [Fact]
    public void Summer_term_around_week_26_is_not_a_new_year()
    {
        var m = Data(C("A", 2, [26, 27, 28])) with { Term = new MybkTerm("20263", "Học kỳ 3 Năm học 2026 - 2027") };
        Assert.Equal("26|27|28|", BkalendarExport.Text(m)!.Split('\n')[2].Split('\t')[11]);
    }

    [Fact]
    public void Term_line_built_from_code_when_name_has_other_shape()
    {
        var m = Data(C("A", 2, [3])) with { Term = new MybkTerm("20262", "HK262") };
        Assert.Equal("20262 - Học kỳ 2 Năm học 2026 - 2027", BkalendarExport.Text(m)!.Split('\n')[0]);
    }
}
