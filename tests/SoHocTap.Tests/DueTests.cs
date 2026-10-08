using SoHocTap.Ui;

namespace SoHocTap.Tests;

public class DueTests
{
    private const long H = 3600;
    // Mốc cố định 2026-10-05 00:00 giờ VN = 2026-10-04 17:00 UTC, để "hôm nay" và "ngày mai" không phụ thuộc giờ chạy test.
    private const long Midnight = 1791133200;

    [Fact]
    public void Of_BelowSixHoursIsUrgent() => Assert.Equal(Urgency.Urgent, Due.Of(5 * H + 59 * 60, 0, false, 6, 24));

    [Fact]
    public void Of_ExactlySixHoursIsSoon() => Assert.Equal(Urgency.Soon, Due.Of(6 * H, 0, false, 6, 24));

    [Fact]
    public void Of_BelowTwentyFourHoursIsSoon() => Assert.Equal(Urgency.Soon, Due.Of(23 * H + 59 * 60, 0, false, 6, 24));

    [Fact]
    public void Of_ExactlyTwentyFourHoursIsNormal() => Assert.Equal(Urgency.Normal, Due.Of(24 * H, 0, false, 6, 24));

    [Theory]
    [InlineData(24, 24, true)]
    [InlineData(30, 24, true)]
    [InlineData(6, 24, false)]
    [InlineData(0, 24, false)]
    [InlineData(6, 0, false)]
    public void SoonHidden_WhenUrgentNotBelowSoon(int urgent, int soon, bool hidden)
    {
        Assert.Equal(hidden, Due.SoonHidden(urgent, soon));
        // Đúng như Due.Of: mốc vàng ẩn thì không giờ nào cho ra Soon.
        if (hidden) Assert.DoesNotContain(Enumerable.Range(0, 48).Select(h => Due.Of(h * H + 1, 0, false, urgent, soon)), u => u == Urgency.Soon);
    }

    [Fact]
    public void Of_PastAndNotDoneIsOverdue() => Assert.Equal(Urgency.Overdue, Due.Of(100, 101, false, 6, 24));

    [Fact]
    public void Of_DueExactlyNowIsUrgentNotOverdue() => Assert.Equal(Urgency.Urgent, Due.Of(100, 100, false, 6, 24));

    [Fact]
    public void Of_DoneIsNoneEvenWhenPast() => Assert.Equal(Urgency.None, Due.Of(100, 5000, true, 6, 24));

    [Fact]
    public void Of_ZeroHoursTurnsLevelOff()
    {
        Assert.Equal(Urgency.Soon, Due.Of(H, 0, false, 0, 24));
        Assert.Equal(Urgency.Normal, Due.Of(H, 0, false, 0, 0));
        Assert.Equal(Urgency.Urgent, Due.Of(H, 0, false, 6, 0));
        Assert.Equal(Urgency.Overdue, Due.Of(0, 1, false, 0, 0));
    }

    [Fact]
    public void LeftText_Past() => Assert.Equal("format.until.passed", Due.LeftText(Midnight + 100, Midnight + 200));

    [Fact]
    public void LeftText_UnderAnHourIsMinutesAtLeastOne()
    {
        Assert.Equal("format.until.minutes|45", Due.LeftText(Midnight + 45 * 60, Midnight));
        Assert.Equal("format.until.minutes|1", Due.LeftText(Midnight + 10, Midnight));
    }

    [Fact]
    public void LeftText_SameDayIsRoundedHours() => Assert.Equal("format.until.hours|4", Due.LeftText(Midnight + 10 * H, Midnight + 6 * H));

    [Fact]
    public void LeftText_NextCalendarDayIsTomorrow() => Assert.Equal("format.until.tomorrow", Due.LeftText(Midnight + 30 * H, Midnight + 20 * H));

    [Fact]
    public void LeftText_LaterIsCalendarDays() => Assert.Equal("format.until.days|5", Due.LeftText(Midnight + 5 * 24 * H + 3 * H, Midnight + 1 * H));

    [Fact]
    public void SpanText_PicksUnit()
    {
        Assert.Equal("notify.daysHours|2|3", Due.SpanText(2 * 86400 + 3 * H));
        Assert.Equal("notify.hours|4", Due.SpanText(4 * H + 59));
        Assert.Equal("notify.minutes|1", Due.SpanText(10));
    }
}
