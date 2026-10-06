using System.Windows.Controls;
using System.Windows.Input;

namespace SoHocTap.Ui.SettingsCards;

/// <summary>
/// Ô chữ của thẻ Cài đặt: lưu khi rời ô (cả khi mất focus bàn phím, ví dụ Ctrl+số đổi trang), bấm Enter, hoặc khi trang Cài đặt
/// đóng lại (<see cref="Flush"/>). <c>normalize</c> trả dạng chuẩn để lưu, null là sai dạng: hiện <c>invalid</c> ngay dưới ô và trả ô
/// về giá trị đang lưu. Lỗi chỉ mất khi lưu được hoặc người dùng sửa ô; trang vẽ lại không xóa lỗi.
/// </summary>
internal sealed class TextSetting
{
    private readonly TextBox _box;
    private readonly TextBlock _error;
    private readonly Func<string> _get;
    private readonly Func<string, string?> _normalize;
    private readonly Action<string> _set;
    private readonly string _invalid;
    private readonly Action? _saved;
    private string _committed = "";
    private bool _setting;

    public TextSetting(TextBox box, TextBlock error, Func<string> get, Func<string, string?> normalize, Action<string> set, string invalid, Action? saved = null)
    {
        (_box, _error, _get, _normalize, _set, _invalid, _saved) = (box, error, get, normalize, set, invalid, saved);
        box.LostFocus += (_, _) => Flush();
        box.LostKeyboardFocus += (_, e) =>
        {
            if (!SettingsUi.ToMenu(e)) Flush();
        };
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Flush();
        };
        box.TextChanged += (_, _) =>
        {
            if (!_setting) SettingsUi.Clear(_error);   // người dùng đang sửa: lỗi cũ không còn đúng
        };
    }

    private void Show(string text)
    {
        _setting = true;
        try { _box.Text = _committed = text; }
        finally { _setting = false; }
    }

    public void Load()
    {
        if (!_box.IsKeyboardFocusWithin) Show(_get());
    }

    /// <summary>Lưu chữ đang gõ nếu khác lần lưu trước (gọi nhiều lần cũng chỉ ghi một lần).</summary>
    public void Flush()
    {
        if (_box.Text == _committed) return;
        if (_normalize(_box.Text) is not { } v)
        {
            Show(_get());
            SettingsUi.Show(_error, _invalid);
            return;
        }
        Show(v);
        if (v == _get()) SettingsUi.Clear(_error);
        else if (SettingsUi.Save(() => _set(v), _error)) _saved?.Invoke();
        else Show(_get());
    }
}
