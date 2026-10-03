namespace SoHocTap.Data;

/// <summary>
/// Loại lỗi của một lượt đồng bộ. UI dựa vào loại này để chọn câu báo và nút (đăng nhập lại, thử lại), không dò chữ
/// tiếng Việt trong message. Lưu dạng tên (chuỗi) trong sync-state.json cạnh message cũ để bản cũ vẫn đọc được.
/// </summary>
public enum SyncErrorKind
{
    /// <summary>Phiên HCMUT/LMS/MyBK đã hết: cần người dùng đăng nhập lại.</summary>
    SessionExpired,
    /// <summary>Không kết nối được (mất mạng, DNS, WebView không mở được trang).</summary>
    Network,
    /// <summary>Server trường trả lỗi (HTTP 5xx, trang bảo trì, Moodle báo lỗi).</summary>
    Server,
    /// <summary>Server đang giới hạn (HTTP 429/503): app đã tạm dừng, lần sau tự thử lại.</summary>
    Throttled,
    /// <summary>Server không trả lời kịp (quá timeout của app hoặc của HttpClient).</summary>
    Timeout,
    /// <summary>Không ghi được file trên máy (file đang mở ở chương trình khác, thiếu quyền).</summary>
    FileBusy,
    /// <summary>Server trả về dữ liệu không đọc được (JSON lạ, thiếu trường bắt buộc).</summary>
    Data,
    /// <summary>Lỗi trong app (chưa phân loại được): người dùng nên gửi Chi tiết khi báo lỗi.</summary>
    Bug,
}

/// <summary>Lỗi đồng bộ đã biết loại, dùng khi chỗ throw biết rõ nguyên nhân hơn loại exception.</summary>
public sealed class SyncException(SyncErrorKind kind, string message, Exception? inner = null) : Exception(message, inner)
{
    public SyncErrorKind Kind { get; } = kind;
}

public static class SyncErrorText
{
    /// <summary>Key câu báo trong lang/*.json; câu nhận {0} là tên nguồn (LMS, MyBK).</summary>
    public static string LangKey(SyncErrorKind kind) => kind switch
    {
        SyncErrorKind.SessionExpired => "error.sessionExpired",
        SyncErrorKind.Network => "error.network",
        SyncErrorKind.Server => "error.server",
        SyncErrorKind.Throttled => "error.throttled",
        SyncErrorKind.Timeout => "error.timeout",
        SyncErrorKind.FileBusy => "error.fileBusy",
        SyncErrorKind.Data => "error.data",
        _ => "error.generic",
    };

    /// <summary>Đọc lại tên loại đã lưu (sync-state.json); tên lạ hoặc không có thì null.</summary>
    public static SyncErrorKind? Parse(string? name) =>
        Enum.TryParse<SyncErrorKind>(name, ignoreCase: false, out var k) && Enum.IsDefined(k) ? k : null;
}
