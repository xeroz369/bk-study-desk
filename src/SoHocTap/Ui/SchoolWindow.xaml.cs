using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace SoHocTap.Ui;

public partial class SchoolWindow : Window
{
    // Trang trường tải quá 10 giây thì báo "đang chậm" kèm nút Thử lại (Nielsen: 10 s là giới hạn giữ được chú ý của người dùng);
    // thanh tải chỉ hiện sau 1 giây (Fluent 2 wait UX: dưới 1 giây không hiện chỉ báo).
    private readonly DispatcherTimer _slow = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly DispatcherTimer _showLoading = new() { Interval = TimeSpan.FromSeconds(1) };

    public SchoolWindow(string url, string title)
    {
        InitializeComponent();
        Title = string.IsNullOrWhiteSpace(title) ? WebHost.Host(url) : title;
        Loaded += async (_, _) =>
        {
            await Web.EnsureCoreWebView2Async(await WebHost.EnvironmentAsync());
            var core = Web.CoreWebView2;
            core.Settings.AreDevToolsEnabled = System.Diagnostics.Debugger.IsAttached;
            core.DocumentTitleChanged += (_, _) => Title = core.DocumentTitle is { Length: > 0 } t ? t : Title;
            core.NavigationStarting += (_, e) =>
            {
                Log.Debug($"Cửa sổ trường: mở {Log.Where(e.Uri)}");
                _slow.Stop();
                _slow.Start();
                _showLoading.Stop();
                _showLoading.Start();
            };
            _showLoading.Tick += (_, _) => { _showLoading.Stop(); Loading.Visibility = Visibility.Visible; };
            _slow.Tick += (_, _) =>
            {
                _slow.Stop();
                Log.Debug($"Cửa sổ trường: {Log.Where(core.Source)} tải quá 10 giây");
                ShowBar(Severity.Warning, L.F("web.slow", WebHost.Host(core.Source)), L.T("web.retry"), () => core.Reload());
            };
            core.NavigationCompleted += async (_, e) =>
            {
                _slow.Stop();
                _showLoading.Stop();
                Loading.Visibility = Visibility.Collapsed;
                Log.Debug($"Cửa sổ trường: {Log.Where(core.Source)} {(e.IsSuccess ? "ok" : e.WebErrorStatus.ToString())} HTTP {e.HttpStatusCode}");
                ShowStatus(core, e);
                await SessionKeeper.PersistAsync(core);
            };
            // Phiên LMS/MyBK trên server hết hạn sớm hơn cookie (cookie giữ 30 ngày, phiên server vài giờ), nên trang trường
            // hiện "session timed out" / trang đăng nhập. Khi đó đi qua cổng SSO một lần: SSO còn phiên thì tự vào lại, không thì
            // hiện trang đăng nhập HCMUT. Giới hạn 1 lần / 60 giây để không lặp vòng chuyển trang.
            var lastSso = DateTime.MinValue;
            core.NavigationCompleted += (_, _) =>
            {
                if (DateTime.UtcNow - lastSso < TimeSpan.FromSeconds(60) || WebHost.SsoEntry(core.Source) is not { } entry) return;
                lastSso = DateTime.UtcNow;
                Log.Debug($"Cửa sổ trường: phiên {WebHost.Host(core.Source)} hết, vào lại qua SSO");
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
        Closed += (_, _) => { _slow.Stop(); _showLoading.Stop(); };
    }

    /// <summary>Báo trạng thái trang vừa tải: mất mạng, server lỗi/giới hạn, hay phải đăng nhập lại; tải bình thường thì ẩn thanh báo.</summary>
    private void ShowStatus(CoreWebView2 core, CoreWebView2NavigationCompletedEventArgs e)
    {
        var host = WebHost.Host(core.Source);
        if (WebHost.NetworkError(e) is { } net) ShowBar(Severity.Error, net, L.T("web.retry"), () => core.Reload());
        else if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
            ShowBar(Severity.Error, L.F("web.failed", host, e.WebErrorStatus), L.T("web.retry"), () => core.Reload());
        else if (e.HttpStatusCode == 429) ShowBar(Severity.Warning, L.F("web.throttled", host), null, null);
        else if (e.HttpStatusCode >= 500) ShowBar(Severity.Error, L.F("web.serverError", host, e.HttpStatusCode), L.T("web.retry"), () => core.Reload());
        else if (WebHost.IsSsoLogin(core.Source)) ShowBar(Severity.Warning, L.T("web.sessionExpired"), null, null);
        else Bar.Hide();
    }

    private void ShowBar(Severity severity, string text, string? action, Action? run) => Bar.Show(severity, "", text, action, run);
}
