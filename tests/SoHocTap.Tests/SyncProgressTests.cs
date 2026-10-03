using SoHocTap.Sources;
using SoHocTap.Ui;

namespace SoHocTap.Tests;

/// <summary>Tiến độ đồng bộ theo trọng số (SyncPlan) và cách hiện thanh tiến độ (ProgressGate).</summary>
public class SyncProgressTests
{
    /// <summary>Kế hoạch giống LMS khi không tự tải tài liệu: kiểm tra (chưa biết số khóa), nhóm, lịch 2, quiz 2, thông báo, điểm.</summary>
    private static SyncPlan Lms(bool autoDownload = false)
    {
        var plan = new SyncPlan()
            .Add("sync.lms.check", weight: null)
            .Add("sync.lms.groups")
            .Add("sync.lms.events", 2)
            .Add("sync.lms.quizzes", 2)
            .Add("sync.lms.news", weight: null)
            .Add("sync.lms.grades", weight: null);
        if (autoDownload) plan.Add("sync.lms.files", weight: null, reserved: true).Add("sync.lms.assign");
        return plan.CapUntilDone("sync.lms.check", 0.33);
    }

    [Fact]
    public void UnknownTotalIsIndeterminate()
    {
        var plan = Lms();
        var p = plan.Enter("sync.lms.check", "sync.lms.courses");
        Assert.Null(p.Permille);
        Assert.Equal("sync.lms.courses", p.Key);
    }

    [Fact]
    public void WeightedByStepsOnceCountsKnown()
    {
        var plan = Lms();
        plan.Enter("sync.lms.check", "sync.lms.courses");
        plan.SetWeight("sync.lms.check", 4);
        plan.SetWeight("sync.lms.news", 4);
        plan.SetWeight("sync.lms.grades", 4);
        // Tổng = 4 + 1 + 2 + 2 + 4 + 4 = 17.
        var p = plan.Advance(2, 3, 4);
        Assert.Equal("sync.lms.check", p.Key);
        Assert.Equal((3, 4), (p.Count, p.Of));
        Assert.Equal(117, p.Permille);                         // 2/17
        Assert.Equal(235, plan.Enter("sync.lms.groups").Permille);   // xong kiểm tra: 4/17
        Assert.Equal(294, plan.Enter("sync.lms.events").Permille);   // 5/17
        Assert.Equal(764, plan.Enter("sync.lms.grades").Permille);   // 13/17
        Assert.Equal(1000, plan.Finish().Permille);
    }

    [Fact]
    public void CappedAt33PercentUntilCourseCheckFinishes()
    {
        var plan = Lms();
        plan.SetWeight("sync.lms.check", 40);   // nhiều lớp mới: kiểm tra chiếm phần lớn tổng
        plan.SetWeight("sync.lms.news", 1);
        plan.SetWeight("sync.lms.grades", 1);
        plan.Enter("sync.lms.check");
        Assert.Equal(330, plan.Advance(39, 40, 40).Permille);
        Assert.True(plan.Enter("sync.lms.groups").Permille > 330);
    }

    [Fact]
    public void NeverGoesBackwardsAndNever100BeforeFinish()
    {
        var plan = Lms(autoDownload: true);
        var seen = new List<int>();
        void Take(SyncProgress p) { if (p.Permille is { } v) seen.Add(v); }
        Take(plan.Enter("sync.lms.check", "sync.lms.courses"));
        plan.SetWeight("sync.lms.check", 3);
        plan.SetWeight("sync.lms.news", 2);
        plan.SetWeight("sync.lms.grades", 2);
        for (var i = 0; i < 3; i++) Take(plan.Advance(i, i + 1, 3));
        foreach (var k in new[] { "sync.lms.groups", "sync.lms.events", "sync.lms.quizzes", "sync.lms.news", "sync.lms.grades" }) Take(plan.Enter(k));
        plan.SetWeight("sync.lms.files", 0);   // không có tệp: phần giữ chỗ bỏ đi, thanh nhảy lên
        Take(plan.Enter("sync.lms.assign"));
        Take(plan.Advance(0.5));
        Assert.Equal(seen.OrderBy(x => x), seen);
        Assert.All(seen, v => Assert.True(v < 1000));
        Assert.Equal(1000, plan.Finish().Permille);
    }

    [Fact]
    public void FilesReserveHalfTheBarUntilTheirCountIsKnown()
    {
        var plan = Lms(autoDownload: true);
        plan.SetWeight("sync.lms.check", 2);
        plan.SetWeight("sync.lms.news", 1);
        plan.SetWeight("sync.lms.grades", 1);
        // Hết các bước không phải tệp (trừ bước lưu đề cuối cùng): chưa quá nửa thanh.
        var beforeFiles = plan.Enter("sync.lms.files").Permille!.Value;
        Assert.InRange(beforeFiles, 400, 500);
        plan.SetWeight("sync.lms.files", 1000);   // 1000 byte
        var p = plan.Advance(100, 2, 2, "Giải tích 2");
        Assert.Equal("Giải tích 2", p.Detail);
        Assert.InRange(p.Permille!.Value, 500, 560);   // nửa đầu + 10% của nửa sau
        Assert.InRange(plan.Enter("sync.lms.assign").Permille!.Value, 950, 990);
    }

