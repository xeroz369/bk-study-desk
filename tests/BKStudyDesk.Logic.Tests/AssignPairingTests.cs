using SoHocTap.Sources.Lms;

namespace SoHocTap.Tests;

public class AssignPairingTests
{
    private const long Now = 1_760_000_000;

    [Fact]
    public void Match_PrefersCmid()
    {
        Assert.Equal(900, AssignPairing.MatchInstance(assignId: 12, cmid: 900, eventInstances: new HashSet<long> { 900, 12 }));
    }

    [Fact]
    public void Match_FallsBackToId()
    {
        Assert.Equal(12, AssignPairing.MatchInstance(12, 900, new HashSet<long> { 12 }));
        Assert.Equal(12, AssignPairing.MatchInstance(12, null, new HashSet<long> { 12 }));
    }

    [Fact]
    public void Match_NoEvent() => Assert.Null(AssignPairing.MatchInstance(12, 900, new HashSet<long> { 5, 6 }));

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(499, false, true)]
    [InlineData(500, false, false)]
    [InlineData(10, true, false)]
    public void CalendarComplete(int count, bool missingInstance, bool expected) =>
        Assert.Equal(expected, AssignPairing.CalendarComplete(count, missingInstance));

    [Fact]
    public void InferDone_OnlyWhenCalendarCompleteAndDueInWindow()
    {
        Assert.True(AssignPairing.InferDone(Now + 86400, Now, calendarComplete: true));
        Assert.True(AssignPairing.InferDone(Now - AssignPairing.WindowBefore, Now, true));
        Assert.True(AssignPairing.InferDone(Now + 300 * 86400, Now, true));
        Assert.False(AssignPairing.InferDone(Now + 86400, Now, calendarComplete: false));
        Assert.False(AssignPairing.InferDone(Now - AssignPairing.WindowBefore - 1, Now, true));
    }
}
