using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using SoHocTap.Core;

namespace SoHocTap.Ui;

/// <summary>Một phông trong ô chọn ở Cài đặt: Source lưu vào config, Name là tên hiện cho người dùng.</summary>
internal sealed record FontItem(string Source, string Name)
{
    public override string ToString() => Name;   // tên cho screen reader đọc
}

/// <summary>
/// Phông, cỡ chữ của cả app (Cài đặt, thẻ Chữ). Theme Fluent của WPF (.NET 10) không đặt FontFamily/FontSize cho control nào:
/// chữ kế thừa từ Window, mặc định là phông, cỡ chữ thông báo của Windows (SystemFonts.MessageFont*, thường Segoe UI 12).
/// Nên app đặt resource ở Application.Resources, mọi Window bind FontFamily/FontSize vào <see cref="FamilyKey"/>/<see cref="SizeKey"/>,
/// các vai chữ (tiêu đề, chữ phụ) dùng key riêng giãn theo cùng tỉ lệ. Đổi là thấy ngay, không cần khởi động lại.
/// Phông biểu tượng (IconFont, SymbolThemeFontFamily: Segoe Fluent Icons) không đổi.
/// </summary>
internal static class AppFont
{
    public const string FamilyKey = "AppFontFamily";
    public const string SizeKey = "AppFontSize";

    /// <summary>Vai chữ của app và cỡ thiết kế ở cỡ mặc định (thân chữ 12). Giữ đúng các số cũ để cỡ mặc định trông y như trước.</summary>
    private static readonly (string Key, double Design)[] Roles =
    [
        ("AppFontSizeCaption", 11),   // nhãn giờ, chi tiết buổi ở lưới tuần
        ("AppFontSizeSub", 13),       // dòng mô tả trang, ghi chú
        ("AppFontSizeSection", 14),   // tiêu đề khối, tiêu đề InfoBar
        ("AppFontSizeHeading", 16),   // tiêu đề hộp thoại nhỏ, ngày ở lưới tuần
        ("AppFontSizeTitle", 18),     // tiêu đề hộp thoại, tên môn
        ("AppFontSizeDisplay", 20),   // tiêu đề trang, số liệu lớn
    ];

    /// <summary>
    /// Key cỡ chữ của theme Fluent (PresentationFramework.Fluent, Resources/Variables.xaml, Styles/TextBlock.xaml, DataGrid.xaml,
    /// TreeViewItem.xaml, GroupBox.xaml của .NET 10), giá trị theo thân chữ 14. Đè lại ở Application.Resources để control, style nào
    /// tra key bằng DynamicResource (GroupBox header, code của app) cũng giãn theo. Chọn mặc định thì bỏ phần đè, theme dùng số của nó.
    /// </summary>
    private static readonly (string Key, double Value)[] FluentSizes =
    [
        ("ControlContentThemeFontSize", 14),
        ("ContentControlFontSize", 14),
        ("BodyTextBlockFontSize", 14),
        ("CaptionTextBlockFontSize", 12),
        ("SubtitleTextBlockFontSize", 20),
        ("TitleTextBlockFontSize", 28),
        ("TitleLargeTextBlockFontSize", 40),
        ("DisplayTextBlockFontSize", 68),
        ("DefaultDataGridFontSize", 14),
        ("TreeViewItemFontSize", 14),
        ("GroupBoxHeaderFontSize", 20),
    ];

    /// <summary>Thân chữ của Fluent: mốc để giãn <see cref="FluentSizes"/>.</summary>
    private const double FluentBody = 14;

    /// <summary>Phông, cỡ đang dùng (đã kiểm). Đổi ở Cài đặt thì báo <see cref="Changed"/> (thanh trên cùng đo lại, khung Luyện tập đổi theo).</summary>
    public static FontChoice Current { get; private set; } = FontChoice.Default;

    public static event Action? Changed;

    /// <summary>Cỡ chữ mặc định của Windows (cỡ thân chữ khi người dùng chọn Mặc định).</summary>
    public static double SystemSize => SystemFonts.MessageFontSize;

    public static string SystemFamily => SystemFonts.MessageFontFamily.Source;

