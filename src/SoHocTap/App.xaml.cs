using System.Windows;
using SoHocTap.Core;

namespace SoHocTap;

public partial class App : Application
{
    /// <summary>Windows tự mở app lúc login (--tray): chạy dưới tray, không hiện window.</summary>
    public bool StartInTray { get; init; }

    /// <summary>
    /// Một dòng log lúc mở app: đọc báo lỗi là biết ngay máy chạy bản nào, kiểu cài nào, ngôn ngữ, culture, múi giờ
    /// (lỗi giờ học lệch 7 tiếng thường do múi giờ máy). Không có gì cá nhân: không tên máy, không tên user.
    /// </summary>
    private static string StartupLine() =>
        $"App khởi động: {Shell.AppInfo.Name} {Shell.AppInfo.Version}, kiểu cài {Paths.Kind}, " +
        $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}), " +
        $"ngôn ngữ UI {Ui.L.Code}, culture {Ui.L.Culture.Name} (máy {System.Globalization.CultureInfo.InstalledUICulture.Name}), " +
        $"múi giờ {TimeZoneInfo.Local.Id} (UTC{Offset(TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow))}), .NET {Environment.Version}";

    private static string Offset(TimeSpan o) => (o < TimeSpan.Zero ? "-" : "+") + o.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Ngôn ngữ UI (lang\<mã>.json theo app.language): load trước khi tạo window.
        Ui.L.Load();
        Log.Info(StartupLine());
        DispatcherUnhandledException += (_, x) =>
        {
            Log.Error("Lỗi trên UI thread", x.Exception);
            x.Handled = true;
            // Handled = true để app không tắt; báo lên UI (thanh trạng thái) để người dùng biết có gì đó hỏng, không im lặng.
            AppEvents.RaiseUnhandled(x.Exception);
        };
        // DataGrid nằm trong tab chưa mở có width = 0: WPF ép mọi column về width tối thiểu (chừa chỗ cho column co giãn) và
        // giữ luôn như vậy khi tab mở ra. Workaround: lần đầu grid có width thật thì set lại width thiết kế cho từng column (Ui/Grids.cs).
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.DataGrid), FrameworkElement.SizeChangedEvent,
            new SizeChangedEventHandler((sender, e) =>
            {
                if (e.PreviousSize.Width < 1 && e.NewSize.Width > 0) Ui.Grids.RestoreWidths((System.Windows.Controls.DataGrid)sender);
            }));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.DataGrid), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => Ui.Grids.RestoreWidths((System.Windows.Controls.DataGrid)sender)));
        Ui.ScrollBubble.Register();
        var main = new Ui.MainWindow();
        MainWindow = main;
        main.Start(hidden: StartInTray);
        Shell.SingleInstance.Listen(() => Dispatcher.InvokeAsync(main.ShowFromTray));
    }
}
