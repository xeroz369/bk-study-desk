using SoHocTap.Ui;

namespace SoHocTap.Tests;

public class WeekMathTests
{
    private static readonly DateTime Monday = new(2026, 9, 28);

    [Fact]
    public void Position_ExactMinute()
    {
        var at = WeekMath.NowLine(Monday, new DateTime(2026, 10, 3, 9, 41, 59), 7 * 60, 17 * 60, 1.0, 10);
        Assert.NotNull(at);
        Assert.Equal((5, 10 + 161.0), (at.Value.Column, at.Value.Y));
    }

    [Fact]
    public void Position_ScalesWithPx()
    {
        var at = WeekMath.NowLine(Monday, new DateTime(2026, 9, 28, 8, 30, 0), 7 * 60, 12 * 60, 0.7, 10);
        Assert.NotNull(at);
        Assert.Equal(0, at.Value.Column);
        Assert.Equal(10 + 90 * 0.7, at.Value.Y, 6);
    }

    [Theory]
    [InlineData(2026, 9, 27, 10, 0)]
    [InlineData(2026, 10, 5, 10, 0)]
    public void OutsideWeek_Null(int y, int m, int d, int h, int min) =>
        Assert.Null(WeekMath.NowLine(Monday, new DateTime(y, m, d, h, min, 0), 7 * 60, 17 * 60, 1, 10));

    [Fact]
    public void Sunday_IsLastColumn() =>
        Assert.Equal(6, WeekMath.NowLine(Monday, new DateTime(2026, 10, 4, 12, 0, 0), 7 * 60, 17 * 60, 1, 10)?.Column);

    [Theory]
    [InlineData(6, 59, false)]
    [InlineData(7, 0, true)]
    [InlineData(17, 0, true)]
    [InlineData(17, 1, false)]
    [InlineData(23, 30, false)]
    public void OutsideHours_Null(int h, int min, bool shown) =>
        Assert.Equal(shown, WeekMath.NowLine(Monday, new DateTime(2026, 9, 30, h, min, 0), 7 * 60, 17 * 60, 1, 10) is not null);

    [Fact]
    public void MondayWithTime_StillMatchesDate() =>
        Assert.Equal(2, WeekMath.NowLine(Monday.AddHours(13), new DateTime(2026, 9, 30, 8, 0, 0), 7 * 60, 17 * 60, 1, 10)?.Column);

    [Theory]
    [InlineData(0, 0, 60_050)]
    [InlineData(30, 0, 30_050)]
    [InlineData(59, 999, 51)]
    public void UntilNextMinute(int sec, int ms, double expectedMs) =>
        Assert.Equal(expectedMs, WeekMath.UntilNextMinute(new DateTime(2026, 10, 3, 9, 41, sec, ms)).TotalMilliseconds);

    [Fact]
    public void FullDay_EveningLineShows()
    {
        var at = WeekMath.NowLine(Monday, new DateTime(2026, 10, 3, 19, 38, 0), 0, 24 * 60, 1, 10);
        Assert.Equal((5, 10 + 19 * 60 + 38.0), at);
    }

    [Fact]
    public void Scroll_ToFirstClassWhenNowVisible() =>
        Assert.Equal(7 * 60, WeekMath.ScrollAnchor(Monday, new DateTime(2026, 9, 30, 10, 0, 0), 7 * 60 + 30, 600));

    [Fact]
    public void Scroll_ToNowWhenOutsideView() =>
        Assert.Equal(18 * 60, WeekMath.ScrollAnchor(Monday, new DateTime(2026, 10, 3, 19, 38, 0), 7 * 60, 240));

    [Fact]
    public void Scroll_OtherWeekIgnoresNow() =>
        Assert.Equal(7 * 60, WeekMath.ScrollAnchor(Monday, new DateTime(2026, 10, 6, 21, 0, 0), 7 * 60, 600));

    [Fact]
    public void Scroll_ClampedSoBottomIsMidnight() =>
        Assert.Equal(24 * 60 - 600, WeekMath.ScrollAnchor(Monday, new DateTime(2026, 10, 4, 23, 50, 0), 7 * 60, 600));

    [Fact]
    public void Scroll_EmptyWeekStartsAtSeven() =>
        Assert.Equal(7 * 60, WeekMath.ScrollAnchor(Monday.AddDays(14), new DateTime(2026, 10, 3, 19, 0, 0), null, 600));

    [Fact]
    public void ScrollAnchor_StaysOnClasses_WhenEveningHasNothingLeft() =>
        Assert.Equal(7 * 60, WeekMath.ScrollAnchor(Monday, new DateTime(2026, 10, 3, 19, 38, 0), 7 * 60, 240, 17 * 60 + 50));

    [Fact]
    public void ScrollAnchor_StaysOnFirstClass_BeforeItStarts() =>
        Assert.Equal(7 * 60, WeekMath.ScrollAnchor(Monday, new DateTime(2026, 10, 2, 4, 10, 0), 7 * 60, 600, 17 * 60));
}
