using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace BKStudyDesk.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>"dark", "light" hay "system" (theo hệ điều hành); giá trị lạ là tối.</summary>
    public static void ApplyTheme(string theme) => Current!.RequestedThemeVariant = theme switch
    {
        "light" => ThemeVariant.Light,
        "system" => ThemeVariant.Default,
        _ => ThemeVariant.Dark,
    };

    /// <summary>Màu nhấn đang dùng ("#RRGGBB"), để khung Luyện tập dùng cùng màu.</summary>
    public static string AccentHex { get; private set; } = "";

    /// <summary>Phông, cỡ chữ đang dùng (đã kiểm), để khung Luyện tập dùng cùng phông và tỉ lệ.</summary>
    internal static SoHocTap.Ui.FontChoice Font { get; private set; } = SoHocTap.Ui.FontChoice.Default;

    /// <summary>Các vai chữ (cỡ thiết kế ở Tokens.axaml); ApplyFont ghi bản đã giãn đè lên.</summary>
    private static readonly string[] FontRoles = ["FontBody", "FontTitle", "FontCard", "FontSub", "FontMeta", "FontDay", "FontMonth"];
    private static readonly Dictionary<string, double> DesignSizes = [];
    private static HashSet<string>? _fonts;

    /// <summary>Phông trên máy, xếp theo chữ cái.</summary>
    public static IReadOnlyList<string> InstalledFonts() => [.. FontNames().Order(StringComparer.CurrentCultureIgnoreCase)];

    private static HashSet<string> FontNames() =>
        _fonts ??= new HashSet<string>(Avalonia.Media.FontManager.Current.SystemFonts.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

    public static bool IsFontInstalled(string name) => FontNames().Contains(name);

    /// <summary>
    /// Áp phông, cỡ chữ cho cả app: mỗi vai chữ giãn theo cùng tỉ lệ (FontChoice.Scale), control Fluent theo ControlContentThemeFontSize,
    /// phông qua ContentControlThemeFontFamily (mặc định thì bỏ khóa để Fluent dùng phông của hệ điều hành).
    /// </summary>
    internal static void ApplyFont(SoHocTap.Ui.FontChoice choice)
    {
        var res = Current!.Resources;
        var scale = choice.Scale(SoHocTap.Presentation.FontConfig.DesignBodySize);
        foreach (var key in FontRoles)
        {
            if (!DesignSizes.TryGetValue(key, out var design))   // đọc cỡ thiết kế một lần, trước khi ghi đè
                DesignSizes[key] = design = Current.Styles.TryGetResource(key, null, out var v) && v is double d ? d : SoHocTap.Presentation.FontConfig.DesignBodySize;
            res[key] = SoHocTap.Ui.FontChoice.Scaled(design, scale);
        }
        res["ControlContentThemeFontSize"] = SoHocTap.Ui.FontChoice.Scaled(SoHocTap.Presentation.FontConfig.DesignBodySize, scale);
        if (choice.Family.Length > 0) res["ContentControlThemeFontFamily"] = new Avalonia.Media.FontFamily(choice.Family);
        else res.Remove("ContentControlThemeFontFamily");
        Font = choice;
        AppearanceChanged?.Invoke();
    }

    /// <summary>Màu nhấn hay phông vừa đổi (khung Luyện tập đổi theo ngay).</summary>
    internal static event Action? AppearanceChanged;

    private static readonly string[] DataGridSelectedKeys =
    [
        "DataGridRowSelectedBackgroundBrush", "DataGridRowSelectedUnfocusedBackgroundBrush",
        "DataGridRowSelectedHoveredBackgroundBrush", "DataGridRowSelectedHoveredUnfocusedBackgroundBrush",
    ];

    /// <summary>
    /// Màu nhấn ("#RRGGBB" hay "system" là màu nhấn của hệ điều hành): đặt cho Fluent (nút chính, ô chọn, viền focus) và màu thanh trên
    /// cùng (AccentColors.Bar: tối dần tới khi chữ trắng đọc rõ, chế độ tối tối hơn). Màu sai dạng thì giữ màu nhấn trong config mặc định.
    /// </summary>
    public static void ApplyAccent(string value)
    {
        var rgb = value == "system" && Current!.PlatformSettings?.GetColorValues().AccentColor1 is { } sys ? (sys.R, sys.G, sys.B)
            : SoHocTap.Presentation.AccentColors.Parse(value) ?? SoHocTap.Presentation.AccentColors.Parse(SoHocTap.Core.Settings.App.DefaultAccent)!.Value;
        AccentHex = SoHocTap.Presentation.AccentColors.Hex(rgb);
        var color = Avalonia.Media.Color.FromRgb(rgb.R, rgb.G, rgb.B);
        var fluent = Current!.Styles.OfType<Avalonia.Themes.Fluent.FluentTheme>().First();
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            if (fluent.Palettes.TryGetValue(variant, out var palette)) palette.Accent = color;
            var dark = variant == ThemeVariant.Dark;
            if (!Current.Resources.ThemeDictionaries.TryGetValue(variant, out var dict)) Current.Resources.ThemeDictionaries[variant] = dict = new Avalonia.Controls.ResourceDictionary();
            var d = (IResourceDictionary)dict;
            static Avalonia.Media.SolidColorBrush Brush((byte R, byte G, byte B) c) => new(Avalonia.Media.Color.FromRgb(c.R, c.G, c.B));
            d["Bar"] = Brush(SoHocTap.Presentation.AccentColors.Bar(rgb, dark));
            // Ô ngày, dòng đang chọn: màu nhấn pha lên nền thẻ (Surface trong Tokens) để đổi theo màu nhấn.
            if (Current.TryGetResource("Surface", variant, out var s) && s is Avalonia.Media.ISolidColorBrush surface)
            {
                var bg = (surface.Color.R, surface.Color.G, surface.Color.B);
                d["DateBox"] = Brush(SoHocTap.Presentation.AccentColors.Mix(rgb, bg, dark ? SoHocTap.Presentation.AccentColors.DateBoxTintDark : SoHocTap.Presentation.AccentColors.DateBoxTintLight));
                var selected = Brush(SoHocTap.Presentation.AccentColors.Mix(rgb, bg, dark ? SoHocTap.Presentation.AccentColors.SelectedTintDark : SoHocTap.Presentation.AccentColors.SelectedTintLight));
                d["RowSelected"] = selected;
                // DataGrid: dòng chọn nền nhạt thay cho nền accent đậm, chữ màu ở cột Còn vẫn đạt 4,5:1
                foreach (var key in DataGridSelectedKeys) d[key] = selected;
            }
        }
        AppearanceChanged?.Invoke();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ApplyAccent(SoHocTap.Core.Settings.App.Accent);
        ApplyFont(SoHocTap.Presentation.FontConfig.Read(IsFontInstalled));
        // Chế độ màu: cài đặt app.theme (mặc định tối); --theme=light|dark ép riêng lần chạy này (chụp kiểm tra).
        ApplyTheme(Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--theme="))?[8..] ?? SoHocTap.Core.Settings.App.Theme);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Hệ điều hành mở app lúc đăng nhập (--tray): chạy ở khay, không hiện cửa sổ; nút X không đóng app (ShutdownMode tường minh).
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var main = new MainWindow();
            main.Closed += (_, _) => desktop.Shutdown();
            if (Platform.Autostart.LaunchedAtLogin) main.StartHidden();
            else desktop.MainWindow = main;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
