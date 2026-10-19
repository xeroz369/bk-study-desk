using SoHocTap.Files;
using SoHocTap.Sources.Lms;

namespace BKStudyDesk.Core.Tests;

/// <summary>Lớp kho chung của môn trên LMS ("..._Video"): phần riêng trong môn, tính theo học kỳ của môn đang học.</summary>
public class SharedCourseTests
{
    [Theory]
    [InlineData("Kỹ thuật số (EE1009)_Video", null, "Kho chung")]
    [InlineData("Kỹ thuật số (Thí nghiệm) (EE1010)_Video", "Thí nghiệm", "Thí nghiệm (kho chung)")]
    [InlineData("Kỹ thuật số (EE1009)_GIẢNG VIÊN A (CQ_HK261) [L02]", null, null)]
    public void Shared_part_from_marker(string fullname, string? part, string? expected) =>
        Assert.Equal(expected, Organizer.SharedPart(fullname, part, "_Video"));

    [Fact]
    public void Marker_off_keeps_part() => Assert.Equal("BTL", Organizer.SharedPart("X (AB1234)_Video", "BTL", ""));

    private static CourseInfo C(string code, string? part, string term) => new(1, "x", "Môn", code, part, term, "", "");

    [Fact]
    public void Shared_course_takes_current_term_only_when_subject_is_current()
    {
        var shared = C("EE1009", "Kho chung", "");
        var all = new[] { C("EE1009", null, "HK261"), shared, C("MT2013", "Kho chung", "") };
        Assert.Equal("HK261", LmsSource.SharedTerm(shared, all, "HK261").Term);
        Assert.Equal("", LmsSource.SharedTerm(all[2], all, "HK261").Term);       // môn không học kỳ này
        Assert.Equal("HK261", LmsSource.SharedTerm(all[0], all, "HK261").Term);   // lớp thường giữ nguyên
        Assert.True(Organizer.IsShared("Thí nghiệm (kho chung)"));
        Assert.False(Organizer.IsShared("Thí nghiệm"));
    }
}
