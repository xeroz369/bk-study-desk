namespace SoHocTap.Core;

/// <summary>Chế độ cập nhật người dùng chọn (app.update.mode). Ask = chưa chọn: không gọi mạng.</summary>
public enum UpdateMode { Ask, Notify, Auto, Off }

/// <summary>Kết quả lần cài bản mới trước (so bản đang chạy với bản định cài).</summary>
public enum ApplyOutcome { None, Updated, Failed }

/// <summary>
/// Quyết định có kiểm tra bản mới hay không (hàm thuần, không gọi mạng). Không ép người dùng: chưa chọn hoặc tắt thì không
/// bao giờ kiểm tra; bản local (build từ source) và bản Store (Store tự cập nhật) cũng không.
/// </summary>
public static class UpdatePolicy
{
    /// <summary>Khoảng tối thiểu giữa hai lần kiểm tra, dù config đặt ngắn hơn.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromHours(6);

    public static UpdateMode ParseMode(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "notify" => UpdateMode.Notify,
        "auto" => UpdateMode.Auto,
        "off" => UpdateMode.Off,
        _ => UpdateMode.Ask,
    };

    public static TimeSpan Interval(int checkHours) => TimeSpan.FromHours(checkHours) < MinInterval ? MinInterval : TimeSpan.FromHours(checkHours);

    /// <param name="lastCheck">lần kiểm tra gần nhất, kể cả lần lỗi (lỗi thì chờ đủ một chu kỳ, không thử lại liên tục)</param>
    public static bool ShouldCheck(UpdateMode mode, InstallKind kind, bool isLocalChannel, DateTimeOffset now, DateTimeOffset? lastCheck, int checkHours)
    {
        if (isLocalChannel || kind == InstallKind.Store || mode is UpdateMode.Ask or UpdateMode.Off) return false;
        // Đồng hồ máy từng bị chỉnh lùi: lần kiểm tra "ở tương lai" thì coi như đã tới hạn, tránh kẹt mãi.
        if (lastCheck is not { } last || last > now) return true;
        return now - last >= Interval(checkHours);
    }

    /// <summary>"v1.0.7" mới hơn "1.0.6"? Chuỗi không đọc được thì coi như không mới (không báo nhầm).</summary>
    public static bool IsNewer(string candidate, string current)
    {
        static Version? V(string s) => Version.TryParse(s.Trim().TrimStart('v', 'V').Split('-', '+')[0], out var v) ? v : null;
        return V(candidate) is { } c && (V(current) is not { } cur || c > cur);
    }

    /// <summary>Nguồn cập nhật hợp lệ: repo GitHub qua https, hoặc thư mục có sẵn trên máy (chỉ để test). Không nhận http trần.</summary>
    public static bool IsValidSource(string src, Func<string, bool> dirExists) =>
        src.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
        || (Path.IsPathFullyQualified(src) && !src.Contains("://", StringComparison.Ordinal) && dirExists(src));

    /// <summary>
    /// Lần trước app định cài <paramref name="applying"/>: giờ đang chạy bản đó (hoặc mới hơn) là đã lên, còn thấp hơn là cài lỗi
    /// (Velopack giữ bản cũ). Không có gì đang chờ thì None.
    /// </summary>
    public static ApplyOutcome ApplyResult(string? applying, string current) =>
        string.IsNullOrWhiteSpace(applying) ? ApplyOutcome.None
        : IsNewer(applying, current) ? ApplyOutcome.Failed : ApplyOutcome.Updated;

    /// <summary>Đang bật Tiết kiệm pin thì hoãn tải, chỉ báo có bản mới.</summary>
    public static bool CanDownload(bool batterySaver) => !batterySaver;
}
