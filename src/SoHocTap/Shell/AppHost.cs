using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SoHocTap.Api;
using SoHocTap.Core;
using SoHocTap.Sources;
using SoHocTap.Sources.Lms;
using SoHocTap.Sources.Mybk;
using SoHocTap.Ui;
using SoHocTap.Updates;

namespace SoHocTap.Shell;

/// <summary>
/// Composition root của app: các source, scheduler, login, tray, nhắc hạn, API cho khung Luyện tập.
/// UI (MainWindow) chỉ đọc <see cref="State"/> và gọi các lệnh ở đây.
/// </summary>
internal sealed class AppHost : IDisposable
    , IShellActions
{
    private readonly Window _main;
    private readonly Dispatcher _ui;
    private readonly MybkRunner _mybk;
    private readonly Tray _tray;
    private readonly DeadlineNotifier _notifier;
    private readonly DispatcherTimer _notifyTimer = new() { Interval = TimeSpan.FromMinutes(5) };
    private readonly LoginService _login;

    public SourceHub Hub { get; }
    /// <summary>Cập nhật app (chỉ bản public; chỉ kiểm tra khi người dùng cho phép).</summary>
    public UpdateService Updates { get; } = new();
    public ApiRouter Router { get; }
    public AppState State { get; }
    /// <summary>Thư viện tài liệu chung (tắt mặc định; bật trong Cài đặt). Không gọi mạng khi tắt.</summary>
    public SoHocTap.Library.LibraryService Library { get; } = new();

    /// <summary>Tiến độ login (stage, message), window chính hiện lên InfoBar.</summary>
    public event Action<string, string>? LoginProgress;
    /// <summary>Bấm vào thông báo ở tray thì mở page này.</summary>
    public event Action<string>? Navigate;

    public AppHost(Window main)
    {
        Log.Verbose = DiagnosticLog.Active();
        _main = main;
        _ui = main.Dispatcher;
        _mybk = new MybkRunner(_ui, () => new WindowInteropHelper(_main).Handle);
        Hub = new SourceHub([new LmsSource(), new MybkSource(() => _mybk)]);
        Router = new ApiRouter(this);
        State = new AppState(Hub);
        _login = new LoginService(main, (stage, msg) => _ui.InvokeAsync(() => OnLogin(stage, msg)));

        _tray = new Tray(Environment.ProcessPath ?? "");
        _tray.Open += ShowMain;
        _tray.Sync += () => SyncAll();
        _tray.Exit += () => ((MainWindow)_main).Exit();
        _tray.Navigate += page => Navigate?.Invoke(page);
        _notifier = new DeadlineNotifier((t, b, p) => _ui.InvokeAsync(() => _tray.Show(t, b, p)));
        _notifyTimer.Tick += (_, _) => _notifier.Check();

        // Update.exe đã chạy và đang đợi app thoát: báo tiếng Việt rồi thoát êm (qua Dispose: dọn icon khay, dừng đồng bộ, lưu cửa sổ).
        Updates.Restarting += version => _ui.InvokeAsync(() => ExitForUpdateAsync(version));
        Hub.Changed += (name, stage) => _ui.InvokeAsync(() =>
        {
            // Lượt không có gì mới, hay vừa bắt đầu khi đã có dữ liệu: chỉ thanh trạng thái và InfoBar, không đọc lại, không vẽ lại trang.
            // Chưa có dữ liệu thì bắt đầu đồng bộ phải vẽ lại trang để bảng trống nói "đang tải lần đầu".
            if (stage == "done" && Hub.Quiet(name)) State.RefreshStatusOnly();
            else if (stage is "done" or "data") State.Reload();
            else if (stage == "progress") State.RefreshProgress();   // chỉ cập nhật thanh trạng thái, không vẽ lại các trang
            else if (stage == "start" && State.SyncedAt(name) is not null) State.RefreshStatusOnly();
            else State.RefreshStatus();
            if (name == "mybk" && stage == "done")
            {
                _mybk.Release();
                // Sync MyBK vừa đi qua SSO: biết chắc phiên còn, khỏi chạy keep-alive ngay sau đó. Hết phiên thì thôi giữ phiên.
                if (!Hub.Failed("mybk")) { _ssoAliveAt = DateTime.UtcNow; _ssoExpired = false; }
                else if (Hub.ErrorKind("mybk") == Data.SyncErrorKind.SessionExpired) _ssoExpired = true;
            }
            if (name == "lms" && stage == "done") _notifier.Check();
        });
        // Tiết kiệm pin: scheduler giãn chu kỳ gấp đôi (cùng cách đọc trạng thái pin với UpdateService).
        Hub.BatterySaver = () => Power.BatterySaverOn;
    }

    public void Start()
    {
        State.Reload();
        Hub.StartScheduler();
        _notifier.Check();
        _notifyTimer.Start();
        StartUpdates();
        StartKeepAlive();
        StartLibrary();
        // Có mạng lại hay máy vừa thức dậy: chạy ngay một lượt scheduler (nguồn nào tới hạn thì sync), không đợi tới tick kế.
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private void OnNetworkChanged(object? sender, System.Net.NetworkInformation.NetworkAvailabilityEventArgs e)
    {
        if (!e.IsAvailable) return;
        Log.Debug("Có mạng lại, kiểm tra nguồn tới hạn");
        Hub.RunDueSoon(TimeSpan.FromSeconds(15));
        CheckUpdatesSoon(TimeSpan.FromSeconds(20));
    }

    private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode != Microsoft.Win32.PowerModes.Resume) return;
        Log.Debug("Máy thức dậy, kiểm tra nguồn tới hạn");
        // Wi-Fi thường mất vài chục giây mới nối lại sau khi thức dậy; chưa có mạng thì RunDue tự bỏ lượt, NetworkChange gọi lại sau.
        Hub.RunDueSoon(TimeSpan.FromSeconds(30));
        CheckUpdatesSoon(TimeSpan.FromSeconds(45));
    }

    private DateTime _ssoAliveAt = DateTime.MinValue;   // lần cuối biết chắc phiên SSO còn (đăng nhập, giữ phiên)
    private bool _ssoExpired;                           // đã thấy phiên hết: thôi giữ phiên cho tới khi đăng nhập lại

    /// <summary>
    /// Giữ phiên SSO khi app đang mở (kể cả ẩn ở khay): SSO của trường tự hủy phiên sau vài giờ không dùng dù cookie còn hạn,
    /// nên cứ sso.keepAliveMinutes phút (mặc định 60) đi qua cổng SSO một lần bằng WebView ẩn: 1 lượt GET chỉ đọc.
    /// 0 = tắt (Cài đặt). Kiểm tra mỗi 5 phút để sau khi máy thức dậy thì làm ngay. Phiên đã hết thì đồng bộ MyBK một lần
    /// để app báo "cần đăng nhập lại" thay vì vẫn ghi là đã đăng nhập.
    /// </summary>
    private void StartKeepAlive()
    {
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (await timer.WaitForNextTickAsync())
            {
                try { await KeepAliveTickAsync(); }
                catch (Exception e) when (e is not OutOfMemoryException) { Log.Warn($"Giữ phiên SSO: {e.Message}"); }
            }
        });
    }

    private async Task KeepAliveTickAsync()
    {
        var minutes = Config.Int("sso.keepAliveMinutes", 60);
        if (minutes <= 0 || _ssoExpired || !MybkSource.SignedIn) return;
        if (DateTime.UtcNow - _ssoAliveAt < TimeSpan.FromMinutes(minutes)) return;
        // Tiết kiệm pin: bỏ giữ phiên (mỗi lần là một WebView ẩn chạy renderer). Phiên có hết thì lần sync sau báo đăng nhập lại.
        if (Power.BatterySaverOn) return;
        if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return;
        var since = _ssoAliveAt == DateTime.MinValue ? "chưa rõ" : $"{(DateTime.UtcNow - _ssoAliveAt).TotalMinutes:0} phút";
        var alive = await _mybk.KeepAliveAsync(CancellationToken.None);
        _mybk.Release();
        switch (alive)
        {
            case true:
                _ssoAliveAt = DateTime.UtcNow;
                Log.Debug($"Giữ phiên SSO: còn phiên (lần trước {since})");
                break;
            case false:
                _ssoExpired = true;
                // Ghi cả mức Info: biết phiên SSO thật sự sống bao lâu (cookie còn mà server đã hủy).
                Log.Info($"Phiên SSO đã hết hạn trên server (lần cuối còn phiên: {since} trước)");
                Hub.Start("mybk", force: true);   // MyBK báo "phiên hết hạn" → thanh báo mời đăng nhập lại
                break;
        }
    }

    /// <summary>
    /// Kiểm tra bản mới theo chế độ người dùng chọn: 60 giây sau khi mở rồi mỗi giờ xem đã tới hạn chưa (UpdatePolicy).
    /// Chế độ tự động thì tải ngay (trừ khi Tiết kiệm pin) và cài khi app thoát. "--update-now" chỉ dành cho bản test cập nhật.
    /// </summary>
    private void StartUpdates()
    {
        UpdateService.NoteVersion();
        if (!UpdateService.Supported) return;
        if (Environment.GetCommandLineArgs().Contains("--update-now") && UpdateService.IsTestInstall)
        {
            _ = Task.Run(async () =>
            {
                if (await Updates.CheckAsync(manual: true, default) is null) { Log.Info("update-now: không có bản mới"); return; }
                if (await Updates.DownloadAsync(userAsked: true, default)) Updates.ApplyAndRestart();
            });
            return;
        }
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(60));
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
            do await UpdateTickAsync();
            while (await timer.WaitForNextTickAsync());
        });
    }

    /// <summary>Một lượt kiểm tra cập nhật: tới hạn (app.update.checkHours) thì hỏi nguồn; chế độ tự động thì tải sẵn.</summary>
    private async Task UpdateTickAsync()
    {
        try
        {
            if (!Updates.Due()) return;
            var offer = await Updates.CheckAsync(manual: false, default);
            // Chế độ tự động: tải sẵn, cài lúc app thoát hẳn (Dispose, ApplyOnExit) hoặc lúc mở app lần sau (Program, ApplyOnStartup).
            if (offer is not null && UpdateService.Mode == UpdateMode.Auto && UpdateService.CanSelfUpdate)
                await Updates.DownloadAsync(userAsked: false, default);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Warn($"Vòng kiểm tra cập nhật: {e.Message}"); }
    }

    /// <summary>
    /// Sau khi Update.exe (silent) đã chạy: câu "Đang cài bản x.y.z, app sẽ tự mở lại sau vài giây" ở thanh trạng thái (MainWindow nghe
    /// Restarting), thêm thông báo khay nếu cửa sổ đang ẩn; đợi một chút cho người dùng đọc rồi thoát hẳn. Update.exe chỉ đợi app thoát
    /// 60 giây, nên nếu thoát êm bị kẹt thì 30 giây sau thoát cứng.
    /// </summary>
    private async Task ExitForUpdateAsync(string version)
    {
        if (!_main.IsVisible) _tray.Show(AppInfo.Name, L.F("update.installing", version), "");
        _ = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ =>
        {
            Log.Warn("Thoát để cài bản mới quá 30 giây, thoát cứng");
            Environment.Exit(0);
        }, TaskScheduler.Default);
        await Task.Delay(UpdateService.ExitNoticeDelay);
        // Cửa sổ khác (cửa sổ cập nhật đang mở dạng hộp thoại, cửa sổ trường...) đóng trước, rồi mới đóng cửa sổ chính.
        foreach (var w in Application.Current.Windows.OfType<Window>().Where(w => w != _main).ToList())
        {
            try { w.Close(); }
            catch (InvalidOperationException e) { Log.Warn($"Đóng cửa sổ trước khi cài bản mới: {e.Message}"); }   // cửa sổ đang tự đóng
        }
        ((MainWindow)_main).Exit();
    }

    private DateTimeOffset? _wakeUpdateCheck;
    private readonly object _wakeGate = new();

    /// <summary>
    /// Máy thức dậy hay có mạng lại: vòng mỗi giờ có thể đã lỡ (máy ngủ cả đêm), nên kiểm tra luôn nếu đã tới hạn. Tối đa mỗi giờ một lần
    /// (Wi-Fi chập chờn bắn sự kiện liên tục); chờ một chút cho mạng ổn định.
    /// </summary>
    private void CheckUpdatesSoon(TimeSpan delay)
    {
        if (!UpdateService.Supported) return;
        // NetworkChange bắn trên thread pool, PowerModeChanged trên thread của SystemEvents: khóa để hai sự kiện liền nhau chỉ chạy một lượt.
        lock (_wakeGate)
        {
            if (!UpdatePolicy.WakeCheckAllowed(_wakeUpdateCheck, DateTimeOffset.UtcNow)) return;
            _wakeUpdateCheck = DateTimeOffset.UtcNow;
        }
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            await UpdateTickAsync();
        });
    }

    /// <summary>
    /// Thư viện: đọc index (cache trước, tới hạn thì hỏi mạng) lúc mở app rồi mỗi giờ xem đã quá một ngày chưa.
    /// Tắt thì RefreshAsync không làm gì. Mở tab Thư viện cũng làm mới (SubjectsPage).
    /// </summary>
    private void StartLibrary()
    {
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
            do
            {
                try { await Library.RefreshAsync(SoHocTap.Library.LibraryRefresh.IfDue); }
                catch (Exception e) when (e is not OutOfMemoryException) { Log.Warn($"Thư viện: {e.Message}"); }
            }
            while (await timer.WaitForNextTickAsync());
        });
    }

    public void SyncAll(bool force = false)
    {
        Hub.Start("lms", force);
        Hub.Start("mybk", force);
    }

    private void OnLogin(string stage, string message)
    {
        LoginProgress?.Invoke(stage, message);
        Log.Debug($"Đăng nhập: {stage}");
        if (stage is "done" or "logout") MybkSource.SetSignedIn(stage == "done");
        if (stage == "done") { _ssoAliveAt = DateTime.UtcNow; _ssoExpired = false; }
        if (stage == "done") SyncAll();
        State.RefreshStatus();
    }

    private void ShowMain() => ((MainWindow)_main).ShowFromTray();

    /// <summary>Lần đầu bấm X (app thu xuống tray) thì báo là app vẫn đang chạy.</summary>
    public void TrayHint() => _tray.Show(AppInfo.Name, L.T("tray.stillRunning"), "");

    // ------------------------------------------------------------------ các lệnh của app

    // Lambda async trong InvokeAsync là async void với Dispatcher: lỗi không ai bắt sẽ rơi vào DispatcherUnhandledException,
    // nên bắt ngay ở đây, ghi log và báo stage "error" như các lỗi đăng nhập khác.
    public void Login() => _ui.InvokeAsync(async () =>
    {
        try { await _login.StartAsync(); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Error("Đăng nhập HCMUT", e);
            OnLogin("error", e.Message);
        }
    });

    /// <summary>Logout HCMUT khỏi app: xóa token LMS và toàn bộ cookie của profile WebView2 (session SSO, MyBK).</summary>
    public void Logout() => _ui.InvokeAsync(async () =>
    {
        try
        {
            LmsClient.Logout();
            var env = await WebHost.EnvironmentAsync();
            var c = await env.CreateCoreWebView2ControllerAsync(new WindowInteropHelper(_main).Handle);
            c.CoreWebView2.CookieManager.DeleteAllCookies();
            c.Close();
        }
        // Xóa cookie lỗi (WebView2 runtime hỏng…) thì vẫn ghi là đã đăng xuất: token LMS đã xóa, MyBK không chạy nữa.
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Error("Đăng xuất: không xóa được cookie WebView2", e); }
        OnLogin("logout", "");
    });

    public bool OpenWeb(string url, string title) => OpenWeb(url, title, null);

    public bool OpenWeb(string url, string title, Action? onSignedIn)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) return false;
        url = WebHost.Secure(url);
        if (!WebHost.IsSchoolHost(url))
        {
            Links.Open(url);
            return true;
        }
        _ui.InvokeAsync(() => new SchoolWindow(url, title, onSignedIn).Show());
        return true;
    }

    public bool SyncLms() => Hub.Start("lms");

    /// <summary>Mở LMS/MyBK trong cửa sổ của app. Nếu người dùng đăng nhập lại trong cửa sổ đó thì đồng bộ lại nguồn này ngay
    /// (đề xuất #6: đồng bộ lỗi vì hết phiên thì mở MyBK để đăng nhập).</summary>
    public bool OpenSource(string name) => name switch
    {
        "lms" => OpenWeb(Config.Str("sources.lms.site"), "BK-LMS", () => Hub.Start("lms", force: true)),
        "mybk" => OpenWeb(Config.Str("sources.mybk.home"), "MyBK", () => Hub.Start("mybk", force: true)),
        _ => false,
    };

    public void Dispose()
    {
        // SystemEvents giữ tham chiếu tới handler (static event): gỡ ra để không gọi vào AppHost đã dispose.
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        Updates.ApplyOnExit();
        _notifyTimer.Stop();
        _tray.Dispose();
        Hub.Dispose();
        _mybk.Dispose();
        Library.Dispose();
    }
}
