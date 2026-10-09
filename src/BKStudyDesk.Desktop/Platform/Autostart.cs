using Microsoft.Win32;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Web;

namespace BKStudyDesk.Desktop.Platform;

/// <summary>
/// Mở app cùng hệ điều hành (chạy ở khay, --tray). Nội dung mục: AutostartFiles (lõi, có test). Mỗi hệ điều hành một chỗ:
/// Windows HKCU\...\Run (cùng tên mục với 1.x nên lên bản đa nền tảng không thành hai mục), macOS ~/Library/LaunchAgents, Linux ~/.config/autostart.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppleLabel = "vn.edu.hcmut.bkstudydesk";

    /// <summary>Đường dẫn để mở lại app: AppImage thì file .AppImage (APPIMAGE), còn lại là file chạy hiện tại.</summary>
    private static string Exe => Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } appImage ? appImage : Paths.LongPath(Environment.ProcessPath ?? "");

    private static string LinuxFile => Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } x ? x
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"), "autostart", AppInfo.Id + ".desktop");

    private static string AppleFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", AppleLabel + ".plist");

    /// <summary>App vừa được hệ điều hành mở lúc đăng nhập: chạy ở khay.</summary>
    public static bool LaunchedAtLogin => Environment.GetCommandLineArgs().Contains(AutostartFiles.TrayArg)
#if WINDOWS
        || (AppPackage.IsPackaged && Windows.StoreStartup.LaunchedAtLogin)   // bản Store: StartupTask, không có --tray
#endif
        ;

    public static bool Enabled
    {
        get
        {
#if WINDOWS
            if (AppPackage.IsPackaged) return Windows.StoreStartup.Enabled;
#endif
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using var k = Registry.CurrentUser.OpenSubKey(RunKey);
                    return k?.GetValue(AppInfo.Id) is string v && Exe.Length > 0 && v.Contains(Exe, StringComparison.OrdinalIgnoreCase);
                }
                return File.Exists(OperatingSystem.IsMacOS() ? AppleFile : LinuxFile);
            }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException) { return false; }
        }
    }

    /// <summary>Bật, tắt; trả trạng thái thật sau khi đổi.</summary>
    public static bool Set(bool on)
    {
#if WINDOWS
        if (AppPackage.IsPackaged) return Windows.StoreStartup.Set(on);
#endif
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var k = Registry.CurrentUser.CreateSubKey(RunKey);
                if (on) k.SetValue(AppInfo.Id, AutostartFiles.RunCommand(Exe));
                else if (k.GetValue(AppInfo.Id) is not null) k.DeleteValue(AppInfo.Id);
            }
            else
            {
                var (file, text) = OperatingSystem.IsMacOS()
                    ? (AppleFile, AutostartFiles.LaunchAgent(AppleLabel, Exe))
                    : (LinuxFile, AutostartFiles.DesktopEntry(Exe, AppInfo.Name));
                if (on)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                    File.WriteAllText(file, text);
                }
                else File.Delete(file);
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Log.Error("Mở cùng hệ điều hành", e);
        }
        return Enabled;
    }
}
