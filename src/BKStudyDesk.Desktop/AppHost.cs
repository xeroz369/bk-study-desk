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
    // Sessions (SSO, MyBK, LMS): never guess expiry and never ping SSO on a timer. Use the current session until the server
    // itself rejects it, then go through SSO once. Every SSO pass counts as a MyBK sign-in (hourly keep-alive pushed it to 85/month).
    private readonly MybkBrowser _mybk = new();

    public SoHocTap.Library.LibraryService Library { get; } = new();

    /// <summary>Cập nhật (Velopack, cùng UpdateService của 1.x).</summary>
    public SoHocTap.Updates.UpdateService Updates { get; } = new();

    /// <summary>Có bản mới ở chế độ "báo khi có bản mới": cửa sổ chính báo người dùng (số phiên bản).</summary>
    public event Action<string>? UpdateOffered;
    private readonly CancellationTokenSource _stop = new();   // dừng các vòng nền (thư viện, cập nhật) khi đóng app

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
        StartLibrary();
        StartUpdates();
        // Có mạng lại: chạy ngay một lượt nguồn tới hạn, không đợi tới tick kế (API .NET, có trên cả ba hệ điều hành).
        // Đăng ký ở luồng nền: lần đầu .NET khởi tạo theo dõi mạng mất 50 đến 100 ms, không để chậm lúc mở cửa sổ (đo 01/11/2026).
        _ = Task.Run(() => System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged);
        OfflineMode.Changed += OnOfflineChanged;
        StartActivityWatch();
    }

    /// <summary>
    /// A subject was opened (Subjects page): check its LMS courses for updates, one at a time, each at most every 20 minutes
    /// (LmsSource.CheckCourseOnOpenAsync). Not while Offline or away. A changed course reloads the pages.
    /// </summary>
    public void CheckCoursesOnOpen(IReadOnlyList<long> ids)
    {
        if (OfflineMode.On || !UserActivity.Active || !LmsClient.HasToken) return;
        _ = Task.Run(async () =>
        {
            var changed = false;
            foreach (var id in ids)
                try { changed |= await LmsSource.CheckCourseOnOpenAsync(id, _stop.Token); }
                catch (OperationCanceledException) { return; }
            if (changed) Dispatcher.UIThread.Post(State.Reload);
        });
    }

    private Avalonia.Threading.DispatcherTimer? _activityTimer;

    /// <summary>
    /// Background work runs only while someone is at the computer (UserActivity, docs/plans/2026-11-06-on-demand-sync.md).
    /// Windows reads system-wide idle time; other systems use input and focus in the app window (MainWindow feeds them).
    /// Checked every 15 seconds: going away closes the hidden MyBK browser, coming back runs one due check a few seconds later.
    /// </summary>
    private void StartActivityWatch()
    {
        UserActivity.IdleLimit = TimeSpan.FromMinutes(Math.Max(1, Config.Int("app.idleMinutes", 10)));
        if (OperatingSystem.IsWindows()) UserActivity.SystemIdle = Platform.IdleTime.Read;
        UserActivity.Changed += OnActivityChanged;
        // A failed automatic fill stops all automatic sign-ins; the sign-in bar (InfoBar, session expired) asks the user.
        SsoGate.AutoFillFailed += () => Log.Warn("Tự đăng nhập không được: dừng tự đăng nhập, chờ người dùng đăng nhập trong app");
        _activityTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _activityTimer.Tick += (_, _) => UserActivity.Poll();
        _activityTimer.Start();
    }

    private void OnActivityChanged(bool active)
    {
        Log.Info(active ? "Người dùng quay lại: chạy lại việc nền" : "Người dùng vắng: dừng việc nền");
        if (active && _lmsRefreshWaiting && Hub.ErrorKind(SourceIds.Lms) == SoHocTap.Data.SyncErrorKind.SessionExpired) RefreshLmsSignIn();
        if (active) Hub.RunDueSoon(TimeSpan.FromSeconds(3));
        else _mybk.Release();
        State.RefreshStatusOnly();   // "next sync" times depend on activity
    }

    /// <summary>
    /// Bật Offline: đóng trình duyệt ẩn của MyBK ngay (không giữ kết nối nào tới trường). Tắt: chạy lại một lượt nguồn tới hạn sau
    /// ít giây; giữ phiên SSO và tin nhắn quay lại theo lịch.
    /// </summary>
    private void OnOfflineChanged(bool on)
    {
        if (on) _mybk.Release();
        else Hub.RunDueSoon(TimeSpan.FromSeconds(3));
        Dispatcher.UIThread.Post(State.RefreshStatus);
    }

    /// <summary>Cùng quy tắc với bản 1.x (Shell/AppHost): lượt không có gì mới thì chỉ báo trạng thái, không đọc lại, không vẽ lại trang.</summary>
    private DeadlineNotifier? _notifier;
    private MessageNotifier? _messages;
    private Avalonia.Threading.DispatcherTimer? _messagesTimer;

    /// <summary>Đọc nhanh tin nhắn riêng LMS (lượt 30 phút, lần đầu chưa có tin, nút Tải tin nhắn ngay ở Hộp thư). true nếu đã ghi file.</summary>
    public Task<bool> SyncMessagesNowAsync() => SyncMessagesAsync(manual: true);

    /// <summary>Lượt tự chạy (manual false) thì bỏ khi đang Offline; nút bấm tay thì vẫn đọc.</summary>
    private async Task<bool> SyncMessagesAsync(bool manual)
    {
        if (!manual && (OfflineMode.On || !UserActivity.Active)) return false;   // timed poll: not while Offline or away
        if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return false;
        if (!await Task.Run(() => LmsSource.SyncMessagesAsync(CancellationToken.None))) return false;
        _messages?.Check();
        State.RefreshStatusOnly();
        MessagesChanged?.Invoke();
        return true;
    }

    private Action<string, string, string>? _show;
    private Avalonia.Threading.DispatcherTimer? _freshTimer;
    private readonly HashSet<string> _freshDue = [], _freshWaiting = [], _freshSignedOut = [];
    private bool _freshWarned;   // told the user once; cleared when they sign in

    /// <summary>
    /// Minimum freshness for reminders (KeepFresh): LMS deadlines and quizzes (a light run), and MyBK timetable and exams when MyBK
    /// is on, refreshed when older than app.minFreshDays days, whether or not the user is at the computer. Existing sessions only:
    /// automatic password fill stays tied to activity (SsoGate). Not while Offline (the user asked for no connections).
    /// </summary>
    private void KeepFreshTick()
    {
        if (OfflineMode.On || _freshWaiting.Count > 0) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var days = SoHocTap.Core.Settings.Sync.MinFreshDays;
        _freshDue.Clear();
        _freshSignedOut.Clear();
        var lms = SoHocTap.Data.LmsStore.Latest();
        if (lms.Count > 0 && KeepFresh.Due(KeepFresh.Latest(SoHocTap.Sources.Mybk.MybkNormalize.Long(lms["syncedAt"]), SoHocTap.Sources.Mybk.MybkNormalize.Long(lms["lightAt"])), now, days))
        {
            _freshDue.Add(SourceIds.Lms);
            if (!LmsClient.HasToken) _freshSignedOut.Add(SourceIds.Lms);
            else
            {
                LmsSource.LightNext();
                if (Hub.Start(SourceIds.Lms)) _freshWaiting.Add(SourceIds.Lms);
                else LmsSource.CancelLight();   // a sync is running (it refreshes deadlines too) or just ran
            }
        }
        var mybk = SoHocTap.Data.MybkStore.Latest();
        if (SoHocTap.Core.Settings.Sync.MybkEnabled && mybk.Count > 0
            && KeepFresh.Due(KeepFresh.Oldest(MybkSections.FetchedAt(mybk, MybkSections.Schedule), MybkSections.FetchedAt(mybk, MybkSections.Exams)), now, days))
        {
            _freshDue.Add(SourceIds.Mybk);
            if (!MybkSource.SignedIn) _freshSignedOut.Add(SourceIds.Mybk);
            else
            {
                MybkSource.Want([MybkSections.Schedule, MybkSections.Exams], force: false);
                if (Hub.Start(SourceIds.Mybk, force: true, soon: true)) _freshWaiting.Add(SourceIds.Mybk);
            }
        }
        if (_freshDue.Count > 0) Log.Info($"Cập nhật tối thiểu cho nhắc hạn: {string.Join(", ", _freshDue)}");
        if (_freshWaiting.Count == 0) WarnIfAllSignedOut();
    }

    /// <summary>A keep-fresh run ended: note a signed-out result, and once every due source has answered, tell the user if none worked.</summary>
    private void OnFreshDone(string name)
    {
        if (!_freshWaiting.Remove(name)) return;
        if (Hub.ErrorKind(name) == SoHocTap.Data.SyncErrorKind.SessionExpired) _freshSignedOut.Add(name);
        if (_freshWaiting.Count == 0) WarnIfAllSignedOut();
    }

    private void WarnIfAllSignedOut()
    {
        if (_freshWarned || !KeepFresh.AllSignedOut(_freshDue, _freshSignedOut)) return;
        _freshWarned = true;
        Log.Warn("Cập nhật tối thiểu: mọi phiên đăng nhập đều hết, nhắc hạn đang dùng dữ liệu cũ");
        _show?.Invoke(L.T("fresh.allSignedOutTitle"), L.T("fresh.allSignedOutBody"), "");   // status bar already says "cần đăng nhập"
        State.RefreshStatus();
    }

    /// <summary>Lượt đọc nhanh tin nhắn vừa ghi file mới: trang Hộp thư, thẻ ở Hôm nay đọc lại.</summary>
    public event Action? MessagesChanged;   // tin nhắn riêng LMS mới, xem sau mỗi lượt đồng bộ LMS
    private Avalonia.Threading.DispatcherTimer? _notifyTimer;

    /// <summary>
    /// Bật nhắc hạn (cùng DeadlineNotifier của 1.x: chọn mốc, chống trùng qua data/notified.json): xem mỗi 5 phút và sau mỗi lượt
    /// đồng bộ LMS. <paramref name="show"/> hiện lời nhắc (cửa sổ chính chọn cách hiện theo hệ điều hành).
    /// </summary>
    public void StartReminders(Action<string, string, string> show)
    {
        if (_notifier is not null) return;   // đã bật (mở ở khay rồi mới hiện cửa sổ)
        _notifier = new DeadlineNotifier(show);
        _messages = new MessageNotifier(show);
        // Tin nhắn riêng: đọc nhanh mỗi sources.lms.messagesMinutes phút (lượt LMS đầy đủ chỉ 3 giờ một lần), có tin mới thì báo ngay.
        _messagesTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(Math.Max(10, Config.Int("sources.lms.messagesMinutes", 30))) };
        _messagesTimer.Tick += async (_, _) => await SyncMessagesAsync(manual: false);
        _messagesTimer.Start();
        // Chưa có tin nhắn lưu trên máy (mới cập nhật lên bản có Hộp thư, mới đăng nhập): đọc ngay sau ít giây, không chờ lượt LMS 3 giờ
        // hay lượt nhanh 30 phút (31/10/2026: cập nhật xong mở Hộp thư thấy trống).
        if (!File.Exists(SoHocTap.Data.LmsMessagesStore.FilePath))
            Avalonia.Threading.DispatcherTimer.RunOnce(() => _ = SyncMessagesAsync(manual: false), TimeSpan.FromSeconds(15));
        _show = show;
        // Reminders read saved data: keep it at most app.minFreshDays old, even while the user is away (KeepFresh). First look after
        // 2 minutes, then every 30 minutes; it only sends requests when something is older than the limit.
        _freshTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _freshTimer.Tick += (_, _) => KeepFreshTick();
        _freshTimer.Start();
        Avalonia.Threading.DispatcherTimer.RunOnce(KeepFreshTick, TimeSpan.FromMinutes(2));
        _notifyTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _notifyTimer.Tick += (_, _) => _notifier.Check();
        _notifyTimer.Start();
        _notifier.Check();
    }

    /// <summary>Nhắc hạn đang tạm im tới lúc nào (giây Unix), null là đang bật.</summary>
    public long? RemindersMutedUntil => DeadlineNotifier.MutedUntil();

    /// <summary>Tạm im nhắc tới sáng mai (true) hay bật lại ngay (false); bật lại thì xem mốc ngay, không chờ 5 phút.</summary>
    public void MuteReminders(bool on)
    {
        DeadlineNotifier.Mute(on);
        if (!on) _notifier?.Check();
    }

    private void OnSourceChanged(string name, string stage)
    {
        if (stage == "done" && Hub.Quiet(name)) State.RefreshStatusOnly();
        else if (stage is "done" or "data") State.Reload();
        else if (stage == "progress") State.RefreshProgress();
        else if (stage == "start" && State.SyncedAt(name) is not null) State.RefreshStatusOnly();
        else State.RefreshStatus();
        if (name == SourceIds.Lms && stage == "done") { _notifier?.Check(); _messages?.Check(); }
        if (stage == "done") OnFreshDone(name);
        // Token LMS hết hạn: phiên SSO còn thì trường tự cấp vé mới, lấy lại token ngầm (một lần), không bắt người dùng đăng nhập.
        // Only while the user is at the computer: otherwise wait, OnActivityChanged retries when they are back.
        if (name == SourceIds.Lms && stage == "done" && Hub.ErrorKind(SourceIds.Lms) == SoHocTap.Data.SyncErrorKind.SessionExpired && !_lmsRefreshTried)
            RefreshLmsSignIn();
        if (name == SourceIds.Mybk && stage == "done")
        {
            // The user moved to another page or tab while MyBK was loading: load what that one needs, else close the hidden browser.
            if (MybkSource.NeedsRun() && Hub.Start(SourceIds.Mybk, force: true, soon: true)) return;
            _mybk.Release();
        }
    }

    /// <summary>Các bước của lượt đăng nhập (check, password, mybk, lms, done, cancel, error, logout) cho giao diện.</summary>
    public event Action<string, string>? LoginProgress;

    private Action<Views.LoginView?>? _present;   // chỗ hiện trang đăng nhập (cửa sổ chính đưa vào lần bấm Đăng nhập đầu tiên)
    private LoginService Service => _login ??= new LoginService(v => _present?.Invoke(v), OnLogin);
    private LoginService? _login;
    private bool _lmsRefreshTried, _lmsRefreshWaiting;

    private void RefreshLmsSignIn()
    {
        if (!UserActivity.Active) { _lmsRefreshWaiting = true; Log.Info("Token LMS hết hạn: chờ người dùng quay lại mới đăng nhập lại"); return; }
        _lmsRefreshWaiting = false;
        _lmsRefreshTried = true;
        Service.Refresh();
    }   // token LMS hết hạn: đã thử làm mới ngầm một lần, chờ đăng nhập xong mới thử lại

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
        // Tin nhắn riêng và nội dung trang là của tài khoản vừa đăng xuất: xóa luôn, người đăng nhập sau trên cùng máy không đọc được.
        foreach (var f in new[] { SoHocTap.Data.LmsMessagesStore.FilePath, SoHocTap.Data.LmsPagesStore.FilePath })
            try { if (File.Exists(f)) File.Delete(f); } catch (Exception e) { Log.Warn($"Xóa {Path.GetFileName(f)}: {e.Message}"); }
        try { MessageNotifier.Forget(); } catch (Exception e) { Log.Warn($"Quên mốc tin nhắn: {e.Message}"); }
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
            SsoGate.SignedIn();   // signed in again: one automatic fill is allowed for a later expiry
            _freshWarned = false;
            Hub.ForgetSessionExpired();   // clear the stale "session expired" status now, not after the sync below
            Hub.Start(SourceIds.Lms, force: true);
            Hub.Start(SourceIds.Mybk, force: true);
        }
        State.RefreshStatus();
        LoginProgress?.Invoke(stage, message);
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
                try { if (!OfflineMode.On && UserActivity.Active) await Library.RefreshAsync(SoHocTap.Library.LibraryRefresh.IfDue, ct); }
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
            if (OfflineMode.On || !UserActivity.Active || !Updates.Due()) return;
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
        OfflineMode.Changed -= OnOfflineChanged;
        UserActivity.Changed -= OnActivityChanged;
        _activityTimer?.Stop();
        _freshTimer?.Stop();
        _stop.Cancel();
        _notifyTimer?.Stop();
        _messagesTimer?.Stop();
        Hub.Dispose();
        Library.Dispose();
        Updates.ApplyOnExit();   // đã tải bản mới (chế độ tự động): cài khi app thoát
    }
}
