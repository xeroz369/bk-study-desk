using SoHocTap.Core;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests;

/// <summary>Bảng Lịch (CalendarPresenter): mọi mốc chưa xong từ hôm nay trở đi và quá hạn trong cửa sổ 7 ngày; không cắt; buổi học không vào bảng này.</summary>
public class CalendarTests
{
    private static readonly long Now = Format.Now;
    private const long H = 3600, D = 86400;

    private static TimelineItem Item(string id, string kind, long time, bool done = false) =>
        new(id, kind, "Mốc " + id, "Giải tích 2", time, "Hạn nộp", null, SourceIds.Lms, Done: done);

    [Fact]
    public void Rows_OverdueFirst_ExcludesDoneAndClasses()
    {
        var rows = CalendarPresenter.Build([Item("a", "assign", Now + D), Item("late", "assign", Now - 2 * D), Item("done", "assign", Now + H, done: true),
            Item("cl", "class", Now + 2 * H), Item("ex", "exam", Now + 5 * D)], Now);
        Assert.Equal(["late", "a", "ex"], rows.Select(r => r.Id));
        Assert.True(rows[0].IsDanger);
    }

    [Fact]
    public void Rows_SwitchesShowClassesAndDone()
    {
        TimelineItem[] all = [Item("a", "assign", Now + D), Item("done", "assign", Now + H, done: true), Item("cl", "class", Now + 2 * H)];
        Assert.Equal(["done", "cl", "a"], CalendarPresenter.Build(all, Now, showClasses: true, showDone: true).Select(r => r.Id));
        Assert.Equal(["cl", "a"], CalendarPresenter.Build(all, Now, showClasses: true).Select(r => r.Id));
    }

    [Fact]
    public void Rows_UndatedKeptLast() =>
        Assert.Equal(["a", "u"], CalendarPresenter.Build([Item("u", "assign", TimelineItem.UndatedTime), Item("a", "assign", Now + D)], Now).Select(r => r.Id));

    [Fact]
    public void Rows_KeepAllFuture() =>
        Assert.Equal(90, CalendarPresenter.Build([.. Enumerable.Range(1, 90).Select(i => Item("a" + i, "assign", Now + i * D))], Now).Count);

    [Fact]
    public void Rows_GroupIsOverdueOrDay()
    {
        var rows = CalendarPresenter.Build([Item("late", "assign", Now - D), Item("a", "assign", Now + 3 * D)], Now);
        Assert.Equal(L.T("format.group.overdue"), rows[0].Group);
        Assert.NotEqual(rows[0].Group, rows[1].Group);
    }
}
