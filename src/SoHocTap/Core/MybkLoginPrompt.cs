namespace SoHocTap.Core;

/// <summary>
/// MyBK hết phiên trong khi LMS (hạn nộp, tài liệu, nhắc hạn) vẫn chạy bằng token riêng: thanh báo "đăng nhập lại" chỉ hiện khi người dùng
/// đang cần MyBK. Lúc khác chỉ thanh trạng thái báo, lịch học, lịch thi, điểm vẫn hiện từ dữ liệu đã lưu. Hàm thuần.
/// </summary>
public static class MybkLoginPrompt
{
    /// <summary>Dữ liệu MyBK cũ hơn mức này (lịch thi, điểm có thể đã đổi) thì mời đăng nhập lại.</summary>
    public const long StaleAfter = 3 * 86400;

    /// <summary>Đợt đăng ký môn mở trong khoảng này (hay đang mở) thì mời đăng nhập lại, để kịp đăng ký.</summary>
    public const long RegistrationLead = 3 * 86400;

    /// <param name="onMybkPage">Đang mở trang dùng MyBK (Điểm và học vụ, Dịch vụ).</param>
    /// <param name="syncedAt">Lần đồng bộ MyBK thành công gần nhất; null là chưa có dữ liệu MyBK.</param>
    public static bool Needed(bool onMybkPage, long now, long? syncedAt, IEnumerable<(long Start, long End)> registrations) =>
        onMybkPage || syncedAt is not { } t || now - t > StaleAfter || registrations.Any(r => r.End > now && r.Start - now <= RegistrationLead);
}
