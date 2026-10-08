using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Sources.Lms;
using SoHocTap.Ui;
using SoHocTap.Web;

namespace BKStudyDesk.Desktop.Web;

/// <summary>
/// Một lượt đăng nhập HCMUT cho cả MyBK và LMS trên một WebPage (ẩn hay trong cửa sổ). Trang nào làm gì: LoginSteps (thuần, có test).
/// Token LMS: lấy cookie LMS của trang rồi gọi launch.php (LmsTokenGrab), vì trình duyệt nhúng không cho bắt link scheme lạ.
/// Gặp trang mật khẩu SSO thì báo NeedPassword; app không đọc mật khẩu. Các bước báo ra: mybk, lms, done, error.
/// </summary>
internal sealed class LoginFlow(WebPage page, Action<string, string> progress)
{
    private readonly LoginHosts _hosts = LoginHosts.FromConfig();
    private LoginStage _stage = LoginStage.Sso;
    private bool _sawPassword;   // lượt này đã gặp trang mật khẩu: xong là một lần đăng nhập đầy đủ (đo thời gian sống của phiên SSO)
    private bool _busy;          // đang làm một bước async (lấy token), bỏ qua các lần tải trang chen vào

    public event Action? NeedPassword;
    public event Action? Finished;
    public bool IsDone => _stage == LoginStage.Done;

    public void Start()
    {
        page.Loaded += (u, ok) => { if (ok && u is not null) _ = OnLoadedAsync(u); };
        page.Navigate(Config.Str("sources.mybk.casLogin"));
    }

    private async Task OnLoadedAsync(Uri u)
    {
        if (_busy || IsDone) return;
        Log.Debug($"Đăng nhập: trang {Log.Where(u.AbsoluteUri)} ({_stage})");
        try
        {
            switch (LoginSteps.Next(_stage, u, _hosts))
            {
                case LoginAction.NeedPassword:
                    _sawPassword = true;
                    NeedPassword?.Invoke();
                    break;
                case LoginAction.GoAppLogin:
                    page.Navigate(Config.Str("sources.mybk.appLogin"));
                    break;
                case LoginAction.MybkReady:
                    _busy = true;
                    await SsoKeep.PersistAsync(page);
                    progress("mybk", "");
                    if (await LmsClient.TokenValidAsync()) await FinishAsync();
                    else
                    {
                        _stage = LoginStage.LmsCas;
                        page.Navigate(Config.Str("sources.lms.casLogin"));
                    }
                    break;
                case LoginAction.GoLaunch:
                    _busy = true;
                    var callback = await LmsTokenGrab.LaunchCallbackAsync(LmsTokenGrab.NoRedirectHandler(), LmsClient.NewLaunchUrl(),
                        await page.CookiesAsync(), LmsClient.Scheme, CancellationToken.None);
                    if (callback is null) { progress("error", L.T("login.noToken")); break; }
                    progress("lms", await LmsClient.FinishLoginAsync(callback, CancellationToken.None));
                    await FinishAsync();
                    break;
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Error("Đăng nhập", e);
            progress("error", e.Message);
        }
        finally { _busy = false; }
    }

    private async Task FinishAsync()
    {
        _stage = LoginStage.Done;
        if (_sawPassword) SsoSession.MarkLogin();
        else SsoSession.MarkAlive();
        await SsoKeep.PersistAsync(page);
        progress("done", "");
        Finished?.Invoke();
    }
}
