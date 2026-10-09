using System.Globalization;
using SoHocTap.Core;
using SoHocTap.Files;
using SoHocTap.Shell;
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

/// <summary>Dòng chỉ đọc: tên bên trái, giá trị bên phải (bảng phím tắt).</summary>
public sealed record InfoItem(string Label, string Value) : SettingItem(Label, null);

/// <summary>
/// Tài khoản cho tự đăng nhập lại: đang tắt thì nút Bật mở ô tên đăng nhập, mật khẩu ngay dưới dòng (không mở cửa sổ riêng).
/// Save trả lỗi để hiện, null là đã bật; Off tắt và xóa tài khoản đã lưu, trả câu báo.
/// </summary>
public sealed record CredentialItem(string Label, string? Note, Func<bool> IsOn, Func<string, string, string?> Save, Func<string> Off) : SettingItem(Label, Note);

/// <summary>Một thẻ của trang Cài đặt. Advanced: nằm trong phần Nâng cao (đóng sẵn).</summary>
public sealed record SettingGroup(string Title, string? Note, IReadOnlyList<SettingItem> Items, bool Advanced = false);

/// <summary>Việc của trang Cài đặt cần tới giao diện, hệ điều hành hay phần chạy nền (cửa sổ chính truyền vào).</summary>
public sealed record SettingsActions(
    Func<string> AccountText, Action Login, Func<Task> Logout, Action SyncNow,
    Action<string> ApplyTheme, Action<string> ApplyAccent,
    Func<bool> AutostartGet, Func<bool, bool> AutostartSet,
    Func<Task<string?>> PickRoot, Func<string, Task> OpenLink, Func<Task> OpenLog,
    Action<string> SetLibraryUrl, Action ClearLibrary,
    bool AutoLoginSupported = false, Func<string, string, string?>? AutoLoginSave = null, Action? ForgetCredentials = null,
    bool UpdateSupported = false, Func<Task<string?>>? UpdateCheck = null, Func<Task<string?>>? UpdateInstall = null,
    int PageCount = 0, Action? Refresh = null, Action? Restart = null,
    Func<IReadOnlyList<string>>? Fonts = null, Action<string, double>? ApplyFont = null);

/// <summary>
/// Danh sách mục của trang Cài đặt (cùng khóa cài đặt với 1.x, Ui/Settings/*): Thường dùng (tài khoản, giao diện, nhắc hạn, cập nhật,
/// khi mở và đóng app, giới thiệu) rồi Nâng cao (đồng bộ, mở tài liệu, sắp xếp file, thư viện, nhật ký). Chữ ở lang/*.json, giá trị
/// mặc định và giới hạn ở DefaultConfig.json. Thêm mục = thêm một dòng ở đây; View không cần sửa.
/// </summary>
public static class SettingsCatalog
{
    public static IReadOnlyList<(string Value, string Text)> Themes =>
        [("dark", L.T("set.themeDark")), ("light", L.T("set.themeLight")), ("system", L.T("set.themeSystem"))];

    private static Task<string?> Done(string? message) => Task.FromResult(message);

    /// <summary>Phông (mặc định của hệ điều hành rồi mọi phông trên máy), cỡ chữ, nút về mặc định. Đổi là áp ngay và lưu.</summary>
    private static SettingItem[] FontItems(IReadOnlyList<string> fonts, Action<string, double> apply, Action? refresh)
    {
        var installed = fonts.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var current = FontConfig.Read(installed.Contains);
        return
        [
            new ChoiceItem(L.T("settings.font.family"), null, [("", L.T("set.fontDefault")), .. fonts.Select(f => (f, f))],
                () => current.Family, v => { current = current with { Family = v }; apply(current.Family, current.Size); }),
            new ChoiceItem(L.T("settings.font.size"), L.T("set.fontNote"), FontConfig.SizeOptions(), () => FontConfig.Key(current.Size),
                v => { current = current with { Size = double.Parse(v, CultureInfo.InvariantCulture) }; apply(current.Family, current.Size); }),
            new ActionItem(L.T("settings.font.reset"), null, L.T("settings.font.reset"), () => { apply("", 0); refresh?.Invoke(); return Done(null); }),
        ];
    }

