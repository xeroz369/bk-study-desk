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
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
