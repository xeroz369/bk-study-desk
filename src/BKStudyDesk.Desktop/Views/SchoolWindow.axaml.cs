using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BKStudyDesk.Desktop.Web;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Ui;
using SoHocTap.Web;

namespace BKStudyDesk.Desktop.Views;

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
        Retry.IsVisible = Status.IsVisible = false;
        Address.Text = url;
        _slow.Stop();
        _slow.Start();
        _page.Navigate(url);
    }

    private async void OnLoaded(Uri? u, bool ok)
    {
        _slow.Stop();
        if (u is null) return;
        if (!ok) { Problem(L.T("web.offline")); return; }
        Retry.IsVisible = Status.IsVisible = false;
        Address.Text = u.AbsoluteUri;
        BackButton.IsEnabled = _page.View.CanGoBack;
        ForwardButton.IsEnabled = _page.View.CanGoForward;
        await SsoKeep.PersistAsync(_page);
        var where = u.AbsoluteUri;
        if (SchoolUrls.IsSsoLogin(where)) { _sawLogin = true; return; }
        if (!_ssoTried && SchoolUrls.SsoEntry(where) is { } entry)
        {
            _ssoTried = true;
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
        ToolTip.SetTip(Status, text);
        Status.IsVisible = Retry.IsVisible = true;
    }

    private void OnBack(object? sender, RoutedEventArgs e) { if (_page.View.CanGoBack) _page.View.GoBack(); }

    private void OnForward(object? sender, RoutedEventArgs e) { if (_page.View.CanGoForward) _page.View.GoForward(); }

    private void OnAddressKey(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Avalonia.Input.Key.Enter) return;
        e.Handled = true;
        var text = (Address.Text ?? "").Trim();
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;
        if (Uri.TryCreate(text, UriKind.Absolute, out var u) && u.Scheme is "http" or "https") Go(SchoolUrls.Secure(u.AbsoluteUri));
    }

    private void OnRetry(object? sender, RoutedEventArgs e) => Go(_page.Source?.AbsoluteUri is { } cur && cur != "about:blank" ? cur : _url);

    private async void OnBrowser(object? sender, RoutedEventArgs e)
    {
        if (Uri.TryCreate(_page.Source?.AbsoluteUri ?? _url, UriKind.Absolute, out var u)) await Launcher.LaunchUriAsync(u);
    }
}