    [Fact]
    public void FileBytesDecideTheShare()
    {
        var plan = new SyncPlan().Add("a").Add("sync.lms.files", weight: null, reserved: true);
        plan.Enter("a");
        plan.SetWeight("sync.lms.files", 100 + 900);
        plan.Enter("sync.lms.files", count: 1, of: 2);
        Assert.Equal(550, plan.Advance(100, 2, 2).Permille);    // tệp nhỏ xong: chỉ 10% của phần tệp
    }

    [Fact]
    public void MybkCountsOnePerCallAndTwoForRegistration()
    {
        var plan = new SyncPlan().Add("sync.mybk.open", weight: null);
        for (var i = 0; i < 13; i++) plan.Add("mybk:" + i);
        plan.Add("sync.mybk.registration", 2);
        Assert.Null(plan.Enter("sync.mybk.open").Permille);     // đang mở MyBK, có thể qua SSO: chưa biết bao lâu
        plan.SetWeight("sync.mybk.open", 0);
        var p = plan.Enter("mybk:6", "sync.mybk.api", 7, 13);
        Assert.Equal(400, p.Permille);                          // 6/15
        Assert.Equal(("sync.mybk.api", 7, 13), (p.Key, p.Count, p.Of));
        Assert.Equal(866, plan.Enter("sync.mybk.registration").Permille);   // 13/15
    }

    [Fact]
    public void SignalRoundTrips()
    {
        var p = new SyncProgress("sync.lms.files", 2, 5, "Giải tích 2", 512);
        Assert.True(SyncSignal.TryParseProgress(SyncSignal.Progress(p), out var back));
        Assert.Equal(p, back);
        var unknown = new SyncProgress("sync.mybk.open");
        Assert.True(SyncSignal.TryParseProgress(SyncSignal.Progress(unknown), out var back2));
        Assert.Equal(unknown, back2);
        Assert.False(SyncSignal.TryParseProgress("dòng log thường", out _));
    }

    [Fact]
    public void OverallAveragesKnownAndIsNullWhenAnyUnknown()
    {
        Assert.Equal(0.5, SyncProgress.Overall([new SyncProgress("a", Permille: 400), new SyncProgress("b", Permille: 600)]));
        Assert.Null(SyncProgress.Overall([new SyncProgress("a", Permille: 400), new SyncProgress("b")]));
        Assert.Null(SyncProgress.Overall([new SyncProgress("a", Permille: 400), null]));
        Assert.Null(SyncProgress.Overall([]));
    }
}

public class ProgressGateTests
{
    private static readonly DateTime T0 = new(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc);
    private static DateTime At(double ms) => T0.AddMilliseconds(ms);

    [Fact]
    public void FastSyncNeverShowsTheBar()
    {
        var g = new ProgressGate();
        var s = g.Update(true, 0.2, At(0));
        Assert.False(s.Visible);
        Assert.Equal(TimeSpan.FromSeconds(1), s.Recheck);
        Assert.False(g.Update(true, 0.9, At(900)).Visible);
        Assert.False(g.Update(false, null, At(950)).Visible);
    }

    [Fact]
    public void ShowsAfterOneSecondAndKeeps800Ms()
    {
        var g = new ProgressGate();
        g.Update(true, 0.1, At(0));
        var shown = g.Update(true, 0.3, At(1000));
        Assert.True(shown.Visible);
        Assert.False(shown.Indeterminate);
        Assert.Equal(30, shown.Value, 3);
        var done = g.Update(false, null, At(1200));
        Assert.True(done.Visible);                    // giữ đủ 800 ms kể từ lúc hiện
        Assert.Equal(100, done.Value);
        Assert.Equal(TimeSpan.FromMilliseconds(600), done.Recheck);
        Assert.False(g.Update(false, null, At(1800)).Visible);
    }

    [Fact]
    public void ValueNeverGoesBackwards()
    {
        var g = new ProgressGate();
        g.Update(true, 0.6, At(0));
        Assert.Equal(60, g.Update(true, 0.6, At(1000)).Value, 3);
        Assert.Equal(60, g.Update(true, 0.3, At(1500)).Value, 3);   // MyBK vừa bắt đầu: trung bình tụt, thanh đứng yên
        Assert.Equal(70, g.Update(true, 0.7, At(2000)).Value, 3);
    }

    [Fact]
    public void UnknownTotalIsIndeterminate()
    {
        var g = new ProgressGate();
        g.Update(true, null, At(0));
        var s = g.Update(true, null, At(1000));
        Assert.True(s.Visible);
        Assert.True(s.Indeterminate);
    }

    [Fact]
    public void StalledKeepsRealPercent()
    {
        // Đứng yên bao lâu cũng giữ số đã đạt, không chuyển thanh chạy qua lại (yêu cầu người dùng 03/10/2026).
        var g = new ProgressGate();
        g.Update(true, 0.4, At(0));
        Assert.False(g.Update(true, 0.4, At(1000)).Indeterminate);
        var s = g.Update(true, 0.4, At(60000));
        Assert.False(s.Indeterminate);
        Assert.Equal(40, s.Value, 3);
        Assert.Null(s.Recheck);
    }

    [Fact]
    public void NewSyncAfterHideStartsFromZero()
    {
        var g = new ProgressGate();
        g.Update(true, 0.8, At(0));
        g.Update(true, 0.8, At(1000));
        g.Update(false, null, At(2000));
        g.Update(true, 0.1, At(10000));
        Assert.Equal(10, g.Update(true, 0.1, At(11000)).Value, 3);
    }
}
