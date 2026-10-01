using System.Runtime.InteropServices;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace SoHocTap;

internal static class Program
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [STAThread]
    private static void Main(string[] args)
    {
        // Single instance: mở bản thứ hai thì nó chỉ đưa window đang mở lên trước rồi thoát.
        using var mutex = new Mutex(initiallyOwned: true, AppInfo.Id, out bool first);
        // Restart (Cài đặt → Ngôn ngữ): bản cũ đang thoát, đợi nó nhả mutex tối đa 5 giây.
        if (!first && args.Contains("--restart"))
        {
            try { first = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (AbandonedMutexException) { first = true; }
        }
        if (!first)
        {
            SingleInstance.ActivateExisting();
            return;
        }
        // Taskbar group theo ID này và dùng icon của app. Bản Store đã có AUMID của package, set đè là hỏng pin taskbar.
        if (!AppPackage.IsPackaged && SetCurrentProcessExplicitAppUserModelID(AppInfo.Id) != 0) Log.Info("Không set được AppUserModelID");
        // Menu của tray icon (WinForms) theo theme sáng/tối của Windows.
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetColorMode(System.Windows.Forms.SystemColorMode.System);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Unhandled exception", e.ExceptionObject as Exception);
        Log.Info("App khởi động");
        // Khóa ACL data\ cho riêng tài khoản Windows hiện tại (ổ D: thường để Everyone full quyền).
        // Bản Store để data trong LocalAppData, vốn đã chỉ của user này.
        if (!AppPackage.IsPackaged) SecretStore.Lockdown(Paths.Data);
        var app = new App { StartInTray = args.Contains(Startup.TrayArg) || Startup.LaunchedAtLogin };
        app.InitializeComponent();
        app.Run();
    }
}
