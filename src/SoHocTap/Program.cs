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
        // Velopack phải chạy đầu tiên: lúc cài/gỡ/cập nhật, Update.exe gọi exe với tham số riêng, xử lý xong là thoát luôn.
        // Chế độ Tự động: bản đã tải (lần trước app thoát chưa kịp cài) được cài ngay lúc mở app, Update.exe mở lại app sau khi cài;
        // vẫn giữ cài lúc thoát (UpdateService.ApplyOnExit). Chế độ khác thì chỉ cài khi người dùng bấm.
        // Tắt auto-apply của Velopack: VelopackApp.Run gọi Update.exe không silent, hiện cửa sổ tiếng Anh "Installing Update" với thanh
        // chạy vô định. App tự gọi Update.exe silent ngay sau Run. Hỏi trước Run vì Run xóa biến VELOPACK_RESTART (chặn vòng lặp cài).
        // Gỡ app: xóa mục tự chạy. Dữ liệu (...\BKStudyDesk.Data) do bộ gỡ tiếng Việt (Uninstall.exe) xóa nếu người dùng chọn.
        var pending = Updates.UpdateService.StartupUpdate(args);
        Velopack.VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .OnBeforeUninstallFastCallback(_ => Startup.Remove())
            .Run();
        if (pending is not null && Updates.UpdateService.ApplyAtStartup(pending, args)) return;   // Update.exe đợi app thoát rồi cài
        Updates.UpdateService.EnsureUninstaller();
        // Single instance: mở bản thứ hai thì nó chỉ đưa window đang mở lên trước rồi thoát.
        using var mutex = new Mutex(initiallyOwned: true, AppInfo.InstanceKey, out bool first);
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
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Error($"Unhandled exception{(e.IsTerminating ? " (app sẽ tắt)" : "")}", e.ExceptionObject as Exception);
        // Task chạy nền bị lỗi mà không ai await (fire-and-forget): không ghi log thì lỗi biến mất im lặng khi GC dọn task.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("Task nền lỗi mà không ai bắt", e.Exception);
            e.SetObserved();
            AppEvents.RaiseUnhandled(e.Exception);
        };
        // Khóa ACL data\ cho riêng tài khoản Windows hiện tại (ổ D: thường để Everyone full quyền).
        // Bản Store và bản cài nằm trong LocalAppData, vốn chỉ của user này; bản zip/local thì tự khóa.
        if (Paths.Kind == InstallKind.Portable)
        {
            SecretStore.Lockdown(Paths.Data);
            // Code và nội dung app (exe, ui\, content\) cũng chỉ cho user này ghi: nếu không, user khác trên máy thay được
            // dll/exe (chạy lại lúc đăng nhập qua Run key) hay chèn script vào ui\. Chạy nền vì lần đầu phải áp quyền cho cả cây.
            _ = Task.Run(() => { foreach (var d in new[] { AppContext.BaseDirectory, Paths.Ui, Paths.Content }.Distinct()) SecretStore.Lockdown(d); });
        }
        var app = new App { StartInTray = args.Contains(Startup.TrayArg) || Startup.LaunchedAtLogin };
        app.InitializeComponent();
        app.Run();
    }
}
