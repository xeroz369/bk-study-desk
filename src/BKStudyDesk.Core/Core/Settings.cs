using System.Text.Json.Nodes;

namespace SoHocTap.Core;

public interface ISettingsStore
{
    JsonNode? Node(string dotted);
    JsonNode? DefaultValue(string dotted);
    void Write(string dotted, JsonNode? value);
}

public static partial class Settings
{
    private static ISettingsStore? _store;

    internal static ISettingsStore Store
    {
        get => _store ??= CreateDefaultStore();
        set => _store = value;
    }

    private static partial ISettingsStore CreateDefaultStore();

    public static IReadOnlyList<string> Keys { get; } =
    [
        "notify.hoursBefore", "notify.lastHours", "notify.urgentHours", "notify.soonHours", "notify.digestTimes",
        "app.update.mode", "app.update.checkHours",
        "library.baseUrl", "library.contributePath",
        "app.language", "app.closeToTray", "app.theme", "app.accent",
        "sso.rememberDays", "sso.autoLogin", "sso.autoLoginAsked",
        "sources.lms.syncHours", "sources.mybk.syncHours", "sources.mybk.enabled", "sources.lms.maxFileMB", "sources.lms.autoDownload", "sources.lms.saveQuizzes", "sources.lms.groupPattern",
        "archives.maxMB", "archives.extract",
        "viewer.pdfApp", "folders.root",
    ];

    private static int GetInt(string key)
    {
        if (Store.Node(key) is JsonValue v && v.TryGetValue<int>(out var n) && n >= 0) return n;
        return DefaultInt(key);
    }

    private static int DefaultInt(string key) => Store.DefaultValue(key) is JsonValue d && d.TryGetValue<int>(out var m) ? m : 0;

    private static string GetStr(string key)
    {
        if (Store.Node(key) is JsonValue v && v.TryGetValue<string>(out var s)) return s;
        return Store.DefaultValue(key) is JsonValue d && d.TryGetValue<string>(out var t) ? t : "";
    }

    private static bool GetBool(string key)
    {
        if (Store.Node(key) is JsonValue v && v.TryGetValue<bool>(out var b)) return b;
        return Store.DefaultValue(key) is JsonValue d && d.TryGetValue<bool>(out var c) && c;
    }

    private static void SetInt(string key, int value) => Store.Write(key, JsonValue.Create(value));
    private static void SetStr(string key, string value) => Store.Write(key, JsonValue.Create(value));
    private static void SetBool(string key, bool value) => Store.Write(key, JsonValue.Create(value));

    public static class Notify
    {
        public static int FirstHours { get => GetInt("notify.hoursBefore"); set => SetInt("notify.hoursBefore", value); }
        public static int LastHours { get => GetInt("notify.lastHours"); set => SetInt("notify.lastHours", value); }
        public static int UrgentHours { get => GetInt("notify.urgentHours"); set => SetInt("notify.urgentHours", value); }
        public static int SoonHours { get => GetInt("notify.soonHours"); set => SetInt("notify.soonHours", value); }
        public static string DigestTimes { get => GetStr("notify.digestTimes"); set => SetStr("notify.digestTimes", value); }
        public static string Morning { get => GetStr("notify.morning"); set => SetStr("notify.morning", value); }
    }

    public static class Update
    {
        public static string Mode { get => GetStr("app.update.mode"); set => SetStr("app.update.mode", value); }
        public static int CheckHours { get => GetInt("app.update.checkHours"); set => SetInt("app.update.checkHours", value); }
    }

    public static class Library
    {
        public static string BaseUrl { get => GetStr("library.baseUrl"); set => SetStr("library.baseUrl", value); }
        public static string ContributePath { get => GetStr("library.contributePath"); set => SetStr("library.contributePath", value); }
    }

    public static class App
    {
        public static string Language { get => GetStr("app.language"); set => SetStr("app.language", value); }
        public static bool CloseToTray { get => GetBool("app.closeToTray"); set => SetBool("app.closeToTray", value); }
        public static string OpenSchoolIn { get => GetStr("app.openSchoolIn"); set => SetStr("app.openSchoolIn", value); }
        public static string Theme { get => GetStr("app.theme"); set => SetStr("app.theme", value); }
        public static string Accent { get => GetStr("app.accent"); set => SetStr("app.accent", value); }
        public static string DefaultAccent => Store.DefaultValue("app.accent") is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
    }

    public static class Sso
    {
        public static int RememberDays { get => GetInt("sso.rememberDays"); set => SetInt("sso.rememberDays", value); }
        public static int DefaultRememberDays => DefaultInt("sso.rememberDays");
        public static bool AutoLogin { get => GetBool("sso.autoLogin"); set => SetBool("sso.autoLogin", value); }
        public static bool AutoLoginAsked { get => GetBool("sso.autoLoginAsked"); set => SetBool("sso.autoLoginAsked", value); }
    }

    public static class Sync
    {
        public static int LmsHours { get => GetInt("sources.lms.syncHours"); set => SetInt("sources.lms.syncHours", value); }
        public static int MybkHours { get => GetInt("sources.mybk.syncHours"); set => SetInt("sources.mybk.syncHours", value); }
        public static bool MybkEnabled { get => GetBool("sources.mybk.enabled"); set => SetBool("sources.mybk.enabled", value); }
        public static int MinFreshDays { get => GetInt("app.minFreshDays"); set => SetInt("app.minFreshDays", value); }
        public static int MaxFileMB { get => GetInt("sources.lms.maxFileMB"); set => SetInt("sources.lms.maxFileMB", value); }
        public static bool AutoDownload { get => GetBool("sources.lms.autoDownload"); set => SetBool("sources.lms.autoDownload", value); }
        public static bool SaveQuizzes { get => GetBool("sources.lms.saveQuizzes"); set => SetBool("sources.lms.saveQuizzes", value); }
    }

    public static class Lms
    {
        public static string GroupPattern { get => GetStr("sources.lms.groupPattern"); set => SetStr("sources.lms.groupPattern", value); }
    }

    public static class Archives
    {
        public static int MaxMB { get => GetInt("archives.maxMB"); set => SetInt("archives.maxMB", value); }
        public static bool Extract { get => GetBool("archives.extract"); set => SetBool("archives.extract", value); }
    }

    public static class OpenDocs
    {
        public static string PdfApp { get => GetStr("viewer.pdfApp"); set => SetStr("viewer.pdfApp", value); }
        public static string Root { get => GetStr("folders.root"); set => SetStr("folders.root", value); }
    }
}
