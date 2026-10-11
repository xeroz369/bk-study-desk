using SoHocTap.Files;

namespace BKStudyDesk.Core.Tests;

public class ReadMarksTests
{
    [Fact]
    public void Read_until_the_file_changes()
    {
        var m = new ReadMarks();
        Assert.False(m.IsRead("Môn học/GT2/a.pdf", 100));
        Assert.True(m.Set("Môn học/GT2/a.pdf", 100, read: true));
        Assert.False(m.Set("Môn học/GT2/a.pdf", 100, read: true));   // không đổi: không cần ghi
        Assert.True(m.IsRead("môn học\\gt2\\A.pdf", 100));             // hoa thường, gạch chéo ngược
        Assert.False(m.IsRead("Môn học/GT2/a.pdf", 200));              // bản mới trên LMS: chưa đọc
    }

    [Fact]
    public void Unread_and_round_trip()
    {
        var m = new ReadMarks();
        m.Set("x/b.pdf", 5, read: true);
        var back = new ReadMarks(m.ToJson());
        Assert.True(back.IsRead("x/b.pdf", 5));
        Assert.True(back.Set("x/b.pdf", 5, read: false));
        Assert.False(back.IsRead("x/b.pdf", 5));
        Assert.False(back.Set("x/b.pdf", 5, read: false));
    }
}
