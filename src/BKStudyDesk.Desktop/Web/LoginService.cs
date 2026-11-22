using Avalonia.Threading;
using BKStudyDesk.Desktop.Views;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace BKStudyDesk.Desktop.Web;

internal sealed class LoginService(Action<LoginView?> present, Action<string, string> progress)
{
    private readonly HiddenWeb _hidden = new();
    private LoginView? _view;
    private bool _busy;

    public void Start()
    {
        if (_view is not null) { _view.Retry(); return; }
        Run(silent: false);
    }

    public void Refresh()
    {
        if (_view is null) Run(silent: true);
    }

    private IDisposable? _lane;
    private bool _filled;

    private async void Run(bool silent)
    {
        if (_busy) return;
        _busy = true;
        try { _lane = await SsoGate.EnterAsync(CancellationToken.None); }
        catch (Exception e) when (e is not OutOfMemoryException) { _busy = false; Log.Warn($"Đăng nhập: {e.Message}"); return; }
        if (!silent) progress("check", "");
        _filled = false;
        var page = _hidden.Open(allow: null);
        var flow = new LoginFlow(page, (stage, message) =>
        {
            if (stage == "error" && _busy) { Fallback(silent, message); return; }
            if (stage == "done" && _filled) { SsoGate.FillResult(true); _filled = false; }
            if (!silent) progress(stage, message);
            else if (stage == "done") progress("refresh", "");
        }, mybk: !silent && Settings.Sync.MybkEnabled);
        page.Loaded += (_, ok) => { if (!ok && _busy) Dispatcher.UIThread.Post(() => Fallback(silent, "không mở được trang trường")); };
        flow.NeedPassword += async () =>
        {
            if (!silent) { Show(); return; }
            if (_filled || Platform.Credentials.Usable() is not { } c || !SsoGate.TryClaimFill(UserActivity.Active)) { Fallback(silent, "cần mật khẩu"); return; }
            _filled = true;
            try { if (await page.EvalAsync(SsoForm.FillScript(c.User, c.Password)) != "true") Fallback(silent, "trang đăng nhập không có form như mong đợi"); }
            catch (Exception e) when (e is not OutOfMemoryException) { Fallback(silent, e.Message); }
        };
        flow.Finished += StopHidden;
        flow.Start();
        DispatcherTimer.RunOnce(() => { if (!flow.IsDone && _busy) Fallback(silent, "quá 25 giây"); }, TimeSpan.FromSeconds(25));
    }

    private void Fallback(bool silent, string why)
    {
        if (_filled) { SsoGate.FillResult(false); _filled = false; }
        if (!silent) { Show(); return; }
        Log.Info($"Làm mới đăng nhập ngầm: không được ({why})");
        StopHidden();
    }

    private void StopHidden()
    {
        _hidden.Close();
        _busy = false;
        _lane?.Dispose();
        _lane = null;
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
