using Avalonia.Threading;
using BKStudyDesk.Desktop.Web;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Sources;
using SoHocTap.Sources.Lms;
using SoHocTap.Sources.Mybk;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop;

internal sealed class AppHost : IDisposable, SoHocTap.Api.IShellActions
{
    private readonly MybkBrowser _mybk = new();

    public SoHocTap.Library.LibraryService Library { get; } = new();

    public SoHocTap.Updates.UpdateService Updates { get; } = new();

    public event Action<string>? UpdateOffered;
    private readonly CancellationTokenSource _stop = new();

    public SoHocTap.Api.ApiRouter Router => _router ??= new SoHocTap.Api.ApiRouter(this);
    private SoHocTap.Api.ApiRouter? _router;

    public bool OpenWeb(string url, string title)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) return false;
        OpenUrl?.Invoke(u);
        return true;
    }

    public bool SyncLms() => Hub.Start(SourceIds.Lms, force: true);

    public event Action<Uri>? OpenUrl;

    public SourceHub Hub { get; }
    public AppState State { get; }

    public AppHost()
    {
        Log.Verbose = DiagnosticLog.Active();
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
        _ = Task.Run(() => System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged);
        OfflineMode.Changed += OnOfflineChanged;
        StartActivityWatch();
    }

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

    private void StartActivityWatch()
    {
        UserActivity.IdleLimit = TimeSpan.FromMinutes(Math.Max(1, Config.Int("app.idleMinutes", 10)));
        if (OperatingSystem.IsWindows()) UserActivity.SystemIdle = Platform.IdleTime.Read;
        UserActivity.Changed += OnActivityChanged;
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
        State.RefreshStatusOnly();
    }

    private void OnOfflineChanged(bool on)
    {
        if (on) _mybk.Release();
        else Hub.RunDueSoon(TimeSpan.FromSeconds(3));
        Dispatcher.UIThread.Post(State.RefreshStatus);
    }

    private DeadlineNotifier? _notifier;
    private MessageNotifier? _messages;
    private Avalonia.Threading.DispatcherTimer? _messagesTimer;

    public Task<bool> SyncMessagesNowAsync() => SyncMessagesAsync(manual: true);

    private async Task<bool> SyncMessagesAsync(bool manual)
    {
        if (!manual && (OfflineMode.On || !UserActivity.Active)) return false;
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
    private bool _freshWarned;

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
                else LmsSource.CancelLight();
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
        _show?.Invoke(L.T("fresh.allSignedOutTitle"), L.T("fresh.allSignedOutBody"), "");
        State.RefreshStatus();
    }

    public event Action? MessagesChanged;
    private Avalonia.Threading.DispatcherTimer? _notifyTimer;

    public void StartReminders(Action<string, string, string> show)
    {
        if (_notifier is not null) return;
        _notifier = new DeadlineNotifier(show);
        _messages = new MessageNotifier(show);
        _messagesTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(Math.Max(10, Config.Int("sources.lms.messagesMinutes", 30))) };
        _messagesTimer.Tick += async (_, _) => await SyncMessagesAsync(manual: false);
        _messagesTimer.Start();
        if (!File.Exists(SoHocTap.Data.LmsMessagesStore.FilePath))
            Avalonia.Threading.DispatcherTimer.RunOnce(() => _ = SyncMessagesAsync(manual: false), TimeSpan.FromSeconds(15));
        _show = show;
        _freshTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _freshTimer.Tick += (_, _) => KeepFreshTick();
        _freshTimer.Start();
        Avalonia.Threading.DispatcherTimer.RunOnce(KeepFreshTick, TimeSpan.FromMinutes(2));
        _notifyTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _notifyTimer.Tick += (_, _) => _notifier.Check();
        _notifyTimer.Start();
        _notifier.Check();
    }

    public long? RemindersMutedUntil => DeadlineNotifier.MutedUntil();

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
        if (name == SourceIds.Lms && stage == "done" && Hub.ErrorKind(SourceIds.Lms) == SoHocTap.Data.SyncErrorKind.SessionExpired && !_lmsRefreshTried)
            RefreshLmsSignIn();
        if (name == SourceIds.Mybk && stage == "done")
        {
            if (MybkSource.NeedsRun() && Hub.Start(SourceIds.Mybk, force: true, soon: true)) return;
            _mybk.Release();
        }
    }

    public event Action<string, string>? LoginProgress;

    private Action<Views.LoginView?>? _present;
    private LoginService Service => _login ??= new LoginService(v => _present?.Invoke(v), OnLogin);
    private LoginService? _login;
    private bool _lmsRefreshTried, _lmsRefreshWaiting;

    private void RefreshLmsSignIn()
    {
        if (!UserActivity.Active) { _lmsRefreshWaiting = true; Log.Info("Token LMS hết hạn: chờ người dùng quay lại mới đăng nhập lại"); return; }
        _lmsRefreshWaiting = false;
        _lmsRefreshTried = true;
        Service.Refresh();
    }

    public void Login(Action<Views.LoginView?> present)
    {
        _present = present;
        Service.Start();
    }

    public async Task LogoutAsync()
    {
        LmsClient.Logout();
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
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Error("Đăng xuất: không xóa được cookie", e); }
        finally { hidden.Close(); }
        SsoSession.Forget();
        Platform.Credentials.Forget();
        OnLogin("logout", "");
    }

    private void OnLogin(string stage, string message)
    {
        Log.Info($"Đăng nhập: {stage}");
        if (stage is "done" or "refresh" or "logout") MybkSource.SetSignedIn(stage != "logout");
        if (stage is "done" or "refresh")
        {
            _lmsRefreshTried = false;
            SsoGate.SignedIn();
            _freshWarned = false;
            Hub.ForgetSessionExpired();
            Hub.Start(SourceIds.Lms, force: true);
            Hub.Start(SourceIds.Mybk, force: true);
        }
        State.RefreshStatus();
        LoginProgress?.Invoke(stage, message);
    }

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
        Updates.ApplyOnExit();
    }
}
