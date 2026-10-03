using SoHocTap.Sources.Lms;

namespace SoHocTap.Tests;

/// <summary>Ghép bài tập với mốc trên lịch hành động (LmsSource.CollectEventsAsync), suy ra bài đã nộp.</summary>
public class AssignPairingTests
{
    private const long Now = 1_760_000_000;

    [Fact]
    public void Match_PrefersCmid()
    {
        // LMS trường: instance của mốc = cmid của bài. Có cả mốc trùng id (bài khác) thì vẫn lấy theo cmid.
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
    [InlineData(199, false, true)]
    [InlineData(200, false, false)]     // đọc đủ 4 trang × 50: có thể còn nữa, không biết chắc
    [InlineData(10, true, false)]       // có mốc bài tập thiếu instance: không ghép được
    public void CalendarComplete(int count, bool missingInstance, bool expected) =>
        Assert.Equal(expected, AssignPairing.CalendarComplete(count, missingInstance));

    [Fact]
    public void InferDone_OnlyWhenCalendarCompleteAndDueInWindow()
    {
        Assert.True(AssignPairing.InferDone(Now + 86400, Now, calendarComplete: true));
        Assert.True(AssignPairing.InferDone(Now - AssignPairing.WindowBefore, Now, true));
        Assert.True(AssignPairing.InferDone(Now + AssignPairing.WindowAfter, Now, true));
        Assert.False(AssignPairing.InferDone(Now + 86400, Now, calendarComplete: false));
        Assert.False(AssignPairing.InferDone(Now - AssignPairing.WindowBefore - 1, Now, true));   // hạn ngoài khung đã đọc: không biết
        Assert.False(AssignPairing.InferDone(Now + AssignPairing.WindowAfter + 1, Now, true));
    }
}
