using SoHocTap.Core;

namespace SoHocTap.Tests;

public class UpdatePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static bool Check(UpdateMode mode, InstallKind kind = InstallKind.Installed, DateTimeOffset? last = null, int hours = 24) =>
        UpdatePolicy.ShouldCheck(mode, kind, Now, last, hours);

    [Fact]
    public void ShouldCheck_AskMode_NeverChecks() => Assert.False(Check(UpdateMode.Ask));

    [Fact]
    public void ShouldCheck_Off_NeverChecks() => Assert.False(Check(UpdateMode.Off));

    [Fact]
    public void ShouldCheck_Store_False() => Assert.False(Check(UpdateMode.Notify, InstallKind.Store));

    [Fact]
    public void ShouldCheck_PortableNotify_True() => Assert.True(Check(UpdateMode.Notify, InstallKind.Portable));

    [Fact]
    public void ShouldCheck_Notify_FirstTime_True() => Assert.True(Check(UpdateMode.Notify));

    [Fact]
    public void ShouldCheck_Notify_DueAfter24h_True() => Assert.True(Check(UpdateMode.Notify, last: Now.AddHours(-24)));

    [Fact]
    public void ShouldCheck_Notify_Within24h_False() => Assert.False(Check(UpdateMode.Notify, last: Now.AddHours(-23)));

    [Fact]
    public void ShouldCheck_LastCheckInFuture_TreatsAsDue() => Assert.True(Check(UpdateMode.Auto, last: Now.AddDays(3)));

    [Fact]
    public void Next_AfterFailure_WaitsFullInterval() => Assert.False(Check(UpdateMode.Auto, last: Now.AddMinutes(-5)));

    [Fact]
    public void ShouldCheck_ShortConfiguredHours_StillWaitsSix() => Assert.False(Check(UpdateMode.Notify, last: Now.AddHours(-5), hours: 1));

    [Fact]
    public void Interval_ClampsToSixHours() => Assert.Equal(TimeSpan.FromHours(6), UpdatePolicy.Interval(1));

    [Fact]
    public void Interval_KeepsLargerValue() => Assert.Equal(TimeSpan.FromHours(48), UpdatePolicy.Interval(48));

    [Fact]
    public void CanDownload_BatterySaver_False() => Assert.False(UpdatePolicy.CanDownload(batterySaver: true));

    [Fact]
    public void CanDownload_Normal_True() => Assert.True(UpdatePolicy.CanDownload(batterySaver: false));

    [Theory]
    [InlineData("notify", UpdateMode.Notify)]
    [InlineData("AUTO", UpdateMode.Auto)]
    [InlineData("off", UpdateMode.Off)]
    [InlineData("ask", UpdateMode.Ask)]
    [InlineData("", UpdateMode.Ask)]
    [InlineData(null, UpdateMode.Ask)]
    [InlineData("always", UpdateMode.Ask)]
    public void ParseMode_Values(string? s, UpdateMode expected) => Assert.Equal(expected, UpdatePolicy.ParseMode(s));

    [Theory]
    [InlineData("v1.0.7", "1.0.6", true)]
    [InlineData("1.0.10", "1.0.9", true)]
    [InlineData("1.0.6", "1.0.6", false)]
    [InlineData("1.0.5", "1.0.6", false)]
    [InlineData("garbage", "1.0.6", false)]
    [InlineData("1.0.7", "", true)]
    public void IsNewer_Versions(string candidate, string current, bool expected) => Assert.Equal(expected, UpdatePolicy.IsNewer(candidate, current));

    [Theory]
    [InlineData("https://github.com/xeroz369/bk-study-desk", true)]
    [InlineData("http://github.com/xeroz369/bk-study-desk", false)]
    [InlineData("https://evil.example/feed", false)]
    [InlineData("relative\\feed", false)]
    [InlineData("", false)]
    public void IsValidSource_Values(string src, bool expected) => Assert.Equal(expected, UpdatePolicy.IsValidSource(src, _ => false));

    [Fact]
    public void IsValidSource_ExistingLocalFolder_True() =>
        Assert.True(UpdatePolicy.IsValidSource(Path.Combine(Path.GetTempPath(), "feed"), _ => true));

    [Theory]
    [InlineData(null, "1.0.6", ApplyOutcome.None)]
    [InlineData("", "1.0.6", ApplyOutcome.None)]
    [InlineData("1.0.7", "1.0.7", ApplyOutcome.Updated)]
    [InlineData("1.0.7", "1.0.6", ApplyOutcome.Failed)]
    [InlineData("1.0.7", "1.0.8", ApplyOutcome.Updated)]
    public void ApplyResult_Values(string? applying, string current, ApplyOutcome expected) =>
        Assert.Equal(expected, UpdatePolicy.ApplyResult(applying, current));
}