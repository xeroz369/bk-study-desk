using SoHocTap.Data;
using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

/// <summary>Trang MyBK (MybkPresenter): bảng điểm theo kỳ, giảng viên.</summary>
public class MybkTests
{
    private static MybkData Data(List<MybkGrade>? grades = null, List<MybkClass>? schedule = null, List<MybkRegistered>? registered = null) =>
        new(0, new MybkStudent("SV", "2310000", "L05"), new MybkTerm("20261", "HK261"), schedule ?? [], [],
            [new("20252", "HK252", "7.5", "7.2", null, null, null), new("20261", "HK261", "0", "7.2", null, null, null)], grades ?? [],
            null, null, null, null, null, null, registered);

    private static MybkGrade Grade(string term, string code, double? score, int result) =>
        new(term, code, "Môn " + code, 3, score, score is null ? null : "B", null, result, null, null, null, null);

    [Fact]
    public void Grades_NewestTermFirst_ReservedLast_FailFlag()
    {
        var rows = MybkPresenter.Grades(Data([Grade("BL", "X1", null, -1), Grade("20252", "A1", 8, 1), Grade("20261", "B1", 3, 0)]));
        Assert.Equal(["B1", "A1", "X1"], rows.Select(r => r.Code));
        Assert.True(rows[0].Fail);
        Assert.Equal("HK252", rows[1].Group);
    }

    [Fact]
    public void Teachers_GroupFromRegistration_UnfixedLast()
    {
        var schedule = new List<MybkClass>
        {
            new("MT1005", "Giải tích 2", "L01", 0, [], "", "", "", 0, 0, "Cô A"),
            new("MT1005", "Giải tích 2", "L01", 3, [1], "07:00", "09:50", "H6-205", 1, 3, "Cô A"),
        };
        var rows = MybkPresenter.Teachers(Data(schedule: schedule, registered: [new("MT1005", "Giải tích 2", "L05", null, null, "1", "Đạt")]));
        Assert.Equal(2, rows.Count);
        Assert.Equal("07:00-09:50", rows[0].Time);
        Assert.Equal("L05", rows[0].Group);
        Assert.Equal(99, rows[1].DaySort);
    }

    [Fact]
    public void Subtitle_NoData() => Assert.Equal("2310000, L05, HK261", MybkPresenter.Subtitle(Data()));
}
