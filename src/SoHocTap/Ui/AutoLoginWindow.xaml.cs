using System.ComponentModel;
using System.Windows;
using SoHocTap.Shell;

namespace SoHocTap.Ui;

/// <summary>
/// Hộp nhập tài khoản để bật tự đăng nhập lại (sso.autoLogin). Lưu vào Windows Credential Manager (<see cref="SsoCredentials"/>),
/// không kiểm tra mật khẩu ngay (không gửi lên trường lúc này): sai thì lần hết phiên tới tự đăng nhập không được và app báo như thường.
/// </summary>
public partial class AutoLoginWindow : Window
{
    private AutoLoginWindow()
    {
        InitializeComponent();
        WindowPlacement.FitToWorkArea(this);
        Title = Heading.Text;
        UserBox.Text = SsoCredentials.Read()?.User ?? "";
        Loaded += (_, _) => (UserBox.Text.Length == 0 ? (UIElement)UserBox : PasswordBox).Focus();
    }

    /// <summary>Hỏi tài khoản rồi bật tính năng. Dùng chung cho thẻ Tài khoản và thanh hỏi lần đầu. true = đã bật.</summary>
    public static bool Enable(Window owner)
    {
        if (new AutoLoginWindow { Owner = owner }.ShowDialog() != true) return false;
        Core.Settings.Sso.AutoLogin = true;
        Core.Settings.Sso.AutoLoginAsked = true;
        return true;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var user = UserBox.Text.Trim();
        if (user.Length == 0 || PasswordBox.Password.Length == 0)
        {
            Show(L.T("autoLogin.empty"));
            return;
        }
        try { SsoCredentials.Save(user, PasswordBox.Password); }
        catch (Win32Exception x)
        {
            Show(L.F("settings.saveError", x.Message));
            return;
        }
        DialogResult = true;
    }

    private void Show(string text)
    {
        Error.Text = text;
        Error.Visibility = Visibility.Visible;
    }
}
