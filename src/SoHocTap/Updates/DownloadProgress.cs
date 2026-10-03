namespace SoHocTap.Updates;

/// <summary>
/// Đang tải bản cập nhật tới đâu. <see cref="Percent"/> null: Velopack chưa gọi callback tiến độ lần nào (chưa biết đã tải bao nhiêu,
/// thanh chạy vô định). <see cref="Verified"/>: DownloadUpdatesAsync đã trả về, tức là đã tải xong và kiểm SHA của gói.
/// </summary>
public sealed record DownloadState(string Version, long TotalBytes, int? Percent, bool Verified)
{
    public bool Indeterminate => Percent is null;

    /// <summary>Số byte ứng với phần trăm hiện tại (dung lượng tải lấy từ release feed: tổng các gói delta, hoặc gói đầy đủ).</summary>
    public long DoneBytes => Percent is { } p && TotalBytes > 0 ? TotalBytes * p / 100 : 0;
}

/// <summary>
/// Gom số phần trăm từ callback của Velopack DownloadUpdatesAsync (0 tới 100, làm tròn bước 2%) thành số hiện lên UI (hàm thuần, khóa
/// vì callback chạy trên thread pool):
/// <list type="bullet">
/// <item>Không lùi: gói delta lỗi thì Velopack tải lại gói đầy đủ, callback bắt đầu lại từ 0; thanh đứng yên tới khi vượt số cũ.</item>
/// <item>Callback của bước tải có thể tới 100 trước bước kiểm SHA (Velopack gọi VerifyPackageChecksumAsync sau DownloadReleaseEntry).
/// Nên giữ tối đa 99% tới khi <see cref="Complete"/>.</item>
/// </list>
/// </summary>
public sealed class DownloadTracker(string version, long totalBytes)
{
    public const int MaxBeforeVerified = 99;
    private readonly Lock _lock = new();

    public DownloadState State { get; private set; } = new(version, Math.Max(0, totalBytes), null, false);

    /// <summary>Một lần callback của Velopack. Trả true nếu số hiện lên đổi (để UI khỏi vẽ lại khi không có gì mới).</summary>
    public bool Report(int percent)
    {
        lock (_lock)
        {
            if (State.Verified) return false;
            var p = Math.Clamp(percent, 0, MaxBeforeVerified);
            if (State.Percent is { } cur && p <= cur) return false;
            State = State with { Percent = p };
            return true;
        }
    }

    /// <summary>DownloadUpdatesAsync đã trả về (tải xong, đã kiểm SHA): giờ mới được 100%.</summary>
    public void Complete()
    {
        lock (_lock) State = State with { Percent = 100, Verified = true };
    }
}

/// <summary>Chữ tiến độ tải: "Đang tải bản 1.2.0... 42% (12,3/29,1 MB)". Mẫu câu lấy từ file ngôn ngữ, truyền vào để test.</summary>
public static class DownloadText
{
    /// <summary>MB một chữ số thập phân theo culture của UI (vi: 12,3).</summary>
    public static string Mb(long bytes, IFormatProvider culture) => (bytes / 1048576.0).ToString("0.0", culture);

    /// <param name="starting">"{0}" = phiên bản; dùng trước callback đầu tiên.</param>
    /// <param name="withSize">"{0}" phiên bản, "{1}" phần trăm, "{2}" MB đã tải, "{3}" MB tổng.</param>
    /// <param name="noSize">"{0}" phiên bản, "{1}" phần trăm; khi feed không ghi dung lượng.</param>
    public static string Describe(DownloadState s, string starting, string withSize, string noSize, IFormatProvider culture) =>
        s.Percent is not { } p ? string.Format(culture, starting, s.Version)
        : s.TotalBytes > 0 ? string.Format(culture, withSize, s.Version, p, Mb(s.DoneBytes, culture), Mb(s.TotalBytes, culture))
        : string.Format(culture, noSize, s.Version, p);
}
