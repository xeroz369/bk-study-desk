using SoHocTap.Core;
using SoHocTap.Shell;

namespace SoHocTap.Tests;

public class NotifyDigestTests
{
    private static long At(int day, int hour, int minute = 0) => VnTime.FromWall(new DateTime(2026, 10, day, hour, minute, 0));

    [Theory]
    [InlineData("07:00, 12:00, 19:00", "07:00, 12:00, 19:00")]
    [InlineData("19:00,7:00 ,12:00,7:00", "07:00, 12:00, 19:00")]
    [InlineData("  ", "")]
    public void Parse_NormalizesSortsAndDedupes(string text, string expected) =>
        Assert.Equal(expected, NotifyDigest.Format(NotifyDigest.Parse(text)!));

    [Theory]
    [InlineData("7h")]
    [InlineData("25:00")]
    [InlineData("07:60")]
    [InlineData("07:00;12:00")]
    public void Parse_RejectsBadText(string text) => Assert.Null(NotifyDigest.Parse(text));

    [Fact]
    public void GateOpen_NoSlotsMeansAlwaysOpen() => Assert.True(NotifyDigest.GateOpen(At(6, 13), [], lastDigest: At(6, 12, 59)));

    [Fact]
    public void GateOpen_OncePerSlot()
    {
        int[] slots = [7 * 60, 12 * 60, 19 * 60];
        Assert.True(NotifyDigest.GateOpen(At(6, 13), slots, lastDigest: At(6, 7, 5)));    // 12:00 đã tới, chưa gom
        Assert.False(NotifyDigest.GateOpen(At(6, 13), slots, lastDigest: At(6, 12, 5)));  // đã gom lúc 12:05
        Assert.True(NotifyDigest.GateOpen(At(7, 7, 1), slots, lastDigest: At(6, 19, 2))); // qua ngày: 07:00 hôm sau
        Assert.False(NotifyDigest.GateOpen(At(7, 6), slots, lastDigest: At(6, 19, 2)));   // trước 07:00, slot gần nhất là 19:00 hôm qua đã gom
    }

    [Fact]
    public void GateOpen_AppClosedThroughSlotCatchesUp() =>
        Assert.True(NotifyDigest.GateOpen(At(6, 15), [12 * 60], lastDigest: At(5, 12, 1)));

    [Fact]
    public void Hold_OnlyWhenFarFromDue()
    {
        Assert.True(NotifyDigest.Hold(due: At(7, 10), now: At(6, 13), immediateHours: 6));
        Assert.False(NotifyDigest.Hold(due: At(6, 18), now: At(6, 13), immediateHours: 6));
    }
}

public partial class RemindersTests
{
    [Fact]
    public void Pick_HeldItemIsNotMarkedAndStaysSeen()
    {
        var ledger = new System.Text.Json.Nodes.JsonObject();
        var (fresh, _) = Reminders.Pick([Item("h1", 20)], Stages, Now, ledger, eligible: _ => false);
        Assert.Empty(fresh);
        Assert.Contains("lms:h1", ledger["seen"]!.AsArray().Select(x => x!.ToString()));
        (fresh, _) = Reminders.Pick([Item("h1", 20)], Stages, Now, ledger);
        Assert.Single(fresh);   // tới giờ gom thì vẫn được nhắc, không bị coi là đã nhắc
    }
}
