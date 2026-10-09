using Avalonia.Threading;
using BKStudyDesk.Desktop.Views;
using SoHocTap.Core;

namespace BKStudyDesk.Desktop.Web;

/// <summary>
/// Một chỗ đăng nhập HCMUT cho LMS và MyBK (như Shell/LoginService bản 1.x): thử ngầm trước bằng trang ẩn, phiên SSO còn thì trường tự
/// cấp vé mới, xong luôn, không hiện gì. Cần mật khẩu (hay thử ngầm lỗi, quá 25 giây) thì đưa LoginView cho <paramref name="present"/>
/// hiện ngay trong cửa sổ chính; xong hay hủy thì present(null). Không mở cửa sổ riêng.
/// Các bước báo ra: check, password, mybk, lms, done, cancel, error; lượt làm mới ngầm (Refresh) chỉ báo "refresh" khi xong.
/// </summary>
internal sealed class LoginService(Action<LoginView?> present, Action<string, string> progress)
{
    private readonly HiddenWeb _hidden = new();
    private LoginView? _view;
    private bool _busy;

    /// <summary>Người dùng bấm Đăng nhập: thử ngầm, cần mật khẩu thì hiện trang đăng nhập.</summary>
    public void Start()
    {
        if (_view is not null) { _view.Retry(); return; }   // đang hiện trang đăng nhập: bấm Đăng nhập lại là tải lại trang
        Run(silent: false);
    }

    /// <summary>
    /// Làm mới ngầm (token LMS hết hạn): đi lại lượt đăng nhập trên trang ẩn. Phiên SSO còn thì lấy token mới, người dùng không thấy gì.
    /// SSO đòi mật khẩu: người dùng đã bật Tự đăng nhập lại (Credentials.Usable) thì điền tài khoản một lần, không thì thôi (dải báo
    /// đăng nhập lại hiện như thường). Không bao giờ hiện trang đăng nhập.
    /// </summary>
    public void Refresh()
    {
        if (_view is null) Run(silent: true);
    }

    private void Run(bool silent)
    {
        if (_busy) return;
        _busy = true;
        if (!silent) progress("check", "");
        var filled = false;
        var page = _hidden.Open(allow: null);
        var flow = new LoginFlow(page, (stage, message) =>
        {
            if (stage == "error" && _busy) { Fallback(silent, message); return; }
            if (!silent) progress(stage, message);
            else if (stage == "done") progress("refresh", "");
        });
        page.Loaded += (_, ok) => { if (!ok && _busy) Dispatcher.UIThread.Post(() => Fallback(silent, "không mở được trang trường")); };   // khỏi chờ 25 giây
        flow.NeedPassword += async () =>
        {
            if (!silent) { Show(); return; }
            if (filled || Platform.Credentials.Usable() is not { } c) { Fallback(silent, "cần mật khẩu"); return; }
            filled = true;   // một lần: sai mật khẩu, captcha thì trang mật khẩu hiện lại và dừng, tránh bị trường khóa tài khoản
            // Handler của event (async void): lỗi script phải bắt ở đây, không thì lọt ra UI thread.
            try { if (await page.EvalAsync(SsoForm.FillScript(c.User, c.Password)) != "true") Fallback(silent, "trang đăng nhập không có form như mong đợi"); }
            catch (Exception e) when (e is not OutOfMemoryException) { Fallback(silent, e.Message); }
        };
        flow.Finished += StopHidden;
        flow.Start();
        DispatcherTimer.RunOnce(() => { if (!flow.IsDone && _busy) Fallback(silent, "quá 25 giây"); }, TimeSpan.FromSeconds(25));
    }

    /// <summary>Thử ngầm không xong: lượt người dùng bấm thì hiện trang đăng nhập; lượt làm mới ngầm thì dừng, ghi log.</summary>
    private void Fallback(bool silent, string why)
    {
        if (!silent) { Show(); return; }
        Log.Info($"Làm mới đăng nhập ngầm: không được ({why})");
        StopHidden();
    }

    private void StopHidden()
    {
        _hidden.Close();
        _busy = false;
    }

    private void Show()
    {
        StopHidden();
        if (_view is not null) return;
        Log.Debug("Đăng nhập: cần mật khẩu, hiện trang đăng nhập");
        _view = new LoginView(progress);
        _view.Finished += () => { _view = null; present(null); };
        present(_view);
    }
}
