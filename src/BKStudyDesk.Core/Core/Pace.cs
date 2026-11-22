namespace SoHocTap.Core;

public sealed class Pace(Func<TimeSpan> gap, Func<DateTime>? clock = null) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Func<DateTime> _now = clock ?? (() => DateTime.UtcNow);
    private DateTime _next = DateTime.MinValue;
    private DateTime _pausedUntil = DateTime.MinValue;

    public DateTime NextAt => _next > _pausedUntil ? _next : _pausedUntil;

    public async Task WaitAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var wait = NextAt - _now();
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            _next = _now() + gap();
        }
        finally { _lock.Release(); }
    }

    public void PauseFor(TimeSpan duration)
    {
        var until = _now() + duration;
        if (until > _pausedUntil) _pausedUntil = until;
    }

    public TimeSpan PausedFor => _pausedUntil > _now() ? _pausedUntil - _now() : TimeSpan.Zero;

    public static TimeSpan Backoff(TimeSpan? retryAfter, int attempt = 0, Random? random = null)
    {
        if (retryAfter is { } t) return TimeSpan.FromSeconds(Math.Clamp(t.TotalSeconds, 1, 300));
        var cap = Math.Min(60, 2 * Math.Pow(2, attempt));
        return TimeSpan.FromSeconds(1 + (random ?? Random.Shared).NextDouble() * (cap - 1));
    }

    public static TimeSpan? RetryAfter(System.Net.Http.Headers.RetryConditionHeaderValue? header, DateTimeOffset? serverNow, DateTimeOffset localNow)
    {
        if (header?.Delta is { } d) return d;
        if (header?.Date is { } at) { var wait = at - (serverNow ?? localNow); return wait > TimeSpan.Zero ? wait : TimeSpan.Zero; }
        return null;
    }

    public void Dispose() => _lock.Dispose();

    public static bool IsThrottle(int status) => status is 429 or 503;
}
