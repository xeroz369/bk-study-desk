using SoHocTap.Core;

namespace SoHocTap.Tests;

public class PaceTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 4)]
    [InlineData(5, 60)]
    [InlineData(20, 60)]
    public void Backoff_NoRetryAfter_FullJitterWithinCap(int attempt, double cap)
    {
        var rnd = new Random(1);
        for (var i = 0; i < 200; i++)
        {
            var s = Pace.Backoff(null, attempt, rnd).TotalSeconds;
            Assert.InRange(s, 1, cap);
        }
    }

    [Fact]
    public void Backoff_HonorsShortRetryAfter() => Assert.Equal(TimeSpan.FromSeconds(3), Pace.Backoff(TimeSpan.FromSeconds(3)));

    [Fact]
    public void RetryAfter_DeltaSeconds() =>
        Assert.Equal(TimeSpan.FromSeconds(120), Pace.RetryAfter(new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(120)), null, DateTimeOffset.UtcNow));

    [Fact]
    public void RetryAfter_HttpDate_UsesServerClock()
    {
        var server = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var local = server.AddMinutes(10);   // đồng hồ máy chạy nhanh 10 phút
        var h = new System.Net.Http.Headers.RetryConditionHeaderValue(server.AddSeconds(90));
        Assert.Equal(TimeSpan.FromSeconds(90), Pace.RetryAfter(h, server, local));
    }

    [Fact]
    public void RetryAfter_PastDate_IsZero()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, Pace.RetryAfter(new System.Net.Http.Headers.RetryConditionHeaderValue(now.AddSeconds(-5)), now, now));
    }

    [Fact]
    public void RetryAfter_Missing_IsNull() => Assert.Null(Pace.RetryAfter(null, null, DateTimeOffset.UtcNow));

    [Fact]
    public void Backoff_ClampsLongRetryAfter() => Assert.Equal(TimeSpan.FromMinutes(5), Pace.Backoff(TimeSpan.FromHours(2)));

    [Fact]
    public void Backoff_KeepsReasonableRetryAfter() => Assert.Equal(TimeSpan.FromSeconds(42), Pace.Backoff(TimeSpan.FromSeconds(42)));

    [Theory]
    [InlineData(429, true)]
    [InlineData(503, true)]
    [InlineData(200, false)]
    [InlineData(500, false)]
    [InlineData(404, false)]
    public void IsThrottle(int status, bool expected) => Assert.Equal(expected, Pace.IsThrottle(status));

    [Fact]
    public async Task WaitAsync_SpacesCalls()
    {
        using var pace = new Pace(() => TimeSpan.FromMilliseconds(120));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await pace.WaitAsync(default);   // lần đầu không phải chờ
        Assert.True(sw.ElapsedMilliseconds < 100);
        await pace.WaitAsync(default);
        await pace.WaitAsync(default);
        Assert.True(sw.ElapsedMilliseconds >= 220, $"chỉ chờ {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void PauseFor_PushesNextAt_AndNeverShortens()
    {
        var now = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        using var pace = new Pace(() => TimeSpan.Zero, () => now);
        pace.PauseFor(TimeSpan.FromSeconds(60));
        Assert.Equal(now.AddSeconds(60), pace.NextAt);
        pace.PauseFor(TimeSpan.FromSeconds(10));   // lệnh dừng ngắn hơn không rút ngắn lệnh dừng đang có
        Assert.Equal(now.AddSeconds(60), pace.NextAt);
    }

    [Fact]
    public async Task WaitAsync_Cancelled_Throws()
    {
        using var pace = new Pace(() => TimeSpan.Zero);
        pace.PauseFor(TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pace.WaitAsync(cts.Token));
    }
}
