using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using SoHocTap.Core;

namespace SoHocTap.Shell;

/// <summary>
/// Một chỗ login HCMUT chung cho LMS và MyBK. Thử ngầm trước bằng WebView2 ẩn: session SSO còn hạn thì xong luôn, không hiện gì.
/// Cần mật khẩu (hoặc thử ngầm quá 25 giây) thì mới mở window login để người dùng tự nhập.
/// Các stage báo ra: check · password · mybk · lms · done · cancel · error.
/// </summary>
internal sealed class LoginService(Window owner, Action<string, string> progress)
{
    private CoreWebView2Controller? _hidden;
    private Ui.LoginWindow? _window;
    private bool _busy;

    public async Task StartAsync()
    {
        if (_window is { IsVisible: true }) { _window.Activate(); return; }
        if (_busy) return;
        _busy = true;
        progress("check", "");
        try
        {
            var env = await WebHost.EnvironmentAsync();
            _hidden = await env.CreateCoreWebView2ControllerAsync(new WindowInteropHelper(owner).Handle);
            _hidden.IsVisible = false;
            var flow = new LoginFlow(_hidden.CoreWebView2, progress);
            flow.NeedPassword += ShowWindow;
            flow.Finished += Close;
            flow.Start();
            await Task.Delay(TimeSpan.FromSeconds(25));
            if (!flow.IsDone && _hidden is not null) ShowWindow();
        }
        catch (Exception e)
        {
            Log.Error("Login ngầm", e);
            ShowWindow();
        }
    }

    private void Close()
    {
        _hidden?.Close();
        _hidden = null;
        _busy = false;
    }

    private void ShowWindow()
    {
        Close();
        if (_window is { IsVisible: true }) { _window.Activate(); return; }
        progress("password", "");
        _window = new Ui.LoginWindow(progress) { Owner = owner };
        _window.Show();
    }
}
