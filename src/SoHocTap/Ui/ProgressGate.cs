namespace SoHocTap.Ui;

/// <summary>Thanh tiến độ ở thanh trạng thái nên hiện thế nào. <paramref name="Recheck"/>: gọi lại sau bao lâu (null = chỉ khi có sự kiện mới).</summary>
internal readonly record struct BarState(bool Visible, bool Indeterminate, double Value, TimeSpan? Recheck);

/// <summary>
/// Quyết định hiện thanh tiến độ đồng bộ (hàm thuần, thời gian truyền vào để test):
/// <list type="bullet">
/// <item>Chỉ hiện khi đồng bộ chạy quá 1 giây; đã hiện thì giữ ít nhất 800 ms (số của VS Code progressService.ts), đỡ nhấp nháy.</item>
/// <item>Một thanh, có số, không bao giờ lùi trong một lượt (Win32 UX guide, Progress bars: "don't restart progress").</item>
/// <item>Vô định khi chưa biết tổng (đang mở trang, đăng nhập, chưa có phản hồi đầu), hoặc số đứng yên khoảng 5 giây.</item>
/// <item>Xong thì đầy thanh trong lúc còn giữ hiện.</item>
/// </list>
/// </summary>
internal sealed class ProgressGate
{
    public static readonly TimeSpan ShowDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MinVisible = TimeSpan.FromMilliseconds(800);
    public static readonly TimeSpan Stall = TimeSpan.FromSeconds(5);

    private DateTime? _busySince, _shownAt, _movedAt;
    private double _max = -1;

    /// <param name="busy">Còn nguồn đang đồng bộ.</param>
    /// <param name="value">Phần đã xong 0..1 (SyncProgress.Overall); null = chưa biết tổng.</param>
    public BarState Update(bool busy, double? value, DateTime now)
    {
        if (!busy)
        {
            _busySince = null;
            if (_shownAt is null) return new(false, false, 0, null);
            var left = MinVisible - (now - _shownAt.Value);
            if (left > TimeSpan.Zero) return new(true, false, 100, left);
            _shownAt = null;
            _max = -1;
            return new(false, false, 0, null);
        }

        if (_busySince is null)
        {
            _busySince = now;
            _movedAt = now;
            // Lượt mới ngay trong lúc thanh còn giữ hiện của lượt trước: giữ số cũ, không kéo thanh về 0.
            if (_shownAt is null) _max = -1;
        }
        if (value is { } v && v > _max + 1e-9)
        {
            _max = Math.Clamp(v, 0, 1);
            _movedAt = now;
        }
        if (_shownAt is null)
        {
            var wait = ShowDelay - (now - _busySince.Value);
            if (wait > TimeSpan.Zero) return new(false, false, 0, wait);
            _shownAt = now;
        }
        var still = now - _movedAt!.Value;
        var stalled = still >= Stall;
        var indeterminate = value is null || _max < 0 || stalled;
        // Còn chạy có số: hẹn kiểm tra lại đúng lúc hết 5 giây đứng yên để chuyển sang vô định mà không cần sự kiện.
        TimeSpan? recheck = indeterminate ? null : Stall - still;
        return new(true, indeterminate, Math.Max(_max, 0) * 100, recheck);
    }
}
