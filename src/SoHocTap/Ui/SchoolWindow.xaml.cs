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
            // Link mở window mới: web của trường thì ở lại trong app, trang khác thì đẩy ra trình duyệt.
            core.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (WebHost.IsSchoolHost(e.Uri)) core.Navigate(e.Uri);
                else Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
            };
            core.Navigate(url);
        };
    }
}
