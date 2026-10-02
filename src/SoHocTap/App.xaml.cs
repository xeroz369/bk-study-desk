using System.Windows;
using SoHocTap.Core;

namespace SoHocTap;

public partial class App : Application
{
    /// <summary>Windows tự mở app lúc login (--tray): chạy dưới tray, không hiện window.</summary>
    public bool StartInTray { get; init; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Ngôn ngữ UI (lang\<mã>.json theo app.language): load trước khi tạo window.
        Ui.L.Load();
        DispatcherUnhandledException += (_, x) =>
        {
            Log.Error("Lỗi trên UI thread: " + x.Exception);
            x.Handled = true;
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
