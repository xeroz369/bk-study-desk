using SoHocTap.Core;

namespace SoHocTap.Tests;

public class VnTimeTests
{
    [Fact]
    public void Wall_RoundTrip_IndependentOfMachineZone()
    {
        // 07:00 ngày 05/10/2026 ở VN = 00:00 UTC.
        var sec = VnTime.FromWall(new DateTime(2026, 10, 5, 7, 0, 0));
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), sec);
        Assert.Equal(new DateTime(2026, 10, 5, 7, 0, 0), VnTime.ToWall(sec));
        Assert.Equal(DateTimeKind.Unspecified, VnTime.ToWall(sec).Kind);
    }

    [Fact]
    public void FromWall_IgnoresKind()
    {
        var a = VnTime.FromWall(new DateTime(2026, 10, 5, 7, 0, 0, DateTimeKind.Local));
        var b = VnTime.FromWall(new DateTime(2026, 10, 5, 7, 0, 0, DateTimeKind.Utc));
        Assert.Equal(a, b);
    }

    [Fact]
    public void ToWall_CrossesMidnight()
    {
        // 18:30 UTC = 01:30 sáng hôm sau ở VN.
        var sec = new DateTimeOffset(2026, 12, 31, 18, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        Assert.Equal(new DateTime(2027, 1, 1, 1, 30, 0), VnTime.ToWall(sec));
    }

    [Theory]
    [InlineData("07:00", 420)]
    [InlineData("07:30", 450)]
    [InlineData("7:30", 450)]
    [InlineData("7:05", 425)]
    [InlineData("7g30", 450)]
    [InlineData("09g00", 540)]
    [InlineData("9G15", 555)]
    [InlineData("9g", 540)]
    [InlineData(" 13:45 ", 825)]
    [InlineData("0g00", 0)]
    [InlineData("23:59", 1439)]
    public void ParseClock_Accepts(string text, int minutes) => Assert.Equal(minutes, VnTime.ParseClock(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("--")]
    [InlineData("abc")]
    [InlineData("24:00")]
    [InlineData("7:60")]
    [InlineData("7:")]
    [InlineData(":30")]
    [InlineData("-1:00")]
    [InlineData("7:3a")]
    public void ParseClock_RejectsWithoutThrowing(string? text) => Assert.Null(VnTime.ParseClock(text));

    [Fact]
    public void ParseDateTime_RegistrationFormat()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 2, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), VnTime.ParseDateTime("15/10/2026 09:00"));
        Assert.Null(VnTime.ParseDateTime("31/02/2026 09:00"));
        Assert.Null(VnTime.ParseDateTime(null));
    }

    [Fact]
    public void ParseDateAndClock_ExamFormat()
    {
        Assert.Equal(VnTime.FromWall(new DateTime(2026, 10, 15, 9, 0, 0)), VnTime.ParseDateAndClock("2026-10-15", "09g00"));
        Assert.Equal(VnTime.FromWall(new DateTime(2026, 10, 15)), VnTime.ParseDateAndClock("2026-10-15", "??"));
        Assert.Equal(VnTime.FromWall(new DateTime(2026, 10, 15, 9, 0, 0)), VnTime.ParseDateAndClock("15/10/2026", "09g00"));
        Assert.Null(VnTime.ParseDateAndClock("15.10.2026", "09g00"));
        Assert.Null(VnTime.ParseDateAndClock(null, "09g00"));
    }

    [Theory]
    [InlineData("2026-10-15")]
    [InlineData(" 15/10/2026 ")]
    [InlineData("15/10/2026")]
    public void ParseDate_Accepts(string text) => Assert.Equal(new DateTime(2026, 10, 15), VnTime.ParseDate(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("chưa xếp")]
    [InlineData("2026-02-31")]
    public void ParseDate_RejectsWithoutThrowing(string? text) => Assert.Null(VnTime.ParseDate(text));

    [Fact]
    public void DayDiff_UsesVnMidnight_NotMachineZone()
    {
        // Máy để UTC: 17:30 UTC ngày 04/10 đã là 00:30 sáng 05/10 ở VN. Buổi học 07:00 ngày 05/10 phải là "hôm nay",
        // còn mốc 23:00 tối 04/10 (giờ VN) là hôm qua, dù trên đồng hồ UTC cả hai cùng ngày 04/10 hoặc 05/10.
        var now = new DateTimeOffset(2026, 10, 4, 17, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        Assert.Equal(0, VnTime.DayDiff(VnTime.FromWall(new DateTime(2026, 10, 5, 7, 0, 0)), now));
        Assert.Equal(-1, VnTime.DayDiff(VnTime.FromWall(new DateTime(2026, 10, 4, 23, 0, 0)), now));
        Assert.Equal(1, VnTime.DayDiff(VnTime.FromWall(new DateTime(2026, 10, 6, 0, 0, 0)), now));
    }

    [Theory]
    [InlineData(2026, 10, 4, 2026, 9, 28)]   // Chủ nhật thuộc tuần bắt đầu Thứ hai trước đó
    [InlineData(2026, 10, 5, 2026, 10, 5)]
    [InlineData(2026, 10, 7, 2026, 10, 5)]
    public void Monday_OfWeek(int y, int m, int d, int my, int mm, int md) =>
        Assert.Equal(new DateTime(my, mm, md), VnTime.Monday(new DateTime(y, m, d, 23, 59, 0)));
}
