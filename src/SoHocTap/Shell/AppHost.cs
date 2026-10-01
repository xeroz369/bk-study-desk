using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
#if !PUBLIC_EDITION
using SoHocTap.Api;
#endif
using SoHocTap.Core;
using SoHocTap.Sources;
using SoHocTap.Sources.Lms;
using SoHocTap.Sources.Mybk;
using SoHocTap.Ui;

namespace SoHocTap.Shell;

/// <summary>
/// Composition root của app: các source, scheduler, login, tray, nhắc hạn, API cho khung Luyện tập.
/// UI (MainWindow) chỉ đọc <see cref="State"/> và gọi các lệnh ở đây.
/// </summary>
internal sealed class AppHost : IDisposable
#if !PUBLIC_EDITION
    , IShellActions
#endif
{
    private readonly Window _main;
    private readonly Dispatcher _ui;
    private readonly MybkRunner _mybk;
    private readonly Tray _tray;
    private readonly DeadlineNotifier _notifier;
    private readonly DispatcherTimer _notifyTimer = new() { Interval = TimeSpan.FromMinutes(5) };
    private readonly LoginService _login;

    public SourceHub Hub { get; }
#if !PUBLIC_EDITION
    public ApiRouter Router { get; }
#endif
    public AppState State { get; }

    /// <summary>Tiến độ login (stage, message), window chính hiện lên InfoBar.</summary>
    public event Action<string, string>? LoginProgress;
    /// <summary>Bấm vào thông báo ở tray thì mở page này.</summary>
    public event Action<string>? Navigate;

    public AppHost(Window main)
    {
        _main = main;
        _ui = main.Dispatcher;
        _mybk = new MybkRunner(_ui, () => new WindowInteropHelper(_main).Handle);
        Hub = new SourceHub([new LmsSource(), new MybkSource(() => _mybk)]);
#if !PUBLIC_EDITION
        Router = new ApiRouter(this);
#endif
        State = new AppState(Hub);
        _login = new LoginService(main, (stage, msg) => _ui.InvokeAsync(() => OnLogin(stage, msg)));

        _tray = new Tray(Environment.ProcessPath ?? "");
        _tray.Open += ShowMain;
        _tray.Sync += () => SyncAll();
        _tray.Exit += () => ((MainWindow)_main).Exit();
        _tray.Navigate += page => Navigate?.Invoke(page);
        _notifier = new DeadlineNotifier((t, b, p) => _ui.InvokeAsync(() => _tray.Show(t, b, p)));
        _notifyTimer.Tick += (_, _) => _notifier.Check();

        Hub.Changed += (name, stage) => _ui.InvokeAsync(() =>
        {
            if (stage == "done") State.Reload(); else State.RefreshStatus();
            if (name == "mybk" && stage == "done") _mybk.Release();
            if (name == "lms" && stage == "done") _notifier.Check();
        });
    }

    public void Start()
    {
        State.Reload();
        Hub.StartScheduler();
        _notifier.Check();
        _notifyTimer.Start();
    }

    public void SyncAll(bool force = false)
    {
        Hub.Start("lms", force);
        Hub.Start("mybk", force);
    }

    private void OnLogin(string stage, string message)
    {
        LoginProgress?.Invoke(stage, message);
        if (stage is "done" or "logout") MybkSource.SetSignedIn(stage == "done");
        if (stage == "done") SyncAll();
        State.RefreshStatus();
    }

    private void ShowMain() => ((MainWindow)_main).ShowFromTray();

    /// <summary>Lần đầu bấm X (app thu xuống tray) thì báo là app vẫn đang chạy.</summary>
    public void TrayHint() => _tray.Show(AppInfo.Name, L.T("tray.stillRunning"), "");

    // ------------------------------------------------------------------ các lệnh của app

    public void Login() => _ui.InvokeAsync(async () => await _login.StartAsync());

    /// <summary>Logout HCMUT khỏi app: xóa token LMS và toàn bộ cookie của profile WebView2 (session SSO, MyBK).</summary>
    public void Logout() => _ui.InvokeAsync(async () =>
    {
        LmsClient.Logout();
        var env = await WebHost.EnvironmentAsync();
        var c = await env.CreateCoreWebView2ControllerAsync(new WindowInteropHelper(_main).Handle);
        c.CoreWebView2.CookieManager.DeleteAllCookies();
        c.Close();
        OnLogin("logout", "");
    });

    public bool OpenWeb(string url, string title)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out _)) return false;
        if (!WebHost.IsSchoolHost(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        _ui.InvokeAsync(() => new SchoolWindow(url, title).Show());
        return true;
    }

    public bool SyncLms() => Hub.Start("lms");

    public bool OpenSource(string name) => name switch
    {
        "lms" => OpenWeb(Config.Str("sources.lms.site"), "BK-LMS"),
        "mybk" => OpenWeb(Config.Str("sources.mybk.home"), "MyBK"),
        _ => false,
    };

    public void Dispose()
    {
        _notifyTimer.Stop();
        _tray.Dispose();
        Hub.Dispose();
        _mybk.Dispose();
    }
}
