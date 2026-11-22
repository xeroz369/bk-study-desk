using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Ui;

namespace SoHocTap.Updates;

public sealed record UpdateOffer(string Version, long Bytes, string Notes, string Url, object? Native);

public sealed class UpdateService
{
    private static string StateFile => Paths.DataFile("update-state.json");
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public UpdateOffer? Offer { get; private set; }
    public bool Downloaded { get; private set; }
    public string? LastError { get; private set; }
    public event Action? Changed;
    public event Action<string>? Restarting;

    public DownloadState? Download { get; private set; }
    public bool Downloading => Download is { Verified: false };
    private CancellationTokenSource? _downloadCts;
    private bool _applyLaunched;

    public static readonly TimeSpan ExitNoticeDelay = TimeSpan.FromSeconds(2);

    public static string? StartupNotice { get; private set; }

    public static UpdateMode Mode => UpdatePolicy.ParseMode(Settings.Update.Mode);

    public static bool Supported =>
        Paths.Kind != InstallKind.Store;

    public static bool CanSelfUpdate => Supported && Paths.Kind == InstallKind.Installed;

    public static bool IsTestInstall =>
        CanSelfUpdate && Velopack.Locators.VelopackLocator.Current?.AppId == "BKStudyDeskTest";

    public static void SetMode(UpdateMode m) => Config.Set("app.update.mode", m.ToString().ToLowerInvariant());

