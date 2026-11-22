using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace BKStudyDesk.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public static void ApplyTheme(string theme) => Current!.RequestedThemeVariant = theme switch
    {
        "light" => ThemeVariant.Light,
        "system" => ThemeVariant.Default,
        _ => ThemeVariant.Dark,
    };

    public static string AccentHex { get; private set; } = "";

    internal static SoHocTap.Ui.FontChoice Font { get; private set; } = SoHocTap.Ui.FontChoice.Default;

    private static readonly string[] FontRoles = ["FontBody", "FontTitle", "FontCard", "FontSub", "FontMeta", "FontDay", "FontMonth", "MathScale", "MathScaleDisplay"];
    private static readonly Dictionary<string, double> DesignSizes = [];
    private static HashSet<string>? _fonts;

    public static IReadOnlyList<string> InstalledFonts() => [.. FontNames().Order(StringComparer.CurrentCultureIgnoreCase)];

    private static HashSet<string> FontNames() =>
        _fonts ??= new HashSet<string>(Avalonia.Media.FontManager.Current.SystemFonts.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

    public static bool IsFontInstalled(string name) => FontNames().Contains(name);

    internal static void ApplyFont(SoHocTap.Ui.FontChoice choice)
    {
        var res = Current!.Resources;
        var scale = choice.Scale(SoHocTap.Presentation.FontConfig.DesignBodySize);
        foreach (var key in FontRoles)
        {
            if (!DesignSizes.TryGetValue(key, out var design))
                DesignSizes[key] = design = Current.Styles.TryGetResource(key, null, out var v) && v is double d ? d : SoHocTap.Presentation.FontConfig.DesignBodySize;
            res[key] = SoHocTap.Ui.FontChoice.Scaled(design, scale);
        }
        res["ControlContentThemeFontSize"] = SoHocTap.Ui.FontChoice.Scaled(SoHocTap.Presentation.FontConfig.DesignBodySize, scale);
        if (choice.Family.Length > 0) res["ContentControlThemeFontFamily"] = new Avalonia.Media.FontFamily(choice.Family);
        else res.Remove("ContentControlThemeFontFamily");
        Font = choice;
        AppearanceChanged?.Invoke();
    }

    internal static event Action? AppearanceChanged;

    private static readonly string[] DataGridSelectedKeys =
    [
        "DataGridRowSelectedBackgroundBrush", "DataGridRowSelectedUnfocusedBackgroundBrush",
        "DataGridRowSelectedHoveredBackgroundBrush", "DataGridRowSelectedHoveredUnfocusedBackgroundBrush",
    ];

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
            d["Accent"] = Brush(rgb);
            if (Current.TryGetResource("Surface", variant, out var s) && s is Avalonia.Media.ISolidColorBrush surface)
            {
                var bg = (surface.Color.R, surface.Color.G, surface.Color.B);
                d["DateBox"] = Brush(SoHocTap.Presentation.AccentColors.Mix(rgb, bg, dark ? SoHocTap.Presentation.AccentColors.DateBoxTintDark : SoHocTap.Presentation.AccentColors.DateBoxTintLight));
                var selected = Brush(SoHocTap.Presentation.AccentColors.Mix(rgb, bg, dark ? SoHocTap.Presentation.AccentColors.SelectedTintDark : SoHocTap.Presentation.AccentColors.SelectedTintLight));
                d["RowSelected"] = selected;
                foreach (var key in DataGridSelectedKeys) d[key] = selected;
            }
        }
        AppearanceChanged?.Invoke();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Views.Practice.MathView.Warn = SoHocTap.Core.Log.Warn;
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            SoHocTap.Core.Log.Error("Lỗi giao diện không bắt được", e.Exception);
            e.Handled = true;
            SoHocTap.Core.AppEvents.RaiseUnhandled(e.Exception);
        };
        Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) =>
        {
            if (w is not MainWindow && !w.Classes.Contains("offscreen")) Platform.WindowPlacement.FitDialog(w);
        });
        ApplyAccent(SoHocTap.Core.Settings.App.Accent);
        ApplyFont(SoHocTap.Presentation.FontConfig.Read(IsFontInstalled));
        ApplyTheme(Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--theme=", StringComparison.Ordinal))?[8..] ?? SoHocTap.Core.Settings.App.Theme);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var main = new MainWindow();
            main.Closed += (_, _) => desktop.Shutdown();
            if (Platform.Autostart.LaunchedAtLogin) main.StartHidden();
            else desktop.MainWindow = main;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
