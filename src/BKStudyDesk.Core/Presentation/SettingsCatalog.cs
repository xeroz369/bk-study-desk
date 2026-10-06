using System.Globalization;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>Một mục của trang Cài đặt. View vẽ theo kiểu (công tắc, ô số, ô chữ, ô chọn, nút) bằng control có sẵn; mỗi mục lưu ngay khi đổi.</summary>
public abstract record SettingItem(string Label, string? Note);

public sealed record ToggleItem(string Label, string? Note, Func<bool> Get, Action<bool> Set) : SettingItem(Label, Note);

/// <summary>Ô số nguyên, nhỏ nhất <paramref name="Min"/>. Sai thì báo lỗi và giữ giá trị cũ (Parse trả null).</summary>
public sealed record NumberItem(string Label, string? Note, int Min, Func<int> Get, Action<int> Set) : SettingItem(Label, Note)
{
    public int? Parse(string text) => int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var v) && v >= Min ? v : null;
    public string Error => L.F("settings.numberError", Min);
}

/// <summary>Ô chữ; Normalize trả bản chuẩn để lưu, null khi sai (giữ giá trị cũ, hiện Error).</summary>
public sealed record TextItem(string Label, string? Note, Func<string> Get, Func<string, string?> Normalize, Action<string> Set, string Error) : SettingItem(Label, Note);

public sealed record ChoiceItem(string Label, string? Note, IReadOnlyList<(string Value, string Text)> Options, Func<string> Get, Action<string> Set) : SettingItem(Label, Note);

/// <summary>Nút làm một việc (đăng nhập, mở file log, xóa bộ nhớ đệm); Run trả câu báo kết quả (null là không báo).</summary>
public sealed record ActionItem(string Label, string? Note, string Button, Func<Task<string?>> Run) : SettingItem(Label, Note);

/// <summary>Một thẻ của trang Cài đặt. Advanced: nằm trong phần Nâng cao (đóng sẵn).</summary>
public sealed record SettingGroup(string Title, string? Note, IReadOnlyList<SettingItem> Items, bool Advanced = false);

/// <summary>Việc của trang Cài đặt cần tới giao diện hay phần chạy nền (View, AppHost truyền vào).</summary>
public sealed record SettingsActions(Func<string> AccountText, Action Login, Func<Task> Logout, Action<string> ApplyTheme, Action SyncNow,
    Func<Task> OpenLog, Action<string> SetLibraryUrl, Action ClearLibrary, Func<bool> AutostartGet, Func<bool, bool> AutostartSet,
    bool AutoLoginSupported = false, Func<Task<string?>>? AutoLoginToggle = null, Action? ForgetCredentials = null,
    bool UpdateSupported = false, Func<Task<string?>>? UpdateCheck = null, Func<Task<string?>>? UpdateInstall = null);

/// <summary>
/// Danh sách mục của trang Cài đặt bản đa nền tảng (cùng khóa cài đặt với 1.x, Ui/Settings/*): Thường dùng (tài khoản, giao diện, nhắc hạn,
/// khi đóng app) rồi Nâng cao (đồng bộ, mở tài liệu, thư viện, nhật ký). Thêm mục = thêm một dòng ở đây; View không cần sửa.
/// </summary>
public static class SettingsCatalog
{
    /// <summary>MyBK đồng bộ cách nhau ít nhất 6 giờ: dữ liệu MyBK đổi chậm, đọc dày hơn chỉ tốn request của trường.</summary>
    private const int MybkMinHours = 6;

    public static IReadOnlyList<(string Value, string Text)> Themes { get; } = [("dark", "Tối"), ("light", "Sáng"), ("system", "Theo hệ điều hành")];