    public DateTimeOffset? LastCheck
    {
        get
        {
            try { return JsonStore.ReadObject(StateFile)["lastCheck"] is JsonValue v && v.TryGetValue<long>(out var t) ? DateTimeOffset.FromUnixTimeSeconds(t) : null; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { return null; }
        }
    }

    public bool Due() => Supported && UpdatePolicy.ShouldCheck(Mode, Paths.Kind, DateTimeOffset.UtcNow, LastCheck,
        Settings.Update.CheckHours);

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static void EnsureUninstaller()
    {
        if (!CanSelfUpdate) return;
        try
        {
            var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
            var id = Velopack.Locators.VelopackLocator.Current?.AppId;
            var exe = root is null ? "" : Path.Combine(root, "Uninstall.exe");
            if (id is null || !File.Exists(exe)) return;
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + id, writable: true);
            if (k is null || (k.GetValue("UninstallString") as string)?.Contains("Uninstall.exe", StringComparison.OrdinalIgnoreCase) == true) return;
            k.SetValue("UninstallString", $"\"{exe}\" --uninstall");
            k.SetValue("QuietUninstallString", $"\"{exe}\" --uninstall --silent");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { Log.Warn($"Đăng ký bộ gỡ: {e.Message}"); }
    }

    public static void NoteVersion()
    {
        try
        {
            var o = JsonStore.ReadObject(StateFile);
            var prev = o["version"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            var applying = o["applying"] is JsonValue a && a.TryGetValue<string>(out var t) ? t : null;
            if (UpdatePolicy.ApplyResult(applying, AppInfo.Version) == ApplyOutcome.Failed)
            {
                StartupNotice = L.F("update.applyFailed", applying!, AppInfo.Version);
                Log.Warn($"Cài bản {applying} không thành, đang dùng {AppInfo.Version}");
                o["failedVersion"] = applying;
            }
            if (applying is not null) { o.Remove("applying"); JsonStore.Write(StateFile, o); }
            if (prev == AppInfo.Version) return;
            if (prev is not null) Log.Info($"Đã cập nhật {prev} lên {AppInfo.Version}");
            o["version"] = AppInfo.Version;
            o.Remove("failedVersion");
            JsonStore.Write(StateFile, o);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Warn($"Ghi phiên bản: {e.Message}"); }
    }

    public static Velopack.VelopackAsset? StartupUpdate(string[] args)
    {
        if (args.Any(a => a.StartsWith("--veloapp", StringComparison.OrdinalIgnoreCase))) return null;
        try
        {
            var other = Mutex.TryOpenExisting(InstanceLock.Current, out var running);
            running?.Dispose();
            var restarted = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VELOPACK_RESTART"));
            if (!UpdatePolicy.ApplyOnStartup(Mode, Paths.Kind, args, other, restarted)) return null;
            var locator = Velopack.Locators.VelopackLocator.CreateDefaultForPlatform();
            var pkg = locator.GetLatestLocalFullPackage();
            var current = locator.CurrentlyInstalledVersion;
            if (pkg is null || current is null || pkg.Version <= current) return null;
            var state = JsonStore.ReadObject(StateFile);
            if (state["failedVersion"]?.ToString() == pkg.Version.ToString()) return null;
            Log.Info($"Chế độ tự động: cài bản {pkg.Version} đã tải lúc mở app (đang chạy {current})");
            return pkg;
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Warn($"Kiểm tra bản đã tải lúc mở app: {e.Message}"); return null; }
    }

    public static bool ApplyAtStartup(Velopack.VelopackAsset pkg, string[] args)
    {
        if (UpdatePolicy.PlanApply(ApplyTrigger.Startup, Paths.Kind, Mode, downloaded: true, alreadyLaunched: false) is not { } plan) return false;
        MarkApplying(pkg.Version.ToString());
        try
        {
            var locator = Velopack.Locators.VelopackLocator.CreateDefaultForPlatform();
            new Velopack.UpdateManager(locator.PackagesDir ?? AppContext.BaseDirectory, null, locator)
                .WaitExitThenApplyUpdates(pkg, plan.Silent, plan.Restart, args);
            return true;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Warn($"Cài bản đã tải lúc mở app: {e.Message}");
            ClearApplying();
            return false;
        }
    }

    public async Task<UpdateOffer?> CheckAsync(bool manual, CancellationToken ct)
    {
        if (!Supported) return null;
        await Gate.WaitAsync(ct);
        try
        {
            LastError = null;
            var offer = CanSelfUpdate ? await CheckInstalledAsync(ct) : await CheckLatestReleaseAsync(ct);
            if (offer is not null && !manual && offer.Version == Config.Str("app.update.skip")) offer = null;
            if (offer?.Version != Offer?.Version) { Downloaded = false; Download = null; }
            Offer = offer;
            return offer;
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            LastError = e.Message;
            Log.Warn($"Kiểm tra cập nhật: {e.Message}");
            return null;
        }
        finally
        {
            Gate.Release();
            SaveLastCheck();
            Changed?.Invoke();
        }
    }

    private static void SaveLastCheck()
    {
        try
        {
            var o = JsonStore.ReadObject(StateFile);
            o["lastCheck"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            JsonStore.Write(StateFile, o);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Warn($"Ghi lần kiểm tra cập nhật: {e.Message}"); }
    }

    public static void Skip(UpdateOffer o) => Config.Set("app.update.skip", o.Version);

    public async Task<bool> DownloadAsync(bool userAsked, CancellationToken ct)
    {
        if (Offer is not { Native: not null } offer) return false;
        if (Downloaded) return true;
        if (!userAsked && !UpdatePolicy.CanDownload(Power.BatterySaverOn)) return false;
        try { await Gate.WaitAsync(ct); }
        catch (OperationCanceledException) { return false; }
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _downloadCts = cts;
        var tracker = new DownloadTracker(offer.Version, offer.Bytes);
        try
        {
            LastError = null;
            Publish(tracker.State);
            var mgr = Manager();
            var info = await mgr.CheckForUpdatesAsync();
            cts.Token.ThrowIfCancellationRequested();
            if (info is null || info.TargetFullRelease.Version.ToString() != offer.Version)
            {
                LastError = L.T("update.changed");
                Download = null;
                return false;
            }
            await mgr.DownloadUpdatesAsync(info, p => { if (tracker.Report(p)) Publish(tracker.State); }, cts.Token);
            tracker.Complete();
            Download = tracker.State;
            Downloaded = true;
            Log.Info($"Đã tải bản {offer.Version}");
            return true;
        }
        catch (Exception e) when (cts.IsCancellationRequested)
        {
            Download = null;
            Log.Info($"Đã hủy tải bản {offer.Version} ({e.GetType().Name})");
            return false;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Download = null;
            Log.Warn($"Tải bản cập nhật: {e.Message}");
            return false;
        }
        finally
        {
            _downloadCts = null;
            Gate.Release();
            Changed?.Invoke();
        }
    }

    private void Publish(DownloadState s)
    {
        Download = s;
        Changed?.Invoke();
    }

    public void CancelDownload()
    {
        try { _downloadCts?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    public bool ApplyAndRestart()
    {
        if (Offer?.Native is not Velopack.VelopackAsset a) return false;
        if (UpdatePolicy.PlanApply(ApplyTrigger.UserRestart, Paths.Kind, Mode, Downloaded, _applyLaunched) is not { } plan) return false;
        if (!Launch(a, plan)) return false;
        Restarting?.Invoke(a.Version.ToString());
        return true;
    }

    public void ApplyOnExit()
    {
        if (Offer?.Native is not Velopack.VelopackAsset a) return;
        if (UpdatePolicy.PlanApply(ApplyTrigger.AppExit, Paths.Kind, Mode, Downloaded, _applyLaunched) is not { } plan) return;
        Launch(a, plan);
    }

    private bool Launch(Velopack.VelopackAsset a, ApplyPlan plan)
    {
        MarkApplying(a.Version.ToString());
        try
        {
            Manager().WaitExitThenApplyUpdates(a, plan.Silent, plan.Restart);
            _applyLaunched = true;
            Log.Info($"Đã gọi Update.exe cài bản {a.Version} (silent, {(plan.Restart ? "mở lại app" : "không mở lại app")})");
            return true;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LastError = e.Message;
            Log.Warn($"Cài bản cập nhật: {e.Message}");
            ClearApplying();
            Changed?.Invoke();
            return false;
        }
    }

    private static void MarkApplying(string version)
    {
        try
        {
            var o = JsonStore.ReadObject(StateFile);
            o["applying"] = version;
            JsonStore.Write(StateFile, o);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Warn($"Ghi bản sắp cài: {e.Message}"); }
    }

    private static void ClearApplying()
    {
        try
        {
            var o = JsonStore.ReadObject(StateFile);
            if (o.Remove("applying")) JsonStore.Write(StateFile, o);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Warn($"Bỏ dấu bản sắp cài: {e.Message}"); }
    }

    private static string Source()
    {
        var src = Config.Str("app.update.source");
        var ok = src.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
                 || (IsTestInstall && (UpdatePolicy.IsValidSource(src, Directory.Exists) || UpdatePolicy.IsLoopbackHttp(src)));
        return ok ? src : throw new InvalidOperationException(L.T("update.badSource"));
    }

    private static Velopack.UpdateManager Manager()
    {
        var src = Source();
        return new Velopack.UpdateManager(src.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ? FeedUrl(src) : src);
    }

    private static async Task<UpdateOffer?> CheckInstalledAsync(CancellationToken ct)
    {
        var info = await Manager().CheckForUpdatesAsync();
        ct.ThrowIfCancellationRequested();
        if (info is null) return null;
        var a = info.TargetFullRelease;
        var bytes = info.DeltasToTarget is { Length: > 0 } d ? d.Sum(x => x.Size) : a.Size;
        return new UpdateOffer(a.Version.ToString(), bytes, a.NotesMarkdown ?? "", ReleasesUrl, a);
    }

    public static string FeedUrl(string repo) => repo.TrimEnd('/') + "/releases/download/updates";

    private static string ReleasesUrl => Config.Str("app.update.source").TrimEnd('/') + "/releases/latest";

    private static async Task<UpdateOffer?> CheckLatestReleaseAsync(CancellationToken ct)
    {
        if (!Uri.TryCreate(Source(), UriKind.Absolute, out var u) || u.Host != "github.com") return null;
        var api = $"https://api.github.com/repos{u.AbsolutePath.TrimEnd('/')}/releases/latest";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BKStudyDesk");
        using var resp = await http.GetAsync(api, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
        var tag = json?["tag_name"]?.GetValue<string>() ?? "";
        var page = json?["html_url"]?.GetValue<string>() is { } h && h.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ? h : ReleasesUrl;
        return UpdatePolicy.IsNewer(tag, AppInfo.Version)
            ? new UpdateOffer(tag.TrimStart('v', 'V'), 0, json?["body"]?.GetValue<string>() ?? "", page, null)
            : null;
    }
}

internal static class Power
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus s);

    public static bool BatterySaverOn => OperatingSystem.IsWindows() && GetSystemPowerStatus(out var s) && s.SystemStatusFlag == 1;
}
