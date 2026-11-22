using SoHocTap.Core;

namespace BKStudyDesk.Logic.Tests;

public class KeepFreshTests
{
    private const long Now = 1_793_900_000;

    [Fact]
    public void Due_after_the_set_days_and_never_below_one_day()
    {
        Assert.False(KeepFresh.Due(Now - 86399, Now, 1));
        Assert.True(KeepFresh.Due(Now - 86400, Now, 1));
        Assert.False(KeepFresh.Due(Now - 2 * 86400, Now, 3));
        Assert.True(KeepFresh.Due(Now - 86400, Now, 0));
        Assert.True(KeepFresh.Due(null, Now, 1));
    }

    [Fact]
    public void Latest_and_oldest_of_several_times()
    {
        Assert.Equal(Now, KeepFresh.Latest(Now - 10, Now, null));
        Assert.Null(KeepFresh.Latest(null, 0));
        Assert.Equal(Now - 10, KeepFresh.Oldest(Now - 10, Now));
        Assert.Null(KeepFresh.Oldest(Now, null));
    }

    [Fact]
    public void Warn_only_when_every_due_source_is_signed_out()
    {
        Assert.True(KeepFresh.AllSignedOut(["lms", "mybk"], ["lms", "mybk"]));
        Assert.False(KeepFresh.AllSignedOut(["lms", "mybk"], ["mybk"]));
        Assert.True(KeepFresh.AllSignedOut(["lms"], ["lms"]));
        Assert.False(KeepFresh.AllSignedOut([], []));
    }
}
