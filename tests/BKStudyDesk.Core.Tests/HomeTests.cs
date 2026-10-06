using System.Globalization;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests;

/// <summary>Trang Hôm nay (HomePresenter): mục nào hiện, thứ tự, không cắt. Giờ là giờ thật (TimelineItem đọc Format.Now).</summary>
public class HomeTests
{
    private static readonly long Now = Format.Now;
    private const long H = 3600, D = 86400;

    private static TimelineItem Assign(string id, long time, bool done = false) =>
        new(id, "assign", "Bài " + id, "Giải tích 2", time, "Hạn nộp", "https://lms.example/" + id, SourceIds.Lms, Done: done);

    private static HomeModel Build(IReadOnlyList<TimelineItem>? timeline = null, IReadOnlyList<LmsAnnouncement>? news = null, HomePresenter.MybkPart? mybk = null, bool hasLms = true) =>
        HomePresenter.Build(timeline ?? [], hasLms, news ?? [], mybk, Now);

    [Fact]
    public void Build_NoData_SignedOutOnly()
    {
        var m = Build(hasLms: false);
        Assert.False(m.HasLms);
        Assert.False(m.HasMybk);
        Assert.Empty(m.Todo);
        Assert.Empty(m.Upcoming);
        Assert.Empty(m.News);
        Assert.Empty(m.Registrations);
    }

    [Fact]
    public void Todo_OverdueFirst_ExcludesDoneAndFar()
    {
        var m = Build([Assign("soon", Now + 3 * H), Assign("late", Now - 2 * D), Assign("done", Now + H, done: true), Assign("far", Now + 20 * D)]);
        Assert.Equal(["late", "soon"], m.Todo.Select(t => t.Id));
        Assert.Equal(Urgency.Overdue, m.Todo[0].Level);
        Assert.Equal("https://lms.example/soon", m.Todo[1].Url);
    }

    [Fact]
    public void Todo_HidesOverdueOlderThanWindow() => Assert.Empty(Build([Assign("old", Now - 8 * D)]).Todo);

    [Fact]
    public void Build_KeepsAllTodo() =>
        Assert.Equal(40, Build([.. Enumerable.Range(1, 40).Select(i => Assign("a" + i, Now + i * 6 * H))]).Todo.Count);

    [Fact]
    public void Upcoming_ClassesTodayTomorrow_ExamsIn14Days()
    {
        var today = VnTime.ToWall(Now).Date;
        MybkClass Class(string code, DateTime d) =>
            new(code, code, null, d.DayOfWeek == DayOfWeek.Sunday ? 8 : (int)d.DayOfWeek + 1, [ISOWeek.GetWeekOfYear(d)], "07:00", "09:50", "H1-201", 1, 3, null, ISOWeek.GetYear(d));
        MybkExam Exam(string code, DateTime d) => new(code, code, "GK", d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), "07:00", 50, "H6-205", null);
        var mybk = new HomePresenter.MybkPart(
            [Class("c0", today), Class("c1", today.AddDays(1)), Class("c2", today.AddDays(2))],
            [Exam("e5", today.AddDays(5)), Exam("e20", today.AddDays(20))], []);
        var m = Build(mybk: mybk);
        Assert.True(m.HasMybk);
        Assert.Equal(["c0", "c1", "e5"], m.Upcoming.Select(u => u.Title));
    }

    [Fact]
    public void LmsOnly_HasMybkFalse_TodoStillFilled()
    {
        var m = Build([Assign("a", Now + H)]);
        Assert.False(m.HasMybk);
        Assert.Single(m.Todo);
    }

    [Fact]
    public void News_Last7DaysNewestFirst()
    {
        LmsAnnouncement N(string id, long t) => new(id, "news", "Giải tích 2", "Tin tức", "Tin " + id, null, t, null);
        var m = Build(news: [N("old", Now - 9 * D), N("a", Now - 2 * D), N("b", Now - H)]);
        Assert.Equal(["Tin b", "Tin a"], m.News.Select(n => n.Title));
    }

    [Fact]
    public void Registrations_OpenFirst()
    {
        var mybk = new HomePresenter.MybkPart([], [], [new("later", "Đợt sau", Now + 10 * D, Now + 12 * D), new("open", "Đợt mở", Now - D, Now + D)]);
        var m = Build(mybk: mybk);
        Assert.Equal(["open", "later"], m.Registrations.Select(r => r.Code));
        Assert.True(m.Registrations[0].Open);
    }
}
