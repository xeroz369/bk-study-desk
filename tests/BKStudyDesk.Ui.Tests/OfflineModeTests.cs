using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Sources;

namespace BKStudyDesk.Ui.Tests;

/// <summary>Bật Offline là cài đặt chung: không chạy song song với test khác.</summary>
[CollectionDefinition("offline", DisableParallelization = true)]
public sealed class OfflineCollection;

/// <summary>Chế độ Offline: scheduler thôi tự đồng bộ, bấm tay Đồng bộ ngay vẫn chạy (thư mục data riêng của bộ test).</summary>
[Collection("offline")]
public class OfflineModeTests
{
    private sealed class CountingSource : ISource
    {
        public int Runs;
        public string Name => "fake";
        public SourceStatus Status() => new(true, null);
        public JsonNode? Data() => null;
        public Task SyncAsync(Action<string> log, bool force, CancellationToken ct) { Interlocked.Increment(ref Runs); return Task.CompletedTask; }
        public TimeSpan Interval => TimeSpan.FromMinutes(1);
    }

    [Fact]
    public async Task Offline_stops_scheduler_but_not_manual_sync()
    {
        var src = new CountingSource();
        using var hub = new SourceHub([src]) { Online = () => true };
        try
        {
            OfflineMode.Set(true);
            Assert.True(OfflineMode.On);
            hub.RunDue();
            await Task.Delay(200);
            Assert.Equal(0, src.Runs);

            Assert.True(hub.Start("fake", force: true));
            for (var i = 0; i < 50 && src.Runs == 0; i++) await Task.Delay(20);
            Assert.Equal(1, src.Runs);
        }
        finally { OfflineMode.Set(false); }
        Assert.False(OfflineMode.On);
    }

    private sealed class ExpiredSource : ISource
    {
        public string Name => "expired";
        public SourceStatus Status() => new(true, null);
        public JsonNode? Data() => null;
        public Task SyncAsync(Action<string> log, bool force, CancellationToken ct) => throw new SessionExpiredException("hết phiên");
        public TimeSpan Interval => TimeSpan.FromHours(12);
    }

    [Fact]
    public async Task Login_clears_session_expired_status_right_away()
    {
        using var hub = new SourceHub([new ExpiredSource()]) { Online = () => true };
        var done = Finished(hub);
        Assert.True(hub.Start("expired", force: true));
        await done.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(SoHocTap.Data.SyncErrorKind.SessionExpired, hub.ErrorKind("expired"));
        hub.ForgetSessionExpired();
        Assert.False(hub.Failed("expired"));
        // The sync started by the new sign-in runs even within 30 seconds of the failed one.
        Assert.True(hub.Start("expired", force: true));
    }

    [Fact]
    public async Task Failed_run_still_blocks_quick_restart_without_sign_in()
    {
        using var hub = new SourceHub([new ExpiredSource()]) { Online = () => true };
        var done = Finished(hub);
        Assert.True(hub.Start("expired", force: true));
        await done.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(hub.Failed("expired"));
        Assert.False(hub.Start("expired", force: true));
    }

    // "done" fires after the run is fully over (Running cleared), unlike Failed() which is true a moment earlier.
    private static Task Finished(SourceHub hub)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.Changed += (_, stage) => { if (stage == "done") tcs.TrySetResult(); };
        return tcs.Task;
    }

    [Fact]
    public void Next_auto_sync_is_hidden_when_offline()
    {
        using var hub = new SourceHub([new CountingSource()]) { Online = () => true };
        var next = hub.NextAutoAt("fake");
        Assert.NotNull(next);
        Assert.True(next <= DateTimeOffset.UtcNow.AddMinutes(1));   // chưa đồng bộ lần nào: tới hạn ngay
        try
        {
            OfflineMode.Set(true);
            Assert.Null(hub.NextAutoAt("fake"));
        }
        finally { OfflineMode.Set(false); }
    }
}
