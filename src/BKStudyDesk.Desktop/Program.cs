using Avalonia;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var pending = SoHocTap.Updates.UpdateService.StartupUpdate(args);
        var velopack = Velopack.VelopackApp.Build().SetAutoApplyOnStartup(false);
        if (OperatingSystem.IsWindows()) velopack = velopack.OnBeforeUninstallFastCallback(_ => Platform.Autostart.Set(false));
        velopack.Run();
        L.Load();
        if (pending is not null && SoHocTap.Updates.UpdateService.ApplyAtStartup(pending, args)) return;
        if (OperatingSystem.IsWindows()) SoHocTap.Updates.UpdateService.EnsureUninstaller();
        using var instance = Platform.SingleInstance.Acquire(args);
        if (instance is null) { Log.Debug("Đã có bản khác đang chạy: báo nó rồi thoát"); return; }
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Error($"Unhandled exception{(e.IsTerminating ? " (app sẽ tắt)" : "")}", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("Task nền lỗi mà không ai bắt", e.Exception);
            e.SetObserved();
            AppEvents.RaiseUnhandled(e.Exception);
        };
        if (Paths.Kind == InstallKind.Portable)
        {
            SecretStore.Lockdown(Paths.Data);
            _ = Task.Run(() => { foreach (var d in new[] { AppContext.BaseDirectory, Paths.Content }.Distinct().Where(Directory.Exists)) SecretStore.Lockdown(d); });
        }
        Log.Info($"Khởi động: {Environment.OSVersion}, {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}, kiểu cài {Paths.Kind}");
        var code = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        Log.Info($"Thoát: mã {code}");
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
