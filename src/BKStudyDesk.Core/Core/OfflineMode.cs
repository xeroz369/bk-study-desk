namespace SoHocTap.Core;

/// <summary>
/// Chế độ Offline (nút gạt cạnh Đồng bộ ngay): app thôi tự kết nối tới trường. Không đồng bộ theo lịch, không giữ phiên SSO,
/// không đọc tin nhắn mỗi 30 phút, không làm mới thư viện, không kiểm tra cập nhật; dữ liệu đã lưu vẫn xem được như thường.
/// Việc người dùng tự bấm (Đồng bộ ngay, đăng nhập, mở trang trường) vẫn chạy: một lượt rồi thôi. Lưu trong config (app.offline).
/// </summary>
public static class OfflineMode
{
    public static bool On => Config.Bool("app.offline", false);

    /// <summary>Vừa bật hay tắt (bắn trên thread gọi Set).</summary>
    public static event Action<bool>? Changed;

    public static void Set(bool on)
    {
        if (on == On) return;
        Config.Set("app.offline", on);
        Log.Info(on ? "Bật chế độ Offline" : "Tắt chế độ Offline");
        Changed?.Invoke(on);
    }
}
