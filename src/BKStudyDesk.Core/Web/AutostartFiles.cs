using System.Security;
using System.Text;

namespace SoHocTap.Web;

public static class AutostartFiles
{
    public const string TrayArg = "--tray";

    public static string RunCommand(string exe) => $"\"{exe}\" {TrayArg}";

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
