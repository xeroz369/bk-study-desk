using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Ui;

namespace SoHocTap.Updates;

/// <summary>Một bản mới tìm thấy. <see cref="Native"/> là VelopackAsset (bản cài) hoặc null (bản zip: chỉ có link tải).</summary>
public sealed record UpdateOffer(string Version, long Bytes, string Notes, string Url, object? Native);

/// <summary>
/// Kiểm tra và cài bản mới theo lựa chọn của người dùng (app.update.mode, xem UpdatePolicy).
/// Bản cài (Setup.exe): Velopack tải gói delta, kiểm SHA-256, cài khi người dùng bấm hoặc khi app thoát hẳn (chế độ tự động).
/// Bản zip public: chỉ hỏi GitHub bản mới nhất để hiện link tải. Bản local và bản Store: không làm gì.
/// Chỉ gọi tới nguồn trong app.update.source, không gửi thông tin máy hay tài khoản. Không ném lỗi ra ngoài.
/// </summary>
public sealed class UpdateService
{
    private static string StateFile => Paths.DataFile("update-state.json");
    // Một lần kiểm tra/tải mỗi lúc (cả app dùng chung một UpdateService, sống tới khi app thoát).
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public UpdateOffer? Offer { get; private set; }
    /// <summary>Gói của <see cref="Offer"/> đã tải xong, chờ cài.</summary>
    public bool Downloaded { get; private set; }
    public string? LastError { get; private set; }
    public event Action? Changed;
    /// <summary>Ngay trước khi thoát để cài bản mới: dọn icon khay, dừng đồng bộ.</summary>
    public event Action? Restarting;

    /// <summary>Lần trước cài bản mới không thành (Velopack giữ bản cũ): câu báo cho thanh trạng thái, null nếu không có.</summary>
    public static string? StartupNotice { get; private set; }

    public static UpdateMode Mode => UpdatePolicy.ParseMode(Config.Str("app.update.mode", "ask"));

    /// <summary>Bản này có phần cập nhật không (bản public, không phải Store).</summary>
    public static bool Supported =>
        Paths.Kind != InstallKind.Store;

    /// <summary>Tự cài được (bản cài Velopack); bản zip chỉ báo link.</summary>
    public static bool CanSelfUpdate => Supported && Paths.Kind == InstallKind.Installed;

    /// <summary>Bản cài của bài test cập nhật (pack id BKStudyDeskTest): chỉ bản này nhận cờ --update-now.</summary>
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

    /// <summary>Tới hạn kiểm tra tự động chưa (chế độ, kiểu bản, chu kỳ).</summary>
    public bool Due() => Supported && UpdatePolicy.ShouldCheck(Mode, Paths.Kind, DateTimeOffset.UtcNow, LastCheck,
        Config.Int("app.update.checkHours", 24));

    /// <summary>
    /// Bản cài có bộ gỡ tiếng Việt (Uninstall.exe cạnh Update.exe) thì mục gỡ cài đặt trong Windows trỏ tới nó.
    /// Velopack có thể ghi lại khóa này khi cập nhật, nên mỗi lần mở app kiểm lại.
    /// </summary>
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

