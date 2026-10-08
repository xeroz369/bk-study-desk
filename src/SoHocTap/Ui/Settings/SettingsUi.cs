using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using SoHocTap.Core;

namespace SoHocTap.Ui.SettingsCards;

/// <summary>Phần dùng chung của các thẻ Cài đặt: lưu có báo lỗi dưới thẻ, ghi ô chữ mà không đè lúc đang gõ, khởi động lại app.</summary>
internal static class SettingsUi
{
    /// <summary>Ghi cấu hình; ghi file lỗi thì hiện lý do ở <paramref name="error"/> (chữ màu Critical, ẩn khi không có lỗi).</summary>
    public static bool Save(Action write, TextBlock error)
    {
        try
        {
            write();
            Clear(error);
            return true;
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            Show(error, L.F("settings.saveError", x.Message));
            return false;
        }
    }

    public static void Show(TextBlock error, string text)
    {
        error.Text = text;
        error.Visibility = Visibility.Visible;
        // Dòng lỗi có LiveSetting=Assertive trong XAML: báo cho trình đọc màn hình đọc ngay.
        UIElementAutomationPeer.CreatePeerForElement(error)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    public static void Clear(TextBlock error)
    {
        error.Text = "";
        error.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Mất focus bàn phím vì mở menu chuột phải của ô (Dán, Cắt): người dùng vẫn đang sửa, chưa lưu. Ô trống rồi Dán mà lưu lúc này
    /// thì sẽ báo lỗi và trả về giá trị cũ.
    /// </summary>
    public static bool ToMenu(System.Windows.Input.KeyboardFocusChangedEventArgs e) => e.NewFocus is ContextMenu or MenuItem;

    /// <summary>Đặt chữ cho ô, trừ khi người dùng đang gõ trong ô đó (trang vẽ lại giữa chừng không làm mất chữ đang gõ).</summary>
    public static void ShowText(TextBox box, string value)
    {
        if (!box.IsKeyboardFocusWithin) box.Text = value;
    }

    /// <summary>Launch bản mới với --restart (bản mới đợi bản này nhả mutex single-instance, xem Program.cs) rồi thoát.</summary>
    public static void Restart()
    {
        if (Environment.ProcessPath is not { } exe) return;
        try { Process.Start(new ProcessStartInfo(exe, "--restart") { UseShellExecute = false }); }
        catch (System.ComponentModel.Win32Exception x)
        {
            Log.Error("Restart app lỗi", x);
            return;
        }
        Application.Current.Shutdown();
    }
}
