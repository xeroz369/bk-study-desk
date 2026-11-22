using System.Globalization;
using System.Text.Json;
using SoHocTap.Core;

namespace SoHocTap.Ui;

public static class L
{
    private static Dictionary<string, string> _current = [], _fallback = [];

    public const string Base = "vi";
    public static string Code { get; private set; } = Base;
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("vi-VN");

    private static string Dir => Path.Combine(AppContext.BaseDirectory, "lang");

    public static void Load()
    {
        _fallback = Read(Base);
        var code = Config.Str("app.language", Base);
        _current = code == Base ? _fallback : Read(code);
        if (_current.Count == 0) { _current = _fallback; code = Base; }
        Code = code;
        try { Culture = CultureInfo.GetCultureInfo(T("_meta.culture")); }
        catch (CultureNotFoundException) { Culture = CultureInfo.InvariantCulture; }
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = Culture;
        Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = Culture;
    }

    private static Dictionary<string, string> Read(string code)
    {
        var path = Path.Combine(Dir, code + ".json");
        try
        {
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize(File.ReadAllText(path), CoreJson.Default.StringMap) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            Log.Warn($"Không đọc được file ngôn ngữ {path}: {e.Message}");
            return [];
        }
    }

    public static string T(string key) =>
        _current.TryGetValue(key, out var v) ? v : _fallback.TryGetValue(key, out var f) ? f : key;

    public static string F(string key, params object?[] args) => string.Format(Culture, T(key), args);

    public static IReadOnlyList<(string Code, string Name)> Available() =>
        Directory.Exists(Dir)
            ? Directory.GetFiles(Dir, "*.json").Select(p => Path.GetFileNameWithoutExtension(p))
                .Select(c => (c, Read(c).GetValueOrDefault("_meta.name", c))).OrderBy(x => x.c == Base ? "" : x.c).ToList()
            : [(Base, "Tiếng Việt")];
}
