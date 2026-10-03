using System.Globalization;
using System.Text.Json;
using SoHocTap.Core;
using SoHocTap.Ui;
using SoHocTap.Updates;

namespace SoHocTap.Tests;

/// <summary>Số % tải bản cập nhật (DownloadTracker): số thật của Velopack, không lùi, 100% chỉ khi đã kiểm SHA.</summary>
public class DownloadTrackerTests
{
    [Fact]
    public void IndeterminateUntilFirstCallback()
    {
        var t = new DownloadTracker("1.2.0", 1000);
        Assert.True(t.State.Indeterminate);
        Assert.Null(t.State.Percent);
        Assert.True(t.Report(0));                 // callback đầu tiên, kể cả 0%: đã có số
        Assert.False(t.State.Indeterminate);
        Assert.Equal(0, t.State.Percent);
    }

    [Fact]
    public void NeverGoesBackwards()
    {
        var t = new DownloadTracker("1.2.0", 1000);
        t.Report(40);
        Assert.False(t.Report(0));                // gói delta lỗi, Velopack tải lại gói đầy đủ từ 0
        Assert.False(t.Report(40));               // không đổi: khỏi vẽ lại
        Assert.Equal(40, t.State.Percent);
        Assert.True(t.Report(42));
        Assert.Equal(42, t.State.Percent);
    }

    [Fact]
    public void HundredOnlyAfterVerified()
    {
        var t = new DownloadTracker("1.2.0", 1000);
        t.Report(100);                            // byte cuối đã về, chưa kiểm SHA
        Assert.Equal(DownloadTracker.MaxBeforeVerified, t.State.Percent);
        Assert.False(t.State.Verified);
        t.Complete();
        Assert.Equal(100, t.State.Percent);
        Assert.True(t.State.Verified);
        Assert.False(t.Report(50));               // callback muộn sau khi xong: bỏ qua
        Assert.Equal(100, t.State.Percent);
    }

    [Fact]
    public void ClampsOutOfRange()
    {
        var t = new DownloadTracker("1.2.0", -5);
        t.Report(-10);
        Assert.Equal(0, t.State.Percent);
        Assert.Equal(0, t.State.TotalBytes);
        Assert.Equal(0, t.State.DoneBytes);
    }

    [Fact]
    public void DoneBytesFollowPercent() =>
        Assert.Equal(250, new DownloadState("1.2.0", 1000, 25, false).DoneBytes);
}

/// <summary>Chữ tiến độ, dùng đúng mẫu câu trong lang/*.json.</summary>
public class DownloadTextTests
{
    private static Dictionary<string, string> Lang(string code) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "lang", code + ".json")))!;

    private static string Describe(string code, DownloadState s)
    {
        var l = Lang(code);
        var culture = CultureInfo.GetCultureInfo(code == "vi" ? "vi-VN" : "en-US");
        return DownloadText.Describe(s, l["update.downloadStarting"], l["update.downloadingSize"], l["update.downloading"], culture);
    }

    // 29,1 MB tổng; 42% của số đó là 12,2 MB.
    private const long Total = 30_513_562;

    [Fact]
    public void VietnameseWithSize() =>
        Assert.Equal("Đang tải bản 1.2.0... 42% (12,2/29,1 MB)", Describe("vi", new("1.2.0", Total, 42, false)));

    [Fact]
    public void EnglishUsesDotDecimal() =>
        Assert.Equal("Downloading version 1.2.0... 42% (12.2/29.1 MB)", Describe("en", new("1.2.0", Total, 42, false)));

    [Fact]
    public void BeforeFirstCallbackNoNumber() =>
        Assert.Equal("Đang tải bản 1.2.0...", Describe("vi", new("1.2.0", Total, null, false)));

    [Fact]
    public void UnknownSizeShowsPercentOnly() =>
        Assert.Equal("Đang tải bản 1.2.0... 42%", Describe("vi", new("1.2.0", 0, 42, false)));

    [Fact]
    public void Megabytes() => Assert.Equal("0,0", DownloadText.Mb(0, CultureInfo.GetCultureInfo("vi-VN")));
}

/// <summary>Cài bản đã tải: luôn silent (không cửa sổ tiếng Anh của Velopack), có mở lại app hay không tùy lúc cài.</summary>
public class ApplyPlanTests
{
    [Fact]
    public void UserRestartIsSilentAndRestarts() =>
        Assert.Equal(new ApplyPlan(Silent: true, Restart: true),
            UpdatePolicy.PlanApply(ApplyTrigger.UserRestart, InstallKind.Installed, UpdateMode.Notify, downloaded: true, alreadyLaunched: false));

