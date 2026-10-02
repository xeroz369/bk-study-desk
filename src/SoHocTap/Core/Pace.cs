namespace SoHocTap.Core;

/// <summary>
/// Giãn cách request tới một máy chủ trường: lần gọi sau cách lần trước ít nhất <c>gap</c>, dùng chung cho mọi luồng
/// (đồng bộ, tải tài liệu, ảnh trong Luyện tập). Server báo quá tải (HTTP 429/503) thì <see cref="PauseFor"/> dừng hẳn
/// mọi request tới server đó một lúc, thay vì gọi dồn tiếp.
/// </summary>
public sealed class Pace(Func<TimeSpan> gap, Func<DateTime>? clock = null) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Func<DateTime> _now = clock ?? (() => DateTime.UtcNow);
    private DateTime _next = DateTime.MinValue;
    private DateTime _pausedUntil = DateTime.MinValue;

    /// <summary>Thời điểm sớm nhất được gửi request kế tiếp.</summary>
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

    /// <summary>Server đang giới hạn: không gửi gì tới server này trong khoảng <paramref name="duration"/>.</summary>
    public void PauseFor(TimeSpan duration)
    {
        var until = _now() + duration;
        if (until > _pausedUntil) _pausedUntil = until;
    }

    /// <summary>
    /// Thời gian chờ khi server báo 429/503. Có Retry-After thì chờ ít nhất bằng đó (Azure "transient faults": Retry-After được ưu tiên),
    /// kẹp tối đa 5 phút. Không có thì "full jitter" (AWS Architecture Blog): ngẫu nhiên trong [1, min(60, 2·2^lần)] giây.
    /// Nguồn: learn.microsoft.com/azure/architecture/best-practices/transient-faults, aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter
    /// </summary>
    public static TimeSpan Backoff(TimeSpan? retryAfter, int attempt = 0, Random? random = null)
    {
        if (retryAfter is { } t) return TimeSpan.FromSeconds(Math.Clamp(t.TotalSeconds, 1, 300));
        var cap = Math.Min(60, 2 * Math.Pow(2, attempt));
        return TimeSpan.FromSeconds(1 + (random ?? Random.Shared).NextDouble() * (cap - 1));
    }

    /// <summary>
    /// Retry-After theo RFC 9110 §10.2.3 là số giây HOẶC một mốc HTTP-date. Mốc thời gian thì trừ theo header Date của server
    /// (đồng hồ máy có thể lệch), không có thì theo giờ máy. Mốc đã qua thì coi như chờ 0.
    /// </summary>
    public static TimeSpan? RetryAfter(System.Net.Http.Headers.RetryConditionHeaderValue? header, DateTimeOffset? serverNow, DateTimeOffset localNow)
    {
        if (header?.Delta is { } d) return d;
        if (header?.Date is { } at) { var wait = at - (serverNow ?? localNow); return wait > TimeSpan.Zero ? wait : TimeSpan.Zero; }
        return null;
    }

    public void Dispose() => _lock.Dispose();

    /// <summary>Mã HTTP nghĩa là "chậm lại": 429 Too Many Requests, 503 Service Unavailable.</summary>
    public static bool IsThrottle(int status) => status is 429 or 503;
}