    /// <summary>Đọc config và áp vào Application.Resources. Gọi lúc khởi động, trước khi tạo window.</summary>
    public static void Load(Application app)
    {
        var size = Config.Node("app.font.size") is JsonValue v && v.TryGetValue<double>(out var d) ? d : (double?)null;
        var family = Config.Node("app.font.family") is JsonValue f && f.TryGetValue<string>(out var s) ? s : null;
        var choice = FontChoice.Parse(family, size, IsInstalled);
        if (family is { Length: > 0 } && choice.Family.Length == 0) Log.Warn($"Phông trong config không có trên máy, dùng mặc định: {family}");
        Apply(app, choice);
    }

    /// <summary>Người dùng chọn ở Cài đặt: áp ngay rồi lưu config (trống, 0 = mặc định, Config chỉ ghi phần khác default).</summary>
    public static void Set(FontChoice choice)
    {
        choice = FontChoice.Parse(choice.Family, choice.Size, IsInstalled);
        if (choice == Current) return;
        Apply(Application.Current, choice);
        var c = (JsonObject)Config.Current.DeepClone();
        var appNode = c["app"] as JsonObject ?? (JsonObject)(c["app"] = new JsonObject());
        appNode["font"] = new JsonObject { ["family"] = choice.Family, ["size"] = choice.Size };
        Config.Save(c);
    }

    private static void Apply(Application app, FontChoice choice)
    {
        var res = app.Resources;
        var scale = choice.Scale(SystemSize);
        var body = choice.BodySize(SystemSize);
        var family = choice.Family.Length > 0 ? new FontFamily(choice.Family) : SystemFonts.MessageFontFamily;
        res[FamilyKey] = family;
        res[SizeKey] = body;
        foreach (var (key, design) in Roles) res[key] = FontChoice.Scaled(design, scale);
        // Hàng, tiêu đề cột của DataGrid (App.xaml): cao theo chữ, không nhỏ hơn số cũ.
        res["AppGridRowHeight"] = Math.Max(28, Math.Ceiling(28 * scale));
        res["AppGridHeaderHeight"] = Math.Max(30, Math.Ceiling(30 * scale));
        if (choice.IsDefault)
        {
            foreach (var (key, _) in FluentSizes) res.Remove(key);
            res.Remove(SystemFonts.StatusFontFamilyKey);
            res.Remove(SystemFonts.StatusFontSizeKey);
        }
        else
        {
            foreach (var (key, value) in FluentSizes) res[key] = FontChoice.Scaled(value * body / FluentBody, 1);
            // ToolTip của Fluent lấy phông, cỡ theo SystemFonts.StatusFont* (DynamicResource), đè key đó để tooltip cũng đổi theo.
            res[SystemFonts.StatusFontFamilyKey] = family;
            res[SystemFonts.StatusFontSizeKey] = FontChoice.Scaled(SystemFonts.StatusFontSize, scale);
        }
        Current = choice;
        Changed?.Invoke();
    }

    private static HashSet<string>? _installed;

    /// <summary>Phông có trên máy không (so với Fonts.SystemFontFamilies). Chỉ dựng danh sách khi có phông cần kiểm.</summary>
    private static bool IsInstalled(string family)
    {
        _installed ??= new HashSet<string>(Fonts.SystemFontFamilies.Select(f => f.Source), StringComparer.OrdinalIgnoreCase);
        return _installed.Contains(family);
    }

    /// <summary>Phông trên máy cho ô chọn ở Cài đặt: tên theo ngôn ngữ UI (không có thì tên tiếng Anh), xếp theo chữ cái.</summary>
    public static List<FontItem> Installed()
    {
        var ui = XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag);
        var en = XmlLanguage.GetLanguage("en-us");
        var cmp = StringComparer.Create(L.Culture, ignoreCase: true);
        return [.. Fonts.SystemFontFamilies
            .Select(f => new FontItem(f.Source,
                f.FamilyNames.TryGetValue(ui, out var n) ? n : f.FamilyNames.TryGetValue(en, out var e) ? e : f.Source))
            .Where(x => FontChoice.NormalizeFamily(x.Source, _ => true).Length > 0)
            .DistinctBy(x => x.Source, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Name, cmp)];
    }
}