    /// <summary>Ghi log khi vừa lên bản mới (so với lần chạy trước).</summary>
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
            }
            if (applying is not null) { o.Remove("applying"); JsonStore.Write(StateFile, o); }
            if (prev == AppInfo.Version) return;
            if (prev is not null) Log.Info($"Đã cập nhật {prev} → {AppInfo.Version}");
            o["version"] = AppInfo.Version;
            JsonStore.Write(StateFile, o);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Warn($"Ghi phiên bản: {e.Message}"); }
    }

    /// <summary>Hỏi nguồn có bản mới không. <paramref name="manual"/>: người dùng bấm "Kiểm tra cập nhật" (bỏ qua "bỏ qua bản này").</summary>
    public async Task<UpdateOffer?> CheckAsync(bool manual, CancellationToken ct)
    {
        if (!Supported) return null;
        await Gate.WaitAsync(ct);
        try
        {
            LastError = null;
            var offer = CanSelfUpdate ? await CheckInstalledAsync(ct) : await CheckLatestReleaseAsync(ct);
            if (offer is not null && !manual && offer.Version == Config.Str("app.update.skip")) offer = null;
            if (offer?.Version != Offer?.Version) Downloaded = false;   // bản khác bản đã tải: phải tải lại
            Offer = offer;
            return offer;
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            // Mất mạng, quá thời gian, GitHub giới hạn lượt gọi…: ghi một dòng, đợi chu kỳ sau (lastCheck vẫn ghi bên dưới).
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

    /// <summary>Tải gói (bản cài). Trả false nếu đang bật Tiết kiệm pin (chế độ tự động), bị hủy hoặc tải lỗi.</summary>
    public async Task<bool> DownloadAsync(bool userAsked, Action<int>? progress, CancellationToken ct)
    {
        if (Offer is not { Native: not null } offer) return false;
        if (Downloaded) return true;
        if (!userAsked && !UpdatePolicy.CanDownload(Power.BatterySaverOn)) return false;
        try { await Gate.WaitAsync(ct); }
        catch (OperationCanceledException) { return false; }
        try
        {
            var mgr = Manager();
            var info = await mgr.CheckForUpdatesAsync();
            if (info is null || info.TargetFullRelease.Version.ToString() != offer.Version)
            {
                LastError = L.T("update.changed");
                return false;
            }
            await mgr.DownloadUpdatesAsync(info, progress, ct);
            Downloaded = true;
            Log.Info($"Đã tải bản {offer.Version}");
            return true;
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            LastError = e.Message;
            Log.Warn($"Tải bản cập nhật: {e.Message}");
            return false;
        }
        catch (OperationCanceledException) { return false; }
        finally
        {
            Gate.Release();
            Changed?.Invoke();
        }
    }

    /// <summary>Thoát, cài bản đã tải rồi mở lại app (người dùng bấm "Khởi động lại để cập nhật").</summary>
    public void ApplyAndRestart()
    {
        if (!Downloaded || Offer?.Native is not Velopack.VelopackAsset a) return;
        MarkApplying(a.Version.ToString());
        Restarting?.Invoke();
        Manager().ApplyUpdatesAndRestart(a);
    }

    /// <summary>
    /// Gọi lúc app thoát hẳn: chế độ tự động và đã tải xong thì Update.exe đợi app thoát (tối đa 60 giây) rồi cài, không mở lại app.
    /// Không gọi lúc vừa tải xong: app có thể chạy dưới khay nhiều giờ, quá 60 giây là Update.exe bỏ cuộc.
    /// </summary>
    public void ApplyOnExit()
    {
        if (Mode != UpdateMode.Auto || !Downloaded || Offer?.Native is not Velopack.VelopackAsset a) return;
        MarkApplying(a.Version.ToString());
        try { Manager().WaitExitThenApplyUpdates(a, silent: true, restart: false); }
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Warn($"Cài bản cập nhật khi thoát: {e.Message}"); }
    }

    /// <summary>Ghi lại bản sắp cài, để lần mở sau biết cài thành hay không (UpdatePolicy.ApplyResult).</summary>
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

    /// <summary>Nguồn cập nhật: repo GitHub qua https. Thư mục trên máy chỉ nhận ở bản cài thử (tools-dev/test-update.ps1).</summary>
    private static string Source()
    {
        var src = Config.Str("app.update.source");
        var ok = src.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
                 || (IsTestInstall && UpdatePolicy.IsValidSource(src, Directory.Exists));
        return ok ? src : throw new InvalidOperationException(L.T("update.badSource"));
    }

    /// <summary>
    /// Nguồn: repo GitHub (mặc định) hoặc thư mục trên máy (chỉ dùng để test cập nhật). Gói cập nhật nằm ở release cố định
    /// "updates" của repo (trang release cho người dùng chỉ có bộ cài), Velopack đọc releases.&lt;kênh&gt;.json ở đó.
    /// </summary>
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

    /// <summary>https://github.com/&lt;chủ&gt;/&lt;repo&gt; → link tải của release "updates".</summary>
    public static string FeedUrl(string repo) => repo.TrimEnd('/') + "/releases/download/updates";

    private static string ReleasesUrl => Config.Str("app.update.source").TrimEnd('/') + "/releases/latest";

    /// <summary>Bản zip: đọc bản phát hành mới nhất trên GitHub (API công khai, không cần token) để hiện link tải.</summary>
    private static async Task<UpdateOffer?> CheckLatestReleaseAsync(CancellationToken ct)
    {
        if (!Uri.TryCreate(Source(), UriKind.Absolute, out var u) || u.Host != "github.com") return null;
        var api = $"https://api.github.com/repos{u.AbsolutePath.TrimEnd('/')}/releases/latest";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BKStudyDesk");
        using var resp = await http.GetAsync(api, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;   // repo chưa có bản phát hành nào
        resp.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
        var tag = json?["tag_name"]?.GetValue<string>() ?? "";
        // Link trang tải chỉ nhận https của GitHub, không mở link lạ từ dữ liệu trả về.
        var page = json?["html_url"]?.GetValue<string>() is { } h && h.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ? h : ReleasesUrl;
        return UpdatePolicy.IsNewer(tag, AppInfo.Version)
            ? new UpdateOffer(tag.TrimStart('v', 'V'), 0, json?["body"]?.GetValue<string>() ?? "", page, null)
            : null;
    }
}

/// <summary>Trạng thái Tiết kiệm pin của Windows (GetSystemPowerStatus, SystemStatusFlag = 1).</summary>
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

    public static bool BatterySaverOn => GetSystemPowerStatus(out var s) && s.SystemStatusFlag == 1;
}
