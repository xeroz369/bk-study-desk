using System.Security;
using System.Text;

namespace SoHocTap.Web;

/// <summary>
/// Nội dung mục mở cùng hệ điều hành (thuần, có test): lệnh Run của Windows, file .desktop của Linux (XDG Autostart),
/// LaunchAgent .plist của macOS. App mở bằng --tray (chạy ở khay, không hiện cửa sổ). Việc ghi, xóa: Desktop/Platform/Autostart.cs.
/// </summary>
public static class AutostartFiles
{
    public const string TrayArg = "--tray";

    /// <summary>Giá trị của HKCU\...\Run: đường dẫn trong ngoặc kép (có dấu cách vẫn đúng) rồi --tray.</summary>
    public static string RunCommand(string exe) => $"\"{exe}\" {TrayArg}";

    /// <summary>
    /// File .desktop (Desktop Entry Specification): Exec thoát dấu ngoặc kép, \, `, $ trong đường dẫn; Name là tên app.
    /// </summary>
    public static string DesktopEntry(string exe, string name)
    {
        var quoted = "\"" + new StringBuilder(exe).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("`", "\\`").Replace("$", "\\$") + "\"";
        return $"""
            [Desktop Entry]
            Type=Application
            Name={name}
            Exec={quoted} {TrayArg}
            X-GNOME-Autostart-enabled=true
            Terminal=false

            """;
    }

    /// <summary>LaunchAgent của macOS: chạy app lúc đăng nhập (RunAtLoad), đường dẫn thoát ký tự XML.</summary>
    public static string LaunchAgent(string label, string exe) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
          <key>Label</key><string>{SecurityElement.Escape(label)}</string>
          <key>ProgramArguments</key><array><string>{SecurityElement.Escape(exe)}</string><string>{TrayArg}</string></array>
          <key>RunAtLoad</key><true/>
        </dict>
        </plist>

        """;
}
