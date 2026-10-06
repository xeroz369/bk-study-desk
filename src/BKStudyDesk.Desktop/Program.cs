using Avalonia;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Bản mới đã tải ở lần trước (chế độ tự động): đọc trước khi Velopack chạy, như 1.x. Velopack không tự áp lúc mở (app tự áp ở
        // dưới, có ghi log); gỡ app thì bỏ mục mở cùng hệ điều hành.
        var pending = SoHocTap.Updates.UpdateService.StartupUpdate(args);
        Velopack.VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .OnBeforeUninstallFastCallback(_ => Platform.Autostart.Set(false))
            .Run();
        L.Load();   // ngôn ngữ (lang\*.json) theo app.language, trước khi dựng giao diện
        if (pending is not null && SoHocTap.Updates.UpdateService.ApplyAtStartup(pending, args)) return;   // Update.exe đợi app thoát rồi cài
        if (OperatingSystem.IsWindows()) SoHocTap.Updates.UpdateService.EnsureUninstaller();
        // Một bản: mở bản thứ hai thì nó chỉ đưa cửa sổ đang mở lên trước rồi thoát.
        using var instance = Platform.SingleInstance.Acquire(args);
        if (instance is null) return;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Error($"Unhandled exception{(e.IsTerminating ? " (app sẽ tắt)" : "")}", e.ExceptionObject as Exception);
        // Task chạy nền bị lỗi mà không ai await: không ghi log thì lỗi biến mất im lặng khi GC dọn task.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("Task nền lỗi mà không ai bắt", e.Exception);
            e.SetObserved();
            AppEvents.RaiseUnhandled(e.Exception);
        };
        // Bản zip (portable): thư mục data\ và code của app chỉ cho tài khoản này (bản cài nằm trong thư mục riêng của tài khoản sẵn rồi).
        if (Paths.Kind == InstallKind.Portable)
        {
            SecretStore.Lockdown(Paths.Data);
            _ = Task.Run(() => { foreach (var d in new[] { AppContext.BaseDirectory, Paths.Ui, Paths.Content }.Distinct()) SecretStore.Lockdown(d); });
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
