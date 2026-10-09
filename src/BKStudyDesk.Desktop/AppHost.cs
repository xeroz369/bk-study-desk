using Avalonia.Threading;
using BKStudyDesk.Desktop.Web;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Sources;
using SoHocTap.Sources.Lms;
using SoHocTap.Sources.Mybk;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop;

/// <summary>
/// Phần chạy nền của app đa nền tảng, sống cùng cửa sổ chính: nguồn dữ liệu (SourceHub), trạng thái (AppState), lịch đồng bộ,
/// MyBK chạy ẩn và giữ phiên SSO.
/// Không có giao diện; trang nào cũng đọc State, nghe State.Changed (dữ liệu đổi) và State.StatusChanged (chỉ trạng thái đồng bộ đổi).
/// </summary>
internal sealed class AppHost : IDisposable, SoHocTap.Api.IShellActions
{
    private readonly MybkBrowser _mybk = new();
    private DateTime _ssoAliveAt = DateTime.MinValue;   // lần cuối biết chắc phiên SSO còn (đăng nhập, giữ phiên, đồng bộ MyBK)
    private bool _ssoExpired;                           // đã thấy phiên hết: thôi giữ phiên cho tới khi đăng nhập lại

    public SoHocTap.Library.LibraryService Library { get; } = new();

    /// <summary>Cập nhật (Velopack, cùng UpdateService của 1.x).</summary>
    public SoHocTap.Updates.UpdateService Updates { get; } = new();

    /// <summary>Có bản mới ở chế độ "báo khi có bản mới": cửa sổ chính báo người dùng (số phiên bản).</summary>
    public event Action<string>? UpdateOffered;
    private readonly CancellationTokenSource _stop = new();   // dừng các vòng nền (giữ phiên, thư viện, cập nhật) khi đóng app

    /// <summary>API của 1.x gọi thẳng (không qua HTTP): trang Luyện tập native đọc ghi kết quả, gói, quiz LMS qua đây.</summary>
    public SoHocTap.Api.ApiRouter Router => _router ??= new SoHocTap.Api.ApiRouter(this);
    private SoHocTap.Api.ApiRouter? _router;