    [Fact]
    public void StartupInAutoModeIsSilentAndRestarts() =>
        Assert.Equal(new ApplyPlan(true, true),
            UpdatePolicy.PlanApply(ApplyTrigger.Startup, InstallKind.Installed, UpdateMode.Auto, true, false));

    [Fact]
    public void ExitInAutoModeIsSilentWithoutRestart() =>
        Assert.Equal(new ApplyPlan(true, false),
            UpdatePolicy.PlanApply(ApplyTrigger.AppExit, InstallKind.Installed, UpdateMode.Auto, true, false));

    [Theory]
    [InlineData(UpdateMode.Notify)]
    [InlineData(UpdateMode.Ask)]
    [InlineData(UpdateMode.Off)]
    public void ExitAndStartupOnlyInAutoMode(UpdateMode mode)
    {
        Assert.Null(UpdatePolicy.PlanApply(ApplyTrigger.AppExit, InstallKind.Installed, mode, true, false));
        Assert.Null(UpdatePolicy.PlanApply(ApplyTrigger.Startup, InstallKind.Installed, mode, true, false));
    }

    [Theory]
    [InlineData(InstallKind.Store)]       // Store tự cập nhật
    [InlineData(InstallKind.Portable)]    // bản zip chỉ có link tải
    public void OnlyInstalledBuildsSelfUpdate(InstallKind kind)
    {
        foreach (var trigger in Enum.GetValues<ApplyTrigger>())
            Assert.Null(UpdatePolicy.PlanApply(trigger, kind, UpdateMode.Auto, true, false));
    }

    [Fact]
    public void NothingDownloadedNothingToApply() =>
        Assert.Null(UpdatePolicy.PlanApply(ApplyTrigger.UserRestart, InstallKind.Installed, UpdateMode.Auto, downloaded: false, alreadyLaunched: false));

    [Fact]
    public void ExitAfterRestartClickDoesNotLaunchTwice() =>
        Assert.Null(UpdatePolicy.PlanApply(ApplyTrigger.AppExit, InstallKind.Installed, UpdateMode.Auto, true, alreadyLaunched: true));

    [Fact]
    public void NoStartupApplyRightAfterUpdaterRestart() =>
        Assert.False(UpdatePolicy.ApplyOnStartup(UpdateMode.Auto, InstallKind.Installed, [], otherInstanceRunning: false, restartedByUpdater: true));
}

/// <summary>ProgressGate cho thanh tải bản cập nhật: không chuyển vô định khi đứng yên, hủy thì ẩn ngay.</summary>
public class DownloadGateTests
{
    private static readonly DateTime T0 = new(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc);
    private static DateTime At(double ms) => T0.AddMilliseconds(ms);

    [Fact]
    public void StallDoesNotHideTheRealNumber()
    {
        var g = new ProgressGate(stallAfter: null);
        g.Update(true, 0.4, At(0));
        var s = g.Update(true, 0.4, At(1000));
        Assert.Null(s.Recheck);
        Assert.False(g.Update(true, 0.4, At(60_000)).Indeterminate);   // mạng chậm một phút: vẫn 40%
        Assert.Equal(40, g.Update(true, 0.4, At(60_000)).Value, 3);
    }

    [Fact]
    public void ResetHidesWithoutFillingTheBar()
    {
        var g = new ProgressGate(stallAfter: null);
        g.Update(true, 0.3, At(0));
        Assert.True(g.Update(true, 0.3, At(1000)).Visible);
        g.Reset();                                                      // hủy tải
        var s = g.Update(false, null, At(1100));
        Assert.False(s.Visible);
        Assert.Equal(0, s.Value);
    }

    [Fact]
    public void IndeterminateBeforeFirstNumberOnly()
    {
        var g = new ProgressGate(stallAfter: null);
        g.Update(true, null, At(0));
        Assert.True(g.Update(true, null, At(1000)).Indeterminate);
        Assert.False(g.Update(true, 0, At(1200)).Indeterminate);
    }

    [Fact]
    public void DefaultGateNeverTurnsIndeterminateWhenStalled()
    {
        var g = new ProgressGate();
        g.Update(true, 0.4, At(0));
        g.Update(true, 0.4, At(1000));
        Assert.False(g.Update(true, 0.4, At(5000)).Indeterminate);
    }
}
