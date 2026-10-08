namespace SoHocTap.Ui.SettingsCards;

// Namespace là SettingsCards (không phải Ui.Settings như tên thư mục): namespace SoHocTap.Ui.Settings sẽ che lớp Core.Settings,
// mọi file trong SoHocTap.Ui viết Settings.Notify... sẽ báo lỗi.

/// <summary>
/// Một thẻ của trang Cài đặt (UserControl trong Ui/Settings). Đổi là lưu ngay qua Core.Settings, không có nút Lưu chung.
/// Thêm thẻ mới: một file ở đây và một dòng trong <see cref="SettingsSections"/>.
/// </summary>
internal interface ISettingsSection
{
    /// <summary>Khóa trong lang/vi.json của tiêu đề thẻ (test kiểm không trùng và có chữ).</summary>
    string TitleKey { get; }

    /// <summary>Đọc lại giá trị đang lưu vào control. Gọi khi dựng thẻ và mỗi lần trang Cài đặt vẽ lại; ô đang gõ thì giữ nguyên.</summary>
    void Load();

    /// <summary>Lưu chữ còn đang gõ dở trong ô (trang Cài đặt bị ẩn, đổi trang, thoát app). Thẻ không có ô chữ thì không cần làm gì.</summary>
    void Flush() { }
}
