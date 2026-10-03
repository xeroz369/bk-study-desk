using System.Text;
using SoHocTap.Core;

namespace SoHocTap.Tests;

public class IcsTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_VietnamWallClock_WritesUtc()
    {
        var ics = Ics.Build("Lịch", [new IcsEvent("mt1005-1", new DateTime(2026, 10, 5, 7, 0, 0), new DateTime(2026, 10, 5, 8, 50, 0), "Giải tích 2", "H1-201")], Now);
        Assert.Contains("DTSTART:20261005T000000Z\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTEND:20261005T015000Z\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("UID:mt1005-1@bkstudydesk\r\n", ics, StringComparison.Ordinal);
        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", ics, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void Escape_SpecialCharacters() => Assert.Equal(@"a\, b\; c\\d\ne", Ics.Escape("a, b; c\\d\ne"));

    [Fact]
    public void Fold_EveryLineAtMost75Bytes_AndRejoinsToOriginal()
    {
        var line = "DESCRIPTION:" + string.Concat(Enumerable.Repeat("Giảng viên Nguyễn Văn A, phòng H6-301; ", 8));
        var parts = Ics.Fold(line).ToList();
        Assert.True(parts.Count > 1);
        Assert.All(parts, p => Assert.True(Encoding.UTF8.GetByteCount(p) <= 75));
        Assert.All(parts.Skip(1), p => Assert.StartsWith(" ", p, StringComparison.Ordinal));
        Assert.Equal(line, parts[0] + string.Concat(parts.Skip(1).Select(p => p[1..])));
    }

    [Theory]
    [InlineData(39, 2, 2026, 9, 21)]    // tuần 39/2026, Thứ hai
    [InlineData(40, 8, 2026, 10, 4)]    // Chủ nhật của tuần 40
    [InlineData(2, 3, 2027, 1, 12)]     // tuần 2 nhỏ hơn tuần đầu kỳ: thuộc năm sau
    public void ClassDate_IsoWeekAndYearRollover(int week, int day, int y, int m, int d) =>
        Assert.Equal(new DateTime(y, m, d), Ics.ClassDate(2026, 35, week, day));

    [Fact]
    public void Minutes_ParsesOrNull()
    {
        Assert.Equal(420, Ics.Minutes("07:00"));
        Assert.Equal(450, Ics.Minutes("7g30"));
        Assert.Null(Ics.Minutes("--"));
        Assert.Null(Ics.Minutes(null));
    }
}
