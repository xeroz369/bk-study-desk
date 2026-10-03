using SoHocTap.Ui;

namespace SoHocTap.Tests;

/// <summary>Vạch "bây giờ" trên lưới tuần (issue #22): cột, tọa độ y, ẩn khi ngoài tuần hay ngoài khung giờ.</summary>
public class WeekMathTests
{
    private static readonly DateTime Monday = new(2026, 9, 28);   // Thứ hai

    [Fact]
    public void Position_ExactMinute()
    {
        // Thứ bảy 03/10 9:41, lưới 7:00-17:00, 1 px/phút, lề 10.
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
    [InlineData(2026, 9, 27, 10, 0)]    // Chủ nhật tuần trước
    [InlineData(2026, 10, 5, 10, 0)]    // Thứ hai tuần sau
    public void OutsideWeek_Null(int y, int m, int d, int h, int min) =>
        Assert.Null(WeekMath.NowLine(Monday, new DateTime(y, m, d, h, min, 0), 7 * 60, 17 * 60, 1, 10));

    [Fact]
    public void Sunday_IsLastColumn() =>
        Assert.Equal(6, WeekMath.NowLine(Monday, new DateTime(2026, 10, 4, 12, 0, 0), 7 * 60, 17 * 60, 1, 10)?.Column);

    [Theory]
    [InlineData(6, 59, false)]
    [InlineData(7, 0, true)]     // đúng mép trên
    [InlineData(17, 0, true)]    // đúng mép dưới
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
}
