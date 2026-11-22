using System.Diagnostics;
using System.Text.Json;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace BKStudyDesk.Desktop.Platform;

internal static class Credentials
{
    private static readonly string[] Attrs = ["app", AppInfo.Id, "target", AppInfo.InstanceKey + "/sso"];

    public static bool Supported => _supported.Value;
    private static readonly Lazy<bool> _supported = new(() => OperatingSystem.IsWindows() || (OperatingSystem.IsLinux() && Run("secret-tool", null, ["--version"]) is not null));

    public static (string User, string Password)? Usable() =>
        Settings.Sso.AutoLogin && Settings.Sso.RememberDays > 0 ? Read() : null;

    public static void Save(string user, string password)
    {
        if (OperatingSystem.IsWindows()) SsoCredentials.Save(user, password);
        else if (Run("secret-tool", JsonSerializer.Serialize(new[] { user, password }, DesktopJson.Default.StringArray), ["store", "--label", AppInfo.Name, .. Attrs]) is null)
            throw new IOException("Không lưu được vào kho mật khẩu (secret-tool).");
    }

    public static string? Enable(string user, string password)
    {
        if (user.Trim().Length == 0 || password.Length == 0) return SoHocTap.Ui.L.T("autologin.missing");
        try
        {
            Save(user.Trim(), password);
            Settings.Sso.AutoLogin = true;
            return null;
        }
        catch (Exception x) when (x is not OutOfMemoryException)
        {
            Log.Warn($"Tự đăng nhập lại: không lưu được tài khoản: {x.Message}");
            return SoHocTap.Ui.L.T("autologin.saveFailed");
        }
    }

    public static (string User, string Password)? Read()
    {
        if (OperatingSystem.IsWindows()) return SsoCredentials.Read();
        if (Run("secret-tool", null, ["lookup", .. Attrs]) is not { Length: > 0 } json) return null;
        try { return JsonSerializer.Deserialize(json, DesktopJson.Default.StringArray) is [var u, var p] ? (u, p) : null; }
        catch (JsonException) { return null; }
    }

    public static void Forget()
    {
        if (OperatingSystem.IsWindows()) { SsoCredentials.Forget(); return; }
        Settings.Sso.AutoLogin = false;
        Run("secret-tool", null, ["clear", .. Attrs]);
    }

    private static string? Run(string exe, string? stdin, string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardInput = stdin is not null, CreateNoWindow = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            if (stdin is not null) { p.StandardInput.Write(stdin); p.StandardInput.Close(); }
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(10_000);
            return p.ExitCode == 0 ? output : null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Debug($"Kho mật khẩu: không chạy được {exe}: {e.Message}");
            return null;
        }
    }
}
