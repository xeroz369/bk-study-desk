using SoHocTap.Shell;

namespace BKStudyDesk.Core.Tests;

[CollectionDefinition("sso-gate", DisableParallelization = true)]
public sealed class SsoGateCollection;

/// <summary>One SSO pass at a time, one shared automatic fill per run, only while active.</summary>
[Collection("sso-gate")]
public sealed class SsoGateTests
{
    public SsoGateTests() => SsoGate.SignedIn();

    [Fact]
    public async Task Second_caller_waits_for_the_first()
    {
        var first = await SsoGate.EnterAsync(CancellationToken.None);
        Assert.True(SsoGate.Busy);
        var second = SsoGate.EnterAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.False(second.IsCompleted);
        first.Dispose();
        first.Dispose();   // twice is harmless
        (await second.WaitAsync(TimeSpan.FromSeconds(2))).Dispose();
        Assert.False(SsoGate.Busy);
    }

    [Fact]
    public void One_fill_shared_by_both_paths_and_only_while_active()
    {
        Assert.False(SsoGate.TryClaimFill(active: false));
        Assert.True(SsoGate.TryClaimFill(active: true));    // MyBK claims
        Assert.False(SsoGate.TryClaimFill(active: true));   // LMS refresh at the same time: no second fill
        SsoGate.FillResult(true);
        Assert.True(SsoGate.TryClaimFill(active: true));    // a later expiry may fill again
        SsoGate.FillAborted();
        Assert.False(SsoGate.FillSpent);
    }

    [Fact]
    public void Failed_fill_stops_until_the_user_signs_in()
    {
        var failed = 0;
        void On() => failed++;
        SsoGate.AutoFillFailed += On;
        try
        {
            Assert.True(SsoGate.TryClaimFill(active: true));
            SsoGate.FillResult(false);
            Assert.Equal(1, failed);
            Assert.True(SsoGate.FillSpent);
            Assert.False(SsoGate.TryClaimFill(active: true));
            SsoGate.SignedIn();
            Assert.True(SsoGate.TryClaimFill(active: true));
        }
        finally { SsoGate.AutoFillFailed -= On; SsoGate.SignedIn(); }
    }
}
