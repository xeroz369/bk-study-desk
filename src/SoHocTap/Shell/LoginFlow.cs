using Microsoft.Web.WebView2.Core;
using SoHocTap.Core;
using SoHocTap.Sources.Lms;

namespace SoHocTap.Shell;

/// <summary>
/// Login HCMUT một lần cho cả MyBK và LMS, chạy trên CoreWebView2 nào cũng được (ẩn hoặc nằm trong window):
///  1. SSO cho MyBK: /my/homeSSO.action → /app/login?type=cas → /app (lúc này đã có access token).
///  2. LMS: chỉ chạy khi chưa có token LMS hoặc token đã hết hạn: CAS của LMS (tự qua nhờ session SSO) → launch.php → bắt &lt;scheme&gt;://token.
/// Gặp trang nhập mật khẩu SSO thì raise <see cref="NeedPassword"/> (người dùng tự nhập, app không đọc mật khẩu).
/// Các stage báo ra: mybk · lms · done · error.
/// </summary>
internal sealed class LoginFlow(CoreWebView2 core, Action<string, string> progress)
{
    private enum Stage { Sso, LmsCas, LmsLaunch, Done }

    private Stage _stage = Stage.Sso;
    private string _launchUrl = "";
    private bool _tokenCaught;
    private bool _sawPassword;   // flow này đã gặp trang nhập mật khẩu: xong là một lần đăng nhập đầy đủ (đo phiên SSO)

    public event Action? NeedPassword;
    public event Action? Finished;
    public bool IsDone => _stage == Stage.Done;

    public void Start()
    {
        core.LaunchingExternalUriScheme += (_, e) => Catch(e.Uri, () => e.Cancel = true);
        core.NavigationStarting += (_, e) => Catch(e.Uri, () => e.Cancel = true);
        core.NavigationCompleted += async (_, _) => await OnLoadedAsync();
        core.Navigate(Config.Str("sources.mybk.casLogin"));
    }

    private async Task OnLoadedAsync()
    {
        if (_stage == Stage.Done || !Uri.TryCreate(core.Source, UriKind.Absolute, out var u)) return;
        Log.Debug($"Đăng nhập: trang {Log.Where(core.Source)} ({_stage})");
        var ssoHost = WebHost.Host(Config.Str("sources.mybk.casLogin"));
        var lmsHost = WebHost.Host(Config.Str("sources.lms.site"));
        var mybkHost = WebHost.Host(Config.Str("sources.mybk.site"));

        if (u.Host == ssoHost && u.AbsolutePath.Contains("/login", StringComparison.Ordinal))
        {
            _sawPassword = true;
            NeedPassword?.Invoke();
            return;
        }
        if (_stage == Stage.Sso && u.Host == mybkHost)
        {
            if (u.AbsolutePath.StartsWith("/my/", StringComparison.Ordinal) || u.AbsolutePath.TrimEnd('/') == "/app/login")
                core.Navigate(Config.Str("sources.mybk.appLogin"));
            else if (u.AbsolutePath.StartsWith("/app", StringComparison.Ordinal) && !u.AbsolutePath.Contains("/login") && !u.AbsolutePath.Contains("/401"))
            {
                await SessionKeeper.PersistAsync(core);
                progress("mybk", "");
                if (await LmsClient.TokenValidAsync()) await FinishAsync();
                else
                {
                    _stage = Stage.LmsCas;
                    _launchUrl = LmsClient.NewLaunchUrl();
                    core.Navigate(Config.Str("sources.lms.casLogin"));
                }
            }
        }
        else if (_stage == Stage.LmsCas && u.Host == lmsHost && !u.AbsolutePath.Contains("/login/"))
        {
            _stage = Stage.LmsLaunch;
            core.Navigate(_launchUrl);
        }
    }

    private void Catch(string uri, Action cancel)
    {
        if (!uri.StartsWith(LmsClient.Scheme + "://", StringComparison.OrdinalIgnoreCase) || _tokenCaught) return;
        cancel();
        _tokenCaught = true;
        _ = FinishTokenAsync(uri);
    }

    private async Task FinishTokenAsync(string uri)
    {
        try
        {
            var name = await LmsClient.FinishLoginAsync(uri, CancellationToken.None);
            progress("lms", name);
            await FinishAsync();
        }
        catch (Exception e)
        {
            _tokenCaught = false;
            Log.Error("Login LMS", e);
            progress("error", e.Message);
        }
    }

    private async Task FinishAsync()
    {
        _stage = Stage.Done;
        if (_sawPassword) SsoSession.MarkLogin();
        else SsoSession.MarkAlive();
        await SessionKeeper.PersistAsync(core);
        progress("done", "");
        Finished?.Invoke();
    }
}
