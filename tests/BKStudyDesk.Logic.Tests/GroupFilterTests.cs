using SoHocTap.Sources.Lms;

namespace SoHocTap.Tests;

public class GroupFilterTests
{
    private static HashSet<string> Set(params string[] xs) => [.. xs];

    private const string Pat = "L[0-9]{2,3}";

    [Fact]
    public void Codes_FindsStandaloneCode() => Assert.Equal(["L05"], GroupFilter.Codes("Nộp bài PreLab #2 lớp TN KTS L05", Pat));

    [Fact]
    public void Codes_KeepsOrderAndDropsDuplicates() => Assert.Equal(["L01", "L02"], GroupFilter.Codes("Báo cáo L01 L02 L01", Pat));

    [Fact]
    public void Codes_IgnoresCodeInsideWord() => Assert.Empty(GroupFilter.Codes("CL01 bài", Pat));

    [Fact]
    public void Codes_EmptyInputs_Empty()
    {
        Assert.Empty(GroupFilter.Codes("", Pat));
        Assert.Empty(GroupFilter.Codes("Bài L05", ""));
    }

    [Fact]
    public void NoGroupCodeInName_Keeps() => Assert.True(GroupFilter.Keep(Set(), Set("L05")));

    [Fact]
    public void OwnGroup_Keeps() => Assert.True(GroupFilter.Keep(Set("L05"), Set("L05")));

    [Fact]
    public void OtherGroup_WhenGroupsKnown_Drops() => Assert.False(GroupFilter.Keep(Set("L08"), Set("L05")));

    [Fact]
    public void GroupLookupFailed_Keeps() => Assert.True(GroupFilter.Keep(Set("L08"), null));

    [Fact]
    public void EmptyGroupList_Keeps() => Assert.True(GroupFilter.Keep(Set("L08"), Set()));

    [Fact]
    public void AnyCodeMatching_Keeps() => Assert.True(GroupFilter.Keep(Set("L06", "L05"), Set("L05")));
}
