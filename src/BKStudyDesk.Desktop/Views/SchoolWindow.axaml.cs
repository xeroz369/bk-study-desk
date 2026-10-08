using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BKStudyDesk.Desktop.Web;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Ui;
using SoHocTap.Web;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Trang của trường trong cửa sổ app (cùng cách làm với 1.x, Ui/SchoolWindow), dùng chung phiên đăng nhập của app:
/// - tải quá app.webSlowSeconds giây thì báo chậm, có nút Thử lại; tải lỗi thì báo và có nút Thử lại;
/// - trang đăng nhập riêng của LMS, MyBK (phiên server hết sớm hơn cookie) thì đi qua cổng SSO một lần (SchoolUrls.SsoEntry);
/// - sau mỗi lần tải xong giữ hạn cookie trường (SsoKeep); thấy trang mật khẩu SSO rồi vào lại được trường thì ghi là vừa đăng nhập.
/// </summary>
public partial class SchoolWindow : Window
{
    private readonly WebPage _page = new();
    private readonly DispatcherTimer _slow = new() { Interval = TimeSpan.FromSeconds(Config.Int("app.webSlowSeconds", 10)) };
    private readonly string _url = "";
    private bool _sawLogin, _ssoTried;

    public SchoolWindow() => InitializeComponent();

    internal SchoolWindow(string url, string title) : this()
    {
        _url = url;
        Title = string.IsNullOrWhiteSpace(title) ? SchoolUrls.Host(url) : title;
        Host.Child = _page.View;
        _slow.Tick += (_, _) =>
        {
            _slow.Stop();
            Problem(L.F("web.slow", SchoolUrls.Host(_page.Source?.AbsoluteUri ?? url)));
        };
        _page.Loaded += OnLoaded;
        Opened += (_, _) => Go(url);
        Closed += (_, _) => _slow.Stop();
    }

    private void Go(string url)
    {
        Retry.IsVisible = false;
        Status.Text = SchoolUrls.Host(url);
        _slow.Stop();
        _slow.Start();
        _page.Navigate(url);
    }

    private async void OnLoaded(Uri? u, bool ok)
    {
        _slow.Stop();
        if (u is null) return;
        if (!ok) { Problem(L.T("web.offline")); return; }
        Retry.IsVisible = false;
        Status.Text = u.Host + u.AbsolutePath;
        await SsoKeep.PersistAsync(_page);
        var where = u.AbsoluteUri;
        if (SchoolUrls.IsSsoLogin(where)) { _sawLogin = true; return; }
        if (!_ssoTried && SchoolUrls.SsoEntry(where) is { } entry)
        {
            _ssoTried = true;   // một lần: SSO còn phiên thì tự vào lại, không thì dừng ở trang mật khẩu cho người dùng tự gõ
            Log.Debug($"Cửa sổ trường: {Log.Where(where)} là trang đăng nhập riêng, đi qua SSO");
            Go(entry);
            return;
        }
        if (_sawLogin && SchoolUrls.IsSchoolHost(where))
        {
            _sawLogin = false;
            SsoSession.MarkLogin();
            Log.Info($"Cửa sổ trường: đã đăng nhập lại ({u.Host})");
        }
    }

    private void Problem(string text)
    {
        Status.Text = text;
        Retry.IsVisible = true;
    }

    private void OnRetry(object? sender, RoutedEventArgs e) => Go(_page.Source?.AbsoluteUri is { } cur && cur != "about:blank" ? cur : _url);

    private async void OnBrowser(object? sender, RoutedEventArgs e)
    {
        if (Uri.TryCreate(_page.Source?.AbsoluteUri ?? _url, UriKind.Absolute, out var u)) await Launcher.LaunchUriAsync(u);
    }
}
