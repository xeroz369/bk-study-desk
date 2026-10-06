using SoHocTap.Data;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests;

/// <summary>Thời khóa biểu tuần, lịch thi, xuất .ics.</summary>
public class WeekTests
{
    private static readonly DateTime Monday = new(2026, 10, 12);   // tuần ISO 42

    private static MybkData Data(params MybkClass[] schedule) =>
        new(0, new MybkStudent("SV", "2310000", null), new MybkTerm("20261", "HK261"), [.. schedule], [], [], [], null, null, null, null, null, null, null);

    private static MybkClass Class(string name, int day, string start, string end, params int[] weeks) =>
        new("MT" + day, name, "L01", day, [.. weeks], start, end, "H1", 1, 3, "Cô A", 2026);

    [Fact]
    public void Rows_SortedByDayThenMinute_NotText()
    {
        var rows = WeekPresenter.Rows(Data(Class("Chiều", 2, "13:00", "15:00", 42), Class("Sáng", 2, "7:00", "9:00", 42), Class("Thứ ba", 3, "7:00", "9:00", 42)), [], Monday);
        Assert.Equal(["Sáng", "Chiều", "Thứ ba"], rows.Select(r => r.Name));
    }

    [Fact]
    public void Rows_NotThisWeek_Dimmed_CustomIncluded()
    {
        var custom = new CustomEvent { Id = "c1", Title = "Học bù", Date = "2026-10-14", Start = "18:00", Kind = CustomEvents.KindMakeup };
        var outside = new CustomEvent { Id = "c2", Title = "Tuần khác", Date = "2026-10-30", Start = "18:00" };
        var rows = WeekPresenter.Rows(Data(Class("Giải tích 2", 2, "7:00", "9:00", 41)), [custom, outside], Monday);
        Assert.False(rows.Single(r => r.Name == "Giải tích 2").InWeek);
        var c = Assert.Single(rows, r => r.CustomId is not null);
        Assert.Equal("c1", c.CustomId);
        Assert.Equal(4, c.Order);   // Thứ tư
    }

    [Fact]
    public void NoSlot_UnfixedDayOrTime() =>
        Assert.Equal(["Sinh hoạt", "Lỗi giờ"], WeekPresenter.NoSlot(Data(Class("Sinh hoạt", 0, "", "", 42), Class("Lỗi giờ", 3, "abc", "9:00", 42), Class("Ổn", 4, "7:00", "9:00", 42))));

    [Fact]
    public void Ics_SkipsBadTime_ExportsCustom()
    {
        var custom = new CustomEvent { Id = "c1", Title = "Họp nhóm", Date = "2026-10-14", Start = "18:00" };
        var (events, skipped) = WeekPresenter.Ics(Data(Class("Lỗi giờ", 3, "abc", "9:00", 42)), [custom], Monday);
        Assert.Equal(["Lỗi giờ"], skipped);
        Assert.Contains(events, e => e.Summary == "Họp nhóm");
    }

    [Fact]
    public void Exams_OrderedAndPastFlag()
    {
        TimelineItem Exam(string name, long t) => new("e" + name, "exam", name, name, t, "H6", null, "mybk");
        var rows = WeekPresenter.Exams([Exam("B", Format.Now + 86400), Exam("A", Format.Now - 86400)], Format.Now);
        Assert.Equal(["A", "B"], rows.Select(r => r.Subject));
        Assert.True(rows[0].Past);
    }
}
