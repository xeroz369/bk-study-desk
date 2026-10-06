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

    public override void OnFrameworkInitializationCompleted()
    {
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
