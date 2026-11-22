using System.Globalization;
using SoHocTap.Core;
using SoHocTap.Files;
using SoHocTap.Shell;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public abstract record SettingItem(string Label, string? Note);

public sealed record ToggleItem(string Label, string? Note, Func<bool> Get, Action<bool> Set) : SettingItem(Label, Note);

public sealed record NumberItem(string Label, string? Note, int Min, Func<int> Get, Action<int> Set) : SettingItem(Label, Note)
{
    public int? Parse(string text) => int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var v) && v >= Min ? v : null;
    public string Error => L.F("settings.numberError", Min);
}

public sealed record TextItem(string Label, string? Note, Func<string> Get, Func<string, string?> Normalize, Action<string> Set, string Error) : SettingItem(Label, Note);

public sealed record ChoiceItem(string Label, string? Note, IReadOnlyList<(string Value, string Text)> Options, Func<string> Get, Action<string> Set) : SettingItem(Label, Note);

public sealed record ActionItem(string Label, string? Note, string Button, Func<Task<string?>> Run) : SettingItem(Label, Note);

public sealed record CheckItem(string Label, string? Note, string Button, Func<Task<CheckStep>> Check) : SettingItem(Label, Note);

public sealed record CheckStep(string Message, string? Confirm = null, Func<Task<(string Message, bool Error)>>? Apply = null);

public sealed record InfoItem(string Label, string Value) : SettingItem(Label, null);

public sealed record CredentialItem(string Label, string? Note, Func<bool> IsOn, Func<string, string, string?> Save, Func<string> Off) : SettingItem(Label, Note);

public sealed record SettingGroup(string Title, string? Note, IReadOnlyList<SettingItem> Items, bool Advanced = false);

public sealed record SettingsActions(
    Func<string> AccountText, Action Login, Func<Task> Logout, Action SyncNow,
    Action<string> ApplyTheme, Action<string> ApplyAccent,
    Func<bool> AutostartGet, Func<bool, bool> AutostartSet,
    Func<Task<string?>> PickRoot, Func<string, Task> OpenLink, Func<Task> OpenLog,
    Action<string> SetLibraryUrl, Action ClearLibrary,
    bool AutoLoginSupported = false, Func<string, string, string?>? AutoLoginSave = null, Action? ForgetCredentials = null,
    bool UpdateSupported = false, Func<Task<string?>>? UpdateCheck = null, Func<Task<string?>>? UpdateInstall = null,
    int PageCount = 0, Action? Refresh = null, Action? Restart = null,
    Func<IReadOnlyList<string>>? Fonts = null, Action<string, double>? ApplyFont = null, Func<Task<string?>>? Backup = null);

public static class SettingsCatalog
{
    public static IReadOnlyList<(string Value, string Text)> Themes =>
        [("dark", L.T("set.themeDark")), ("light", L.T("set.themeLight")), ("system", L.T("set.themeSystem"))];

    private static Task<string?> Done(string? message) => Task.FromResult(message);

    public static CheckStep OrganizeCheck()
    {
        var plan = Organizer.PlanDownloads();
        return OrganizeStep(plan, () => Organizer.MoveDownloads(plan));
    }

    public static CheckStep OrganizeStep(IReadOnlyList<Organizer.DownloadMove> plan, Func<Organizer.DownloadResult> move)
    {
        if (plan.Count == 0) return new(L.T("organize.none"));
        const int Shown = 8;
        var lines = plan.Take(Shown).Select(m => L.F("organize.line", Path.GetFileName(m.Source), m.Subject)).ToList();
        if (plan.Count > Shown) lines.Add(L.F("organize.more", plan.Count - Shown));
        return new(string.Join(Environment.NewLine, lines), L.F("organize.move", plan.Count), () => Task.Run(() => OrganizeResult(move())));
    }

    public static (string, bool) OrganizeResult(Organizer.DownloadResult r)
    {
        var parts = new List<string> { L.F("organize.moved", r.Moved) };
        if (r.Duplicates > 0) parts.Add(L.F("organize.dups", r.Duplicates));
        if (r.Failed.Count > 0) parts.Add(L.F("organize.failed", r.Failed.Count));
        return (string.Join(" ", parts), r.Failed.Count > 0);
    }

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

    public static bool Matches(SettingGroup g, SettingItem i, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var q = query.Trim();
        return Files.NameMatch.ContainsFolded(i.Label, q) || Files.NameMatch.ContainsFolded(i.Note, q) || Files.NameMatch.ContainsFolded(g.Title, q);
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
                    if (!on) a.ForgetCredentials?.Invoke();
                }),
            .. a.AutoLoginSupported && a.AutoLoginSave is { } save
                ? new SettingItem[] { new CredentialItem(L.T("set.autoLogin"), L.T(Settings.Sso.AutoLogin ? "set.autoLoginOn" : "set.autoLoginOff"),
                    () => Settings.Sso.AutoLogin, save, () => { a.ForgetCredentials?.Invoke(); return L.T("set.autoLoginOffDone"); }) }
                : [],
            new ToggleItem(L.T("set.mybkSync"), L.T("set.mybkSyncNote"), () => Settings.Sync.MybkEnabled, on => Settings.Sync.MybkEnabled = on),
            new ChoiceItem(L.T("set.openSchoolIn"), L.T("set.openSchoolInNote"), [("browser", L.T("set.openBrowser")), ("app", L.T("set.openApp"))],
                () => Settings.App.OpenSchoolIn, v => Settings.App.OpenSchoolIn = v),
            new ActionItem(L.T("settings.logout"), L.T("set.logoutNote"), L.T("settings.logout"), async () => { await a.Logout(); return L.T("set.loggedOut"); }),
        ]),
        new(L.T("set.appearance"), null,
        [
            new ChoiceItem(L.T("set.theme"), null, Themes, () => Settings.App.Theme, v => { Settings.App.Theme = v; a.ApplyTheme(v); }),
            new ChoiceItem(L.T("set.accent"), L.T("set.accentNote"), [.. AccentColors.Choices().Select(c => (c.Value, c.Name))],
                () => Settings.App.Accent, v => { Settings.App.Accent = v; a.ApplyAccent(v); }),
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
        .. a.Backup is { } backup ? new SettingGroup[]
        {
            new(L.T("settings.data"), null,
            [
                new ActionItem(L.T("settings.backup"), L.T("settings.backupNote"), L.T("settings.backupButton"), backup),
            ]),
        } : [],
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
            new NumberItem(L.T("settings.minFreshDays"), L.T("settings.minFreshDaysNote"), 1, () => Settings.Sync.MinFreshDays, v => Settings.Sync.MinFreshDays = v),
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
        new(L.T("settings.organize"), null,
        [
            new CheckItem(L.T("organize.row"), L.T("organize.note"), L.T("organize.check"), () => Task.Run(OrganizeCheck)),
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
