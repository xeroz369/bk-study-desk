using Avalonia;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack chạy đầu tiên: lúc cài/cập nhật/gỡ, nó gọi app với tham số riêng, xử lý xong là thoát.
        Velopack.VelopackApp.Build().Run();
        L.Load();   // ngôn ngữ (lang\*.json) theo app.language, trước khi dựng giao diện
        // Bản mới đã tải ở lần trước (chế độ tự động): cài trước khi mở app, như 1.x. Update.exe đợi app thoát rồi cài.
        var pending = SoHocTap.Updates.UpdateService.StartupUpdate(args);
        if (pending is not null && SoHocTap.Updates.UpdateService.ApplyAtStartup(pending, args)) return;
        if (OperatingSystem.IsWindows()) SoHocTap.Updates.UpdateService.EnsureUninstaller();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
