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

    private static string ExePath => Paths.LongPath(Environment.ProcessPath ?? "");
    private static string Command => $"\"{ExePath}\" {TrayArg}";

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
            return k?.GetValue(AppInfo.Id) is string v && v.Contains(ExePath.Length > 0 ? ExePath : "\0", StringComparison.OrdinalIgnoreCase);
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

    /// <summary>Gỡ app (hook của Velopack): xóa mục Run nếu nó trỏ tới chính exe này, không đụng bản khác cùng tên.</summary>
    public static void Remove()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (k?.GetValue(AppInfo.Id) is string v && ExePath.Length > 0 && v.Contains(ExePath, StringComparison.OrdinalIgnoreCase))
                k.DeleteValue(AppInfo.Id);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
    }

    /// <summary>
    /// App bị dời chỗ (vd. giải nén ra folder khác) thì cập nhật path trong Run, nếu đang bật. Chỉ khi exe cũ không còn:
    /// exe cũ vẫn còn là một bản khác đang dùng (bản demo, bản thử) thì không đụng vào. Bản Store không cần.
    /// </summary>
    public static void Refresh()
    {
        if (AppPackage.IsPackaged) return;
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        if (k?.GetValue(AppInfo.Id) is not string v || v.Equals(Command, StringComparison.OrdinalIgnoreCase)) return;
        var old = v.StartsWith('"') ? v[1..Math.Max(1, v.IndexOf('"', 1))] : v.Split(' ')[0];
        if (!File.Exists(old)) Set(true);
    }
}
