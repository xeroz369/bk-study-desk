using System.Windows;
using System.Windows.Controls;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace SoHocTap.Ui.SettingsCards;

public partial class AccountSection : UserControl, ISettingsSection
{
    private readonly AppHost _host;

    internal AccountSection(AppHost host)
    {
        InitializeComponent();
        _host = host;
        Load();
    }

    public string TitleKey => "settings.account";

    public void Load()
    {
        var s = _host.State;
        var need = s.Account;
        AccountText.Text = need == AccountNeed.None ? (s.Lms?.User is { } u ? L.F("settings.signedInAs", u) : L.T("settings.signedIn")) : L.T("settings.needLogin");
        AccountDetail.Text = L.F("settings.accountDetail",
            L.T(need is AccountNeed.Lms or AccountNeed.Both ? "settings.lms.need" : "settings.lms.ok"),
            L.T(need is AccountNeed.Mybk or AccountNeed.Both ? "settings.mybk.expired" : s.Mybk is null ? "settings.mybk.never" : "settings.mybk.synced"));
        LoginButton.Content = L.T(need == AccountNeed.None ? "settings.relogin" : "common.loginHcmut");
        LogoutButton.IsEnabled = true;   // luôn cho logout vì có thể vẫn còn session SSO/MyBK dù chưa có dữ liệu LMS
        Remember.IsChecked = Settings.Sso.RememberDays > 0;
        KeepAlive.IsChecked = Settings.Sso.KeepAliveMinutes > 0;
        AutoLogin.IsEnabled = Settings.Sso.RememberDays > 0;
        AutoLogin.IsChecked = Settings.Sso.AutoLogin && AutoLogin.IsEnabled;
    }

    private void OnLogin(object sender, RoutedEventArgs e) => _host.Login();

    private void OnLogout(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this)!, L.T("settings.logoutConfirm"), AppInfo.Name,
                MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK)
            _host.Logout();
    }

    /// <summary>Ghi nhớ đăng nhập: lưu ngay sso.rememberDays (bật là số ngày mặc định, tắt là 0). Nếu tắt thì từ lần login sau, tắt app là mất session.</summary>
    private void OnRemember(object sender, RoutedEventArgs e)
    {
        var on = Remember.IsChecked == true;
        // Không ghi nhớ đăng nhập thì cũng không giữ mật khẩu để tự đăng nhập.
        SettingsUi.Save(() => { Settings.Sso.RememberDays = on ? Settings.Sso.DefaultRememberDays : 0; if (!on) SsoCredentials.Forget(); }, Error);
        Load();
    }

    /// <summary>Bật thì hỏi tài khoản (hủy thì để tắt như cũ); tắt thì xóa tài khoản đã lưu.</summary>
    private void OnAutoLogin(object sender, RoutedEventArgs e)
    {
        if (AutoLogin.IsChecked == true) SettingsUi.Save(() => AutoLoginWindow.Enable(Window.GetWindow(this)!), Error);
        else SettingsUi.Save(SsoCredentials.Forget, Error);
        Load();
    }

    /// <summary>Giữ phiên SSO khi app mở: lưu ngay sso.keepAliveMinutes.</summary>
    private void OnKeepAlive(object sender, RoutedEventArgs e) =>
        SettingsUi.Save(() => Settings.Sso.KeepAliveMinutes = KeepAlive.IsChecked == true ? Settings.Sso.DefaultKeepAliveMinutes : 0, Error);
}
