using SoHocTap.Sources.Lms;

namespace SoHocTap.Tests;

public class GroupFilterTests
{
    private static HashSet<string> Set(params string[] xs) => [.. xs];

    [Fact]
    public void NoGroupCodeInName_Keeps() => Assert.True(GroupFilter.Keep(Set(), Set("L07")));

    [Fact]
    public void OwnGroup_Keeps() => Assert.True(GroupFilter.Keep(Set("L07"), Set("L07")));

    [Fact]
    public void OtherGroup_WhenGroupsKnown_Drops() => Assert.False(GroupFilter.Keep(Set("L08"), Set("L07")));

    [Fact]
    public void GroupLookupFailed_Keeps() => Assert.True(GroupFilter.Keep(Set("L08"), null));

    [Fact]
    public void EmptyGroupList_Keeps() => Assert.True(GroupFilter.Keep(Set("L08"), Set()));

    [Fact]
    public void AnyCodeMatching_Keeps() => Assert.True(GroupFilter.Keep(Set("L06", "L07"), Set("L07")));
}
