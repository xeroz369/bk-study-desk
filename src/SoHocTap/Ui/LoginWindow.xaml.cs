using System.Windows;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace SoHocTap.Ui;

public partial class LoginWindow : Window
{
    private bool _done;
    private bool _shown;   // trang trường đã hiện ít nhất một lần
    // Quá 20 giây chưa mở được trang nào: báo trang trường chậm, có nút Thử lại (không để cửa sổ trống mà không nói gì).
    private readonly System.Windows.Threading.DispatcherTimer _slow = new() { Interval = TimeSpan.FromSeconds(20) };

    internal LoginWindow(Action<string, string> progress)
    {
        InitializeComponent();
        // Màn hình nhỏ / scale lớn: không cao hơn vùng làm việc, kẻo nút Đăng nhập của trang SSO bị khuất dưới taskbar.
        WindowPlacement.FitToWorkArea(this);
        Loaded += async (_, _) =>
        {
            try
            {
                await Web.EnsureCoreWebView2Async(await WebHost.EnvironmentAsync());
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // Không tạo được WebView2 (runtime hỏng, thư mục profile bị khóa...): báo rõ thay vì để cửa sổ trống.
                Log.Error("Đăng nhập: không mở được WebView2", e);
                ShowProblem(L.F("login.webviewFailed", e.Message), retry: false);
                return;
            }
            // Hai cài đặt này áp cho cả profile WebView2 (mọi cửa sổ trường dùng chung profile), không riêng cửa sổ này:
            // bật hỏi lưu mật khẩu (lưu hay không do người dùng bấm), tắt tự điền dữ liệu khác (mặc định của WebView2 là bật).
            Web.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
            Web.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            Web.CoreWebView2.Settings.AreDevToolsEnabled = System.Diagnostics.Debugger.IsAttached;
            Web.CoreWebView2.NavigationCompleted += OnNavigated;
            var flow = new LoginFlow(Web.CoreWebView2, progress);
            flow.Finished += async () => { _done = true; await Task.Delay(800); Close(); };
            flow.Start();
            _slow.Tick += (_, _) => { _slow.Stop(); if (!_shown) ShowProblem(L.T("login.slow"), retry: true); };
            _slow.Start();
        };
        Closed += (_, _) => { _slow.Stop(); if (!_done) progress("cancel", ""); };
    }

    private void OnNavigated(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _shown = true;
            _slow.Stop();
            Status.Visibility = Visibility.Collapsed;
            Web.Visibility = Visibility.Visible;
            return;
        }
        if (_shown || e.WebErrorStatus == Microsoft.Web.WebView2.Core.CoreWebView2WebErrorStatus.OperationCanceled) return;
        Log.Warn($"Đăng nhập: không mở được trang ({e.WebErrorStatus}, HTTP {e.HttpStatusCode})");
        ShowProblem(WebHost.NetworkError(e) ?? L.F("login.failed", e.WebErrorStatus), retry: true);
    }

    private void ShowProblem(string text, bool retry)
    {
        Web.Visibility = Visibility.Hidden;
        Status.Visibility = Visibility.Visible;
        StatusBar.Visibility = Visibility.Collapsed;
        StatusText.Text = text;
        RetryButton.Visibility = retry ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRetry(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is null) return;
        _shown = false;
        StatusBar.Visibility = Visibility.Visible;
        StatusText.Text = L.T("login.opening");
        RetryButton.Visibility = Visibility.Collapsed;
        Web.CoreWebView2.Navigate(Config.Str("sources.mybk.casLogin"));
        _slow.Start();
    }
}