    public static IReadOnlyList<SettingGroup> Groups(SettingsActions a) =>
    [
        new(L.T("settings.account"), a.AccountText(),
        [
            new ActionItem(L.T("common.loginHcmut"), "Đăng nhập một lần cho cả LMS và MyBK. Mật khẩu chỉ gõ trong trang của trường.", "Đăng nhập", () => { a.Login(); return Task.FromResult<string?>(null); }),
            new ToggleItem(L.T("settings.remember"), null, () => Settings.Sso.RememberDays > 0,
                on =>
                {
                    Settings.Sso.RememberDays = on ? Settings.Sso.DefaultRememberDays : 0;
                    if (!on) a.ForgetCredentials?.Invoke();   // không ghi nhớ đăng nhập thì cũng không giữ mật khẩu để tự đăng nhập
                }),
            new ToggleItem(L.T("settings.keepAlive"), null, () => Settings.Sso.KeepAliveMinutes > 0,
                on => Settings.Sso.KeepAliveMinutes = on ? Settings.Sso.DefaultKeepAliveMinutes : 0),
            .. a.AutoLoginSupported && a.AutoLoginToggle is { } toggle
                ? new SettingItem[] { new ActionItem("Tự đăng nhập lại khi hết phiên", Settings.Sso.AutoLogin ? "Đang bật." : "Đang tắt. Mặc định tắt: app không giữ mật khẩu.",
                    Settings.Sso.AutoLogin ? "Tắt" : "Bật...", toggle) }
                : [],
            new ActionItem(L.T("settings.logout"), "Chỉ xóa phiên đăng nhập. Tài liệu, kết quả và dữ liệu đã đồng bộ vẫn còn.", L.T("settings.logout"),
                async () => { await a.Logout(); return "Đã đăng xuất."; }),
        ]),
        new("Giao diện", null,
        [
            new ChoiceItem("Chế độ màu", null, Themes, () => Settings.App.Theme, v => { Settings.App.Theme = v; a.ApplyTheme(v); }),
        ]),
        new(L.T("settings.reminders"), null,
        [
            new NumberItem(L.T("settings.notifyHours"), "hiện thông báo của hệ điều hành, 0 = không nhắc lần này", 0, () => Settings.Notify.FirstHours, v => Settings.Notify.FirstHours = v),
            new NumberItem(L.T("settings.lastHours"), L.T("settings.lastHoursNote"), 0, () => Settings.Notify.LastHours, v => Settings.Notify.LastHours = v),
            new NumberItem(L.T("settings.urgentHours"), L.T("settings.urgentHoursNote"), 0, () => Settings.Notify.UrgentHours, v => Settings.Notify.UrgentHours = v),
            new NumberItem(L.T("settings.soonHours"), L.T("settings.soonHoursNote"), 0, () => Settings.Notify.SoonHours, v => Settings.Notify.SoonHours = v),
            new TextItem(L.T("settings.digestTimes"), L.T("settings.digestTimesNote"), () => Settings.Notify.DigestTimes,
                s => NotifyDigest.Parse(s) is { } slots ? NotifyDigest.Format(slots) : null, v => Settings.Notify.DigestTimes = v, L.T("settings.digestTimesError")),
        ]),
        new(L.T("update.settings"), a.UpdateSupported ? null : "Bản chạy từ thư mục build không tự cập nhật; bản cài từ bộ cài thì có.",
        [
            new ChoiceItem("Khi có phiên bản mới", null, [("notify", L.T("update.mode.notify")), ("auto", L.T("update.mode.auto")), ("off", L.T("update.mode.off"))],
                () => UpdatePolicy.ParseMode(Settings.Update.Mode) switch { UpdateMode.Auto => "auto", UpdateMode.Off => "off", _ => "notify" },
                v => Settings.Update.Mode = v),
            .. a.UpdateSupported && a.UpdateCheck is { } check && a.UpdateInstall is { } install
                ? new SettingItem[] { new ActionItem(L.T("update.check"), null, "Kiểm tra", check), new ActionItem(L.T("update.install"), "Tải bản mới rồi cài, app tự mở lại.", L.T("update.install"), install) }
                : [],
        ]),
        new(L.T("settings.startup"), null,
        [
            new ToggleItem("Mở cùng hệ điều hành", "App chạy ở khay khi đăng nhập máy, để đồng bộ và nhắc hạn.", a.AutostartGet, on => a.AutostartSet(on)),
            new ToggleItem(L.T("settings.closeToTray"), "Tắt thì nút X thoát hẳn app.", () => Settings.App.CloseToTray, v => Settings.App.CloseToTray = v),
        ]),
        new(L.T("settings.sync"), null,
        [
            new NumberItem(L.T("settings.lmsHours"), L.T("settings.lmsHoursNote"), 1, () => Settings.Sync.LmsHours, v => Settings.Sync.LmsHours = v),
            new NumberItem(L.T("settings.mybkHours"), L.T("settings.mybkHoursNote"), MybkMinHours, () => Settings.Sync.MybkHours, v => Settings.Sync.MybkHours = v),
            new NumberItem(L.T("settings.maxMb"), null, 1, () => Settings.Sync.MaxFileMB, v => Settings.Sync.MaxFileMB = v),
            new ToggleItem(L.T("settings.autoDownload"), null, () => Settings.Sync.AutoDownload, v => Settings.Sync.AutoDownload = v),
            new ToggleItem(L.T("settings.autoExtract"), null, () => Settings.Archives.Extract, v => Settings.Archives.Extract = v),
            new ToggleItem(L.T("settings.saveQuizzes"), null, () => Settings.Sync.SaveQuizzes, v => Settings.Sync.SaveQuizzes = v),
            new ActionItem("Đồng bộ lại ngay", null, "Đồng bộ", () => { a.SyncNow(); return Task.FromResult<string?>(L.T("settings.resyncing")); }),
        ], Advanced: true),
        new(L.T("settings.openDocs"), null,
        [
            new TextItem(L.T("settings.pdfApp"), "Để trống: dùng app mặc định của hệ điều hành. Ghi đường dẫn đầy đủ tới app đọc PDF (vd. SumatraPDF).",
                () => Settings.OpenDocs.PdfApp, s => s.Trim(), v => Settings.OpenDocs.PdfApp = v, ""),
        ], Advanced: true),
        new(L.T("settings.library"), L.T("settings.library.note"),
        [
            new TextItem(L.T("settings.library.baseUrl"), null, () => Settings.Library.BaseUrl,
                s => s.Trim() is var t && (t.Length == 0 || Uri.TryCreate(t, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps) ? t : null,
                a.SetLibraryUrl, "Cần địa chỉ https://, hay để trống để tắt thư viện. Đã giữ giá trị cũ."),
            new ActionItem(L.T("settings.library.clear"), null, "Xóa", () => { a.ClearLibrary(); return Task.FromResult<string?>("Đã xóa bộ nhớ đệm."); }),
        ], Advanced: true),
        new(L.T("settings.debug"), L.T("settings.debugNote"),
        [
            new ToggleItem(L.T("settings.debugLog"), null, DiagnosticLog.Active, DiagnosticLog.Set),
            new ActionItem(L.T("settings.openLog"), null, "Mở", async () => { await a.OpenLog(); return null; }),
        ], Advanced: true),
    ];
}
