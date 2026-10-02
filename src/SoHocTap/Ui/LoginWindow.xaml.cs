using System.Windows;
using SoHocTap.Shell;

namespace SoHocTap.Ui;

public partial class LoginWindow : Window
{
    private bool _done;

    internal LoginWindow(Action<string, string> progress)
    {
        InitializeComponent();
        // Màn hình nhỏ / scale lớn: không cao hơn vùng làm việc, kẻo nút Đăng nhập của trang SSO bị khuất dưới taskbar.
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 40);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 40);
        Loaded += async (_, _) =>
        {
            await Web.EnsureCoreWebView2Async(await WebHost.EnvironmentAsync());
            // Hai cài đặt này áp cho cả profile WebView2 (mọi cửa sổ trường dùng chung profile), không riêng cửa sổ này:
            // bật hỏi lưu mật khẩu (lưu hay không do người dùng bấm), tắt tự điền dữ liệu khác (mặc định của WebView2 là bật).
            Web.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
            Web.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            Web.CoreWebView2.Settings.AreDevToolsEnabled = System.Diagnostics.Debugger.IsAttached;
            var flow = new LoginFlow(Web.CoreWebView2, progress);
            flow.Finished += async () => { _done = true; await Task.Delay(800); Close(); };
            flow.Start();
        };
        Closed += (_, _) => { if (!_done) progress("cancel", ""); };
    }
}
