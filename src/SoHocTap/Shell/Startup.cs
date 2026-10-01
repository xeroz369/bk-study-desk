using System.Runtime.InteropServices;
using Microsoft.Win32;
using SoHocTap.Core;

namespace SoHocTap.Shell;

/// <summary>
/// Khởi động cùng Windows.
/// <list type="bullet">
/// <item>Bản zip: ghi một value vào HKCU\Software\Microsoft\Windows\CurrentVersion\Run (chỉ user hiện tại, không cần admin),
/// Windows mở app với --tray nên app nằm dưới tray, không hiện window.</item>
/// <item>Bản Store (MSIX): registry trong package bị ảo hóa nên Run không có tác dụng; dùng StartupTask khai báo trong
/// Package.appxmanifest (TaskId <see cref="TaskId"/>). User tắt trong Task Manager thì app không tự bật lại được.</item>
/// </list>
/// </summary>
internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string TrayArg = "--tray";
    public const string TaskId = "BKStudyDeskStartup";

    private static string Command => $"\"{Environment.ProcessPath}\" {TrayArg}";

    /// <summary>Bản Store được Windows mở lúc đăng nhập (StartupTask): chạy dưới tray như --tray.</summary>
    public static bool LaunchedAtLogin
    {
        get
        {
#if STORE
            if (!AppPackage.IsPackaged) return false;
            try { return Windows.ApplicationModel.AppInstance.GetActivatedEventArgs()?.Kind == Windows.ApplicationModel.Activation.ActivationKind.StartupTask; }
            catch (Exception e) when (e is COMException or InvalidOperationException) { return false; }
#else
            return false;
#endif
        }
    }

    public static bool Enabled
    {
        get
        {
#if STORE
            if (AppPackage.IsPackaged)
            {
                try { return Task.Run(async () => (await Windows.ApplicationModel.StartupTask.GetAsync(TaskId)).State).Result is Windows.ApplicationModel.StartupTaskState.Enabled or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy; }
                catch (AggregateException e) { Log.Warn($"Đọc StartupTask lỗi: {e.InnerException?.Message}"); return false; }
            }
#endif
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(AppInfo.Id) is string v && v.Contains(Environment.ProcessPath ?? "\0", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Bật/tắt autostart. Trả về trạng thái thật sau khi đổi (bản Store có thể bị user hoặc policy chặn).</summary>
    public static bool Set(bool on)
    {
#if STORE
        if (AppPackage.IsPackaged)
        {
            try
            {
                return Task.Run(async () =>
                {
                    var task = await Windows.ApplicationModel.StartupTask.GetAsync(TaskId);
                    if (!on) { task.Disable(); return false; }
                    var state = await task.RequestEnableAsync();
                    if (state is not (Windows.ApplicationModel.StartupTaskState.Enabled or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy))
                        Log.Info($"StartupTask không bật được: {state}");
                    return state is Windows.ApplicationModel.StartupTaskState.Enabled or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
                }).Result;
            }
            catch (AggregateException e) { Log.Warn($"Đổi StartupTask lỗi: {e.InnerException?.Message}"); return Enabled; }
        }
#endif
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (on) k.SetValue(AppInfo.Id, Command);
            else if (k.GetValue(AppInfo.Id) is not null) k.DeleteValue(AppInfo.Id);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Log.Error("Khởi động cùng Windows", e);
        }
        return Enabled;
    }

    /// <summary>App bị dời chỗ (vd. giải nén ra folder khác) thì cập nhật path trong Run, nếu đang bật. Bản Store không cần.</summary>
    public static void Refresh()
    {
        if (AppPackage.IsPackaged) return;
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        if (k?.GetValue(AppInfo.Id) is string v && !v.Equals(Command, StringComparison.OrdinalIgnoreCase)) Set(true);
    }
}

/// <summary>Tên và id của bản app. Bản riêng (Sổ học tập, có Luyện tập) và bản public (BK Study Desk) chạy song song được.</summary>
internal static class AppInfo
{
#if PUBLIC_EDITION
    public const string Id = "BKStudyDesk";
    public const string Name = "BK Study Desk";
    public static bool Practice => false;
#else
    public const string Id = "HCMUT.SoHocTap";
    public const string Name = "Sổ học tập";
    public static bool Practice => true;
#endif

    // Credit: giữ nguyên khi fork hoặc build lại (MIT yêu cầu giữ thông báo bản quyền). Mục Giới thiệu trong Cài đặt đọc từ đây.
    public const string Author = "xeroz369";
    public const string Repo = "https://github.com/xeroz369/bk-study-desk";
    public const string Issues = Repo + "/issues";
    public const string License = "MIT";
    /// <summary>Link ủng hộ (để trống thì không hiện nút). Thêm sau khi có trang Ủng hộ.</summary>
    public const string Support = "";

    /// <summary>Version của bản build (bỏ phần "+commit" mà SDK tự gắn).</summary>
    public static string Version { get; } =
        (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(AppInfo).Assembly)
            ?.InformationalVersion ?? "").Split('+')[0];
}