    public static IReadOnlyList<SettingGroup> Groups(SettingsActions a) =>
    [
        new(L.T("settings.account"), a.AccountText(),
        [
            new ActionItem(L.T("common.loginHcmut"), L.T("set.loginNote"), L.T("set.login"), () => { a.Login(); return Done(null); }),
            new ToggleItem(L.T("settings.remember"), null, () => Settings.Sso.RememberDays > 0,
                on =>
                {
                    Settings.Sso.RememberDays = on ? Settings.Sso.DefaultRememberDays : 0;
                    if (!on) a.ForgetCredentials?.Invoke();   // không ghi nhớ đăng nhập thì cũng không giữ mật khẩu để tự đăng nhập
                }),
            new ToggleItem(L.T("settings.keepAlive"), null, () => Settings.Sso.KeepAliveMinutes > 0,
                on => Settings.Sso.KeepAliveMinutes = on ? Settings.Sso.DefaultKeepAliveMinutes : 0),
            .. a.AutoLoginSupported && a.AutoLoginSave is { } save
                ? new SettingItem[] { new CredentialItem(L.T("set.autoLogin"), L.T(Settings.Sso.AutoLogin ? "set.autoLoginOn" : "set.autoLoginOff"),
                    () => Settings.Sso.AutoLogin, save, () => { a.ForgetCredentials?.Invoke(); return L.T("set.autoLoginOffDone"); }) }
                : [],
            new ActionItem(L.T("settings.logout"), L.T("set.logoutNote"), L.T("settings.logout"), async () => { await a.Logout(); return L.T("set.loggedOut"); }),
        ]),
        new(L.T("set.appearance"), null,
        [
            new ChoiceItem(L.T("set.theme"), null, Themes, () => Settings.App.Theme, v => { Settings.App.Theme = v; a.ApplyTheme(v); }),
            new ChoiceItem(L.T("set.accent"), L.T("set.accentNote"), [.. AccentColors.Choices().Select(c => (c.Value, c.Name))],
                () => Settings.App.Accent, v => { Settings.App.Accent = v; a.ApplyAccent(v); }),
            // Ngôn ngữ đổi sau khi mở lại app (chữ đã dựng không đổi theo): chọn xong thì hiện dòng Khởi động lại.
            new ChoiceItem(L.T("settings.language"), null, [.. L.Available().Select(x => (x.Code, x.Name))],
                () => Settings.App.Language is { Length: > 0 } code ? code : L.Code, v => { Settings.App.Language = v; a.Refresh?.Invoke(); }),
            .. Settings.App.Language is { Length: > 0 } chosen && chosen != L.Code && a.Restart is { } restart
                ? new SettingItem[] { new ActionItem(L.T("settings.languageNote"), null, L.T("settings.restart"), () => { restart(); return Done(null); }) }
                : [],
            .. a.Fonts is { } fonts && a.ApplyFont is { } applyFont ? FontItems(fonts(), applyFont, a.Refresh) : [],
        ]),
        new(L.T("settings.reminders"), null,
        [
            new NumberItem(L.T("settings.notifyHours"), L.T("set.notifyNote"), 0, () => Settings.Notify.FirstHours, v => Settings.Notify.FirstHours = v),
            new NumberItem(L.T("settings.lastHours"), L.T("settings.lastHoursNote"), 0, () => Settings.Notify.LastHours, v => Settings.Notify.LastHours = v),
            new NumberItem(L.T("settings.urgentHours"), L.T("settings.urgentHoursNote"), 0, () => Settings.Notify.UrgentHours, v => Settings.Notify.UrgentHours = v),
            new NumberItem(L.T("settings.soonHours"), L.T("settings.soonHoursNote"), 0, () => Settings.Notify.SoonHours, v => Settings.Notify.SoonHours = v),
            new TextItem(L.T("settings.digestTimes"), L.T("settings.digestTimesNote"), () => Settings.Notify.DigestTimes,
                s => NotifyDigest.Parse(s) is { } slots ? NotifyDigest.Format(slots) : null, v => Settings.Notify.DigestTimes = v, L.T("settings.digestTimesError")),
        ]),
        new(L.T("update.settings"), a.UpdateSupported ? null : L.T("set.updateDev"),
        [
            new ChoiceItem(L.T("set.updateWhen"), null, [("notify", L.T("update.mode.notify")), ("auto", L.T("update.mode.auto")), ("off", L.T("update.mode.off"))],
                () => UpdatePolicy.ParseMode(Settings.Update.Mode) switch { UpdateMode.Auto => "auto", UpdateMode.Off => "off", _ => "notify" },
                v => Settings.Update.Mode = v),
            .. a.UpdateSupported && a.UpdateCheck is { } check && a.UpdateInstall is { } install
                ? new SettingItem[] { new ActionItem(L.T("update.check"), null, L.T("set.check"), check), new ActionItem(L.T("update.install"), L.T("set.installNote"), L.T("update.install"), install) }
                : [],
        ]),
        new(L.T("settings.startup"), null,
        [
            new ToggleItem(L.T("set.autostart"), L.T("set.autostartNote"), a.AutostartGet, on => a.AutostartSet(on)),
            new ToggleItem(L.T("settings.closeToTray"), L.T("set.closeToTrayNote"), () => Settings.App.CloseToTray, v => Settings.App.CloseToTray = v),
        ]),
        // Chỉ ghi phím app thật sự có (MainWindow, bảng DataGrid, danh sách tài liệu ở trang Môn học).
        new(L.T("settings.keys"), null,
        [
            .. a.PageCount > 0 ? new SettingItem[] { new InfoItem(L.T("settings.keys.pages"), L.F("settings.keys.range", "Ctrl+1", $"Ctrl+{a.PageCount}")) } : [],
            new InfoItem(L.T("settings.keys.sync"), "F5"),
            new InfoItem(L.T("settings.keys.back"), L.T("settings.keys.backKey")),
            new InfoItem(L.T("settings.keys.rows"), L.T("settings.keys.rowsKey")),
            new InfoItem(L.T("settings.keys.sort"), L.T("settings.keys.click")),
            new InfoItem(L.T("settings.keys.up"), "Backspace"),
        ]),
        new(L.T("about.title"), L.F("set.version", AppInfo.Version),
        [
            new ActionItem(L.T("about.source"), AppInfo.Repo, L.T("set.open"), async () => { await a.OpenLink(AppInfo.Repo); return null; }),
            new ActionItem(L.T("about.issues"), null, L.T("set.open"), async () => { await a.OpenLink(AppInfo.Issues); return null; }),
            new ActionItem(L.T("about.privacy"), null, L.T("set.open"), async () => { await a.OpenLink(AppInfo.Repo + "/blob/main/PRIVACY.md"); return null; }),
        ]),
        new(L.T("settings.sync"), null,
        [
            new NumberItem(L.T("settings.lmsHours"), L.T("settings.lmsHoursNote"), 1, () => Settings.Sync.LmsHours, v => Settings.Sync.LmsHours = v),
            // MyBK đổi chậm: đồng bộ dày hơn sources.mybk.minSyncHours chỉ tốn request của trường.
            new NumberItem(L.T("settings.mybkHours"), L.T("settings.mybkHoursNote"), Config.Int("sources.mybk.minSyncHours", 1), () => Settings.Sync.MybkHours, v => Settings.Sync.MybkHours = v),
            new NumberItem(L.T("settings.maxMb"), null, 1, () => Settings.Sync.MaxFileMB, v => Settings.Sync.MaxFileMB = v),
            new ToggleItem(L.T("settings.autoDownload"), null, () => Settings.Sync.AutoDownload, v => Settings.Sync.AutoDownload = v),
            new ToggleItem(L.T("settings.autoExtract"), null, () => Settings.Archives.Extract, v => Settings.Archives.Extract = v),
            new ToggleItem(L.T("settings.saveQuizzes"), null, () => Settings.Sync.SaveQuizzes, v => Settings.Sync.SaveQuizzes = v),
            new ActionItem(L.T("settings.resync"), L.T("settings.resync.tip"), L.T("set.resyncButton"), () => { a.SyncNow(); return Done(L.T("settings.resyncing")); }),
        ], Advanced: true),
        new(L.T("settings.openDocs"), null,
        [
            new TextItem(L.T("settings.root"), L.F("settings.rootNote", Paths.StudyRoot), () => Settings.OpenDocs.Root,
                s => s.Trim() is var t && (t.Length == 0 || Directory.Exists(t)) ? t : null, v => Settings.OpenDocs.Root = v, L.T("set.rootError")),
            new ActionItem(L.T("settings.rootPick"), null, L.T("settings.browse"), async () =>
            {
                if (await a.PickRoot() is not { } picked) return null;
                Settings.OpenDocs.Root = picked;
                return L.F("settings.rootNote", picked);
            }),
            new TextItem(L.T("settings.pdfApp"), L.T("set.pdfNote"), () => Settings.OpenDocs.PdfApp, s => s.Trim(), v => Settings.OpenDocs.PdfApp = v, ""),
        ], Advanced: true),
        new(L.T("settings.organize"), L.T("set.organizeNote"),
        [
            new ActionItem(L.T("settings.preview"), null, L.T("settings.preview"), () =>
            {
                var lines = Organizer.ImportDownloads(apply: false);
                return Done(lines.Count == 0 ? L.T("settings.planEmpty") : string.Join(Environment.NewLine, lines));
            }),
            new ActionItem(L.T("settings.apply"), null, L.T("settings.apply"), () => Done(L.F("settings.applied", Organizer.ImportDownloads(apply: true).Count))),
        ], Advanced: true),
        new(L.T("settings.library"), L.T("settings.library.note"),
        [
            new TextItem(L.T("settings.library.baseUrl"), null, () => Settings.Library.BaseUrl,
                s => s.Trim() is var t && (t.Length == 0 || Uri.TryCreate(t, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps) ? t : null,
                a.SetLibraryUrl, L.T("set.libraryUrlError")),
            new ActionItem(L.T("settings.library.clear"), null, L.T("set.clear"), () => { a.ClearLibrary(); return Done(L.T("set.cleared")); }),
        ], Advanced: true),
        new(L.T("settings.debug"), L.T("settings.debugNote"),
        [
            new ToggleItem(L.T("settings.debugLog"), null, DiagnosticLog.Active, DiagnosticLog.Set),
            new ActionItem(L.T("settings.openLog"), null, L.T("set.open"), async () => { await a.OpenLog(); return null; }),
        ], Advanced: true),
    ];
}
