using Avalonia.Controls;
using Avalonia.Threading;
using BKStudyDesk.Desktop.Views;
using SoHocTap.Core;

namespace BKStudyDesk.Desktop.Web;

/// <summary>
/// Một chỗ đăng nhập HCMUT cho LMS và MyBK (như Shell/LoginService bản 1.x): thử ngầm trước bằng trang ẩn, phiên SSO còn thì xong
/// luôn, không hiện gì. Cần mật khẩu (hay thử ngầm quá 25 giây) thì mới mở cửa sổ đăng nhập để người dùng tự gõ.
/// Các bước báo ra: check, password, mybk, lms, done, cancel, error.
/// </summary>
internal sealed class LoginService(Window owner, Action<string, string> progress)
{
    private readonly HiddenWeb _hidden = new();
    private LoginWindow? _window;
    private bool _busy;

    public void Start()
    {
        if (_window is { IsVisible: true }) { _window.Activate(); return; }
        if (_busy) return;
        _busy = true;
        progress("check", "");
        // Thử ngầm lỗi (trang trường lỗi, không có token): mở cửa sổ để người dùng thấy trang và thử lại.
        var page = _hidden.Open(allow: null);
        page.Loaded += (_, ok) => { if (!ok && _busy) Avalonia.Threading.Dispatcher.UIThread.Post(ShowWindow); };   // trang lỗi: hiện cửa sổ ngay, khỏi chờ 25 giây
        var flow = new LoginFlow(page, (stage, message) =>
        {
            if (stage == "error" && _busy) { ShowWindow(); return; }
            progress(stage, message);
        });
        flow.NeedPassword += ShowWindow;
        flow.Finished += StopHidden;
        flow.Start();
        DispatcherTimer.RunOnce(() => { if (!flow.IsDone && _busy) ShowWindow(); }, TimeSpan.FromSeconds(25));
    }

    private void StopHidden()
    {
        _hidden.Close();
        _busy = false;
    }

    private void ShowWindow()
    {
        StopHidden();
        if (_window is { IsVisible: true }) { _window.Activate(); return; }
        Log.Debug("Đăng nhập: cần mật khẩu, mở cửa sổ");
        progress("password", "");
        _window = new LoginWindow(progress);
        _window.Show(owner);
    }
}
