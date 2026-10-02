using System.Diagnostics;
using System.Windows;
using SoHocTap.Shell;

namespace SoHocTap.Ui;

public partial class SchoolWindow : Window
{
    public SchoolWindow(string url, string title)
    {
        InitializeComponent();
        Title = string.IsNullOrWhiteSpace(title) ? WebHost.Host(url) : title;
        Loaded += async (_, _) =>
        {
            await Web.EnsureCoreWebView2Async(await WebHost.EnvironmentAsync());
            var core = Web.CoreWebView2;
            core.DocumentTitleChanged += (_, _) => Title = core.DocumentTitle is { Length: > 0 } t ? t : Title;
            core.NavigationCompleted += async (_, _) => await SessionKeeper.PersistAsync(core);
            // Phiên LMS/MyBK trên server hết hạn sớm hơn cookie (cookie giữ 30 ngày, phiên server vài giờ), nên trang trường
            // hiện "session timed out" / trang đăng nhập. Khi đó đi qua cổng SSO một lần: SSO còn phiên thì tự vào lại, không thì
            // hiện trang đăng nhập HCMUT. Giới hạn 1 lần / 60 giây để không lặp vòng chuyển trang.
            var lastSso = DateTime.MinValue;
            core.NavigationCompleted += (_, _) =>
            {
                if (DateTime.UtcNow - lastSso < TimeSpan.FromSeconds(60) || WebHost.SsoEntry(core.Source) is not { } entry) return;
                lastSso = DateTime.UtcNow;
                core.Navigate(entry);
            };
            // Link mở window mới: web của trường thì ở lại trong app, trang khác thì đẩy ra trình duyệt.
            core.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (WebHost.IsSchoolHost(WebHost.Secure(e.Uri))) core.Navigate(WebHost.Secure(e.Uri));
                else Links.Open(e.Uri);
            };
            core.Navigate(url);
        };
    }
}