    /// <summary>Khung Luyện tập nhờ mở web: mở bằng trình duyệt mặc định (nơi người dùng đã đăng nhập trang trường).</summary>
    public bool OpenWeb(string url, string title)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) return false;
        OpenUrl?.Invoke(u);
        return true;
    }

    /// <summary>Khung Luyện tập nhờ đồng bộ LMS (lưu kết quả quiz vừa làm).</summary>
    public bool SyncLms() => Hub.Start(SourceIds.Lms, force: true);

    /// <summary>Cửa sổ chính mở link bằng Launcher (AppHost không có giao diện).</summary>
    public event Action<Uri>? OpenUrl;

    public SourceHub Hub { get; }
    public AppState State { get; }

    public AppHost()
    {
        Log.Verbose = DiagnosticLog.Active();   // bật "Ghi log chẩn đoán" trong Cài đặt thì ghi cả dòng Debug vào app.log
        Hub = new SourceHub([new LmsSource(), new MybkSource(() => _mybk)]);
        State = new AppState(Hub);
        Hub.Changed += (name, stage) => Dispatcher.UIThread.Post(() => OnSourceChanged(name, stage));
    }

    public void Start()
    {
        State.Reload();
        Hub.StartScheduler();
        StartKeepAlive();
        StartLibrary();
        StartUpdates();
        // Có mạng lại: chạy ngay một lượt nguồn tới hạn, không đợi tới tick kế (API .NET, có trên cả ba hệ điều hành).
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
    }

    /// <summary>Cùng quy tắc với bản 1.x (Shell/AppHost): lượt không có gì mới thì chỉ báo trạng thái, không đọc lại, không vẽ lại trang.</summary>
    private DeadlineNotifier? _notifier;
    private Avalonia.Threading.DispatcherTimer? _notifyTimer;

    /// <summary>
    /// Bật nhắc hạn (cùng DeadlineNotifier của 1.x: chọn mốc, chống trùng qua data/notified.json): xem mỗi 5 phút và sau mỗi lượt
    /// đồng bộ LMS. <paramref name="show"/> hiện lời nhắc (cửa sổ chính chọn cách hiện theo hệ điều hành).
    /// </summary>
    public void StartReminders(Action<string, string, string> show)
    {
        if (_notifier is not null) return;   // đã bật (mở ở khay rồi mới hiện cửa sổ)
        _notifier = new DeadlineNotifier(show);
        _notifyTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _notifyTimer.Tick += (_, _) => _notifier.Check();
        _notifyTimer.Start();
        _notifier.Check();
    }

    private void OnSourceChanged(string name, string stage)
    {
        if (stage == "done" && Hub.Quiet(name)) State.RefreshStatusOnly();
        else if (stage is "done" or "data") State.Reload();
        else if (stage == "progress") State.RefreshProgress();
        else if (stage == "start" && State.SyncedAt(name) is not null) State.RefreshStatusOnly();
        else State.RefreshStatus();
        if (name == SourceIds.Lms && stage == "done") _notifier?.Check();
        // Token LMS hết hạn: phiên SSO còn thì trường tự cấp vé mới, lấy lại token ngầm (một lần), không bắt người dùng đăng nhập.
        if (name == SourceIds.Lms && stage == "done" && Hub.ErrorKind(SourceIds.Lms) == SoHocTap.Data.SyncErrorKind.SessionExpired && !_lmsRefreshTried)
        {
            _lmsRefreshTried = true;
            Service.Refresh();
        }
        if (name == SourceIds.Mybk && stage == "done")
        {
            _mybk.Release();
            // Lượt MyBK vừa đi qua SSO: biết chắc phiên còn, khỏi giữ phiên ngay sau đó. Hết phiên thì thôi giữ phiên.
            if (!Hub.Failed(SourceIds.Mybk)) { _ssoAliveAt = DateTime.UtcNow; _ssoExpired = false; }
            else if (Hub.ErrorKind(SourceIds.Mybk) == SoHocTap.Data.SyncErrorKind.SessionExpired) _ssoExpired = true;
        }
    }

    /// <summary>Các bước của lượt đăng nhập (check, password, mybk, lms, done, cancel, error, logout) cho giao diện.</summary>
    public event Action<string, string>? LoginProgress;

    private Action<Views.LoginView?>? _present;   // chỗ hiện trang đăng nhập (cửa sổ chính đưa vào lần bấm Đăng nhập đầu tiên)
    private LoginService Service => _login ??= new LoginService(v => _present?.Invoke(v), OnLogin);
    private LoginService? _login;
    private bool _lmsRefreshTried;   // token LMS hết hạn: đã thử làm mới ngầm một lần, chờ đăng nhập xong mới thử lại

    /// <summary>Đăng nhập HCMUT: thử ngầm trước, cần mật khẩu thì đưa trang đăng nhập cho <paramref name="present"/> (null: gỡ).</summary>
    public void Login(Action<Views.LoginView?> present)
    {
        _present = present;
        Service.Start();
    }

    /// <summary>Đăng xuất HCMUT khỏi app: xóa token LMS và mọi cookie của trình duyệt nhúng (phiên SSO, MyBK).</summary>
    public async Task LogoutAsync()
    {
        LmsClient.Logout();
        var hidden = new HiddenWeb();
        try
        {
            var page = hidden.Open(allow: null);
            if (page.View.TryGetCookieManager() is { } m)
                foreach (var c in await m.GetCookiesAsync()) m.DeleteCookie(c);
        }
        // Xóa cookie lỗi thì vẫn ghi là đã đăng xuất: token LMS đã xóa, MyBK không chạy nữa.
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Error("Đăng xuất: không xóa được cookie", e); }
        finally { hidden.Close(); }
        SsoSession.Forget();
        Platform.Credentials.Forget();   // đăng xuất thì không giữ tài khoản cho tự đăng nhập
        OnLogin("logout", "");
    }

    private void OnLogin(string stage, string message)
    {
        Log.Info($"Đăng nhập: {stage}");   // chỉ tên bước, để biết lần đăng nhập dừng ở đâu
        if (stage is "done" or "refresh" or "logout") MybkSource.SetSignedIn(stage != "logout");
        if (stage is "done" or "refresh")   // refresh: lượt làm mới ngầm (LoginService.Refresh) xong, như đăng nhập xong
        {
            _lmsRefreshTried = false;
            _ssoAliveAt = DateTime.UtcNow;
            _ssoExpired = false;
            Hub.Start(SourceIds.Lms, force: true);
            Hub.Start(SourceIds.Mybk, force: true);
        }
        State.RefreshStatus();
        LoginProgress?.Invoke(stage, message);
    }

    /// <summary>Giữ phiên SSO như bản 1.x: mỗi 5 phút xem đã quá sso.keepAliveMinutes kể từ lần cuối biết còn phiên chưa.</summary>
    private void StartKeepAlive()
    {
        var ct = _stop.Token;
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try { await KeepAliveTickAsync(ct); }
                catch (Exception e) when (e is not (OutOfMemoryException or OperationCanceledException)) { Log.Warn($"Giữ phiên SSO: {e.Message}"); }
            }
        }, ct);
    }

    private async Task KeepAliveTickAsync(CancellationToken ct)
    {
        var minutes = Settings.Sso.KeepAliveMinutes;
        if (minutes <= 0 || _ssoExpired || !MybkSource.SignedIn) return;
        if (DateTime.UtcNow - _ssoAliveAt < TimeSpan.FromMinutes(minutes)) return;
        if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return;
        var alive = await _mybk.KeepAliveAsync(ct);
        _mybk.Release();
        if (alive == true) _ssoAliveAt = DateTime.UtcNow;
        else if (alive == false)
        {
            _ssoExpired = true;
            SsoSession.Expired();
            Hub.Start(SourceIds.Mybk, force: true);   // MyBK báo "phiên hết hạn", app mời đăng nhập lại
        }
    }

    /// <summary>Thư viện: đọc index (cache trước, tới hạn thì hỏi mạng) lúc mở app rồi mỗi giờ xem đã quá một ngày chưa (như 1.x).</summary>
    private void StartLibrary()
    {
        var ct = _stop.Token;
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
            do
            {
                try { await Library.RefreshAsync(SoHocTap.Library.LibraryRefresh.IfDue, ct); }
                catch (Exception e) when (e is not (OutOfMemoryException or OperationCanceledException)) { Log.Warn($"Thư viện: {e.Message}"); }
            }
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
        }, ct);
    }

    /// <summary>Như 1.x: ghi phiên bản đang chạy, 60 giây sau khi mở rồi mỗi giờ xem đã tới hạn kiểm tra chưa (app.update.checkHours).</summary>
    private void StartUpdates()
    {
        SoHocTap.Updates.UpdateService.NoteVersion();
        if (!SoHocTap.Updates.UpdateService.Supported) return;
        var ct = _stop.Token;
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(60), ct);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
            do await UpdateTickAsync();
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
        }, ct);
    }

    /// <summary>Một lượt: tới hạn thì hỏi nguồn; chế độ tự động thì tải sẵn (cài khi thoát hay lần mở sau), chế độ báo thì báo.</summary>
    private async Task UpdateTickAsync()
    {
        try
        {
            if (!Updates.Due()) return;
            if (await Updates.CheckAsync(manual: false, default) is not { } offer) return;
            if (SoHocTap.Updates.UpdateService.Mode == UpdateMode.Auto && SoHocTap.Updates.UpdateService.CanSelfUpdate)
                await Updates.DownloadAsync(userAsked: false, default);
            else Dispatcher.UIThread.Post(() => UpdateOffered?.Invoke(offer.Version));
        }
        catch (Exception e) when (e is not (OutOfMemoryException or OperationCanceledException)) { Log.Warn($"Vòng kiểm tra cập nhật: {e.Message}"); }
    }

    private void OnNetworkChanged(object? sender, System.Net.NetworkInformation.NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable) Hub.RunDueSoon(TimeSpan.FromSeconds(15));
    }

    public void Dispose()
    {
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        _stop.Cancel();
        _notifyTimer?.Stop();
        Hub.Dispose();
        Library.Dispose();
        Updates.ApplyOnExit();   // đã tải bản mới (chế độ tự động): cài khi app thoát
    }
}
