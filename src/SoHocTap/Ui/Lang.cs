using System.Globalization;
using System.Text.Json;
using System.Windows.Markup;
using SoHocTap.Core;

namespace SoHocTap.Ui;

/// <summary>
/// Ngôn ngữ UI. Mỗi ngôn ngữ là một file JSON phẳng trong folder lang\ cạnh exe: lang\vi.json, lang\en.json…
/// Key dạng "nhóm.tên" (vd "nav.today"), value là chữ hiển thị; {0}, {1} là placeholder cho string.Format.
/// Key "_meta.name" là tên ngôn ngữ hiện ở Cài đặt, "_meta.culture" là culture để format số và ngày (vi-VN, en-US…).
/// Thiếu key thì fallback về tiếng Việt (ngôn ngữ gốc), vẫn thiếu thì hiện luôn key để dễ thấy chỗ chưa dịch.
/// Thêm ngôn ngữ: copy vi.json thành xx.json, dịch value, restart app rồi chọn ở Cài đặt (xem lang\README.md).
/// </summary>
public static class L
{
    private static Dictionary<string, string> _current = [], _fallback = [];

    public const string Base = "vi";
    public static string Code { get; private set; } = Base;
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("vi-VN");

    private static string Dir => Path.Combine(AppContext.BaseDirectory, "lang");

    /// <summary>Load ngôn ngữ theo config app.language (default tiếng Việt). Chỉ gọi một lần lúc khởi động.</summary>
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
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            Log.Warn($"Không đọc được file ngôn ngữ {path}: {e.Message}");
            return [];
        }
    }

    /// <summary>Lấy chữ theo key.</summary>
    public static string T(string key) =>
        _current.TryGetValue(key, out var v) ? v : _fallback.TryGetValue(key, out var f) ? f : key;

    /// <summary>Chữ có placeholder: F("home.nextExamOf", "Giải tích 2").</summary>
    public static string F(string key, params object?[] args) => string.Format(Culture, T(key), args);

    /// <summary>Các ngôn ngữ có trong folder lang\: (mã, tên).</summary>
    public static IReadOnlyList<(string Code, string Name)> Available() =>
        Directory.Exists(Dir)
            ? Directory.GetFiles(Dir, "*.json").Select(p => Path.GetFileNameWithoutExtension(p))
                .Select(c => (c, Read(c).GetValueOrDefault("_meta.name", c))).OrderBy(x => x.c == Base ? "" : x.c).ToList()
            : [(Base, "Tiếng Việt")];
}

/// <summary>Dùng trong XAML: Text="{ui:T nav.today}". Chữ chỉ lấy một lần lúc dựng UI, nên đổi ngôn ngữ phải restart app.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(Key);
}
