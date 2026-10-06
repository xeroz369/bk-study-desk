using SoHocTap.Shell;

namespace SoHocTap.Tests;

/// <summary>Phần thứ hai của RemindersTests (cùng class với NothingNewTests.cs): mục đã nộp không vào danh sách nhắc.</summary>
public partial class RemindersTests
{
    private static DueItem Submitted(string id, double hoursLeft, bool done) => Item(id, hoursLeft) with { Done = done };

    [Fact]
    public void Pick_SkipsDoneItems()
    {
        var (fresh, _) = Reminders.Pick([Submitted("d1", 1, done: true)], Stages, Now, []);
        Assert.Empty(fresh);
    }

    [Fact]
    public void Pick_StillPicksNotDoneItems()
    {
        var (fresh, _) = Reminders.Pick([Submitted("d1", 1, done: false), Submitted("d2", 1, done: true)], Stages, Now, []);
        var only = Assert.Single(fresh);
        Assert.Equal("d1", only.Item.Id);
        Assert.Equal(2, only.Hours);
    }
}
