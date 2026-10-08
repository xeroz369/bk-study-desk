using Avalonia.Controls;
using Avalonia.Interactivity;
using BKStudyDesk.Desktop.Platform;
using SoHocTap.Core;

namespace BKStudyDesk.Desktop.Views;

/// <summary>Hộp thoại bật tự đăng nhập lại. Kết quả (ShowDialog): true khi đã lưu tài khoản và bật sso.autoLogin.</summary>
public partial class AutoLoginWindow : Window
{
    public AutoLoginWindow() => InitializeComponent();

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var user = User.Text?.Trim() ?? "";
        var password = Password.Text ?? "";
        if (user.Length == 0 || password.Length == 0) { Show(SoHocTap.Ui.L.T("autologin.missing")); return; }
        try
        {
            Credentials.Save(user, password);
            Settings.Sso.AutoLogin = true;
            Close(true);
        }
        catch (Exception x) when (x is not OutOfMemoryException)
        {
            Log.Warn($"Tự đăng nhập lại: không lưu được tài khoản: {x.Message}");
            Show(SoHocTap.Ui.L.T("autologin.saveFailed"));
        }
        finally { Password.Text = ""; }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private void Show(string text)
    {
        Error.Text = text;
        Error.IsVisible = true;
    }
}
