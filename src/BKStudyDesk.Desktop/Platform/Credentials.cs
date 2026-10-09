using System.Diagnostics;
using System.Text.Json;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace BKStudyDesk.Desktop.Platform;

/// <summary>
/// Tài khoản HCMUT cho tự đăng nhập lại (sso.autoLogin, mặc định tắt), lưu trong kho mật khẩu của hệ điều hành, không trong thư mục data:
/// Windows Credential Manager (SsoCredentials, cùng 1.x), Linux libsecret qua secret-tool (mật khẩu đi qua stdin, không lên dòng lệnh).
/// macOS chưa có (CLI security đòi gõ ở terminal): Supported = false, Cài đặt ẩn mục này. Tắt tính năng, tắt Ghi nhớ đăng nhập hay
/// đăng xuất thì xóa.
/// </summary>
internal static class Credentials
{
    private static readonly string[] Attrs = ["app", AppInfo.Id, "target", AppInfo.InstanceKey + "/sso"];

    /// <summary>Máy có kho mật khẩu dùng được. Dò một lần mỗi lần chạy app (Linux chạy secret-tool; dải báo đọc ở mỗi lần đổi trạng thái).</summary>
    public static bool Supported => _supported.Value;
    private static readonly Lazy<bool> _supported = new(() => OperatingSystem.IsWindows() || (OperatingSystem.IsLinux() && Run("secret-tool", null, ["--version"]) is not null));

    /// <summary>Được tự đăng nhập không: người dùng đã bật, đang ghi nhớ đăng nhập, và có tài khoản đã lưu.</summary>
    public static (string User, string Password)? Usable() =>
        Settings.Sso.AutoLogin && Settings.Sso.RememberDays > 0 ? Read() : null;

    public static void Save(string user, string password)
    {
        if (OperatingSystem.IsWindows()) SsoCredentials.Save(user, password);
        else if (Run("secret-tool", JsonSerializer.Serialize(new[] { user, password }), ["store", "--label", AppInfo.Name, .. Attrs]) is null)
            throw new IOException("Không lưu được vào kho mật khẩu (secret-tool).");
    }

    /// <summary>Lưu tài khoản rồi bật sso.autoLogin (form ở trang Cài đặt). Trả câu lỗi để hiện, null là đã bật.</summary>
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
        try { return JsonSerializer.Deserialize<string[]>(json) is [var u, var p] ? (u, p) : null; }
        catch (JsonException) { return null; }
    }

    /// <summary>Tắt tự đăng nhập và xóa tài khoản đã lưu.</summary>
    public static void Forget()
    {
        if (OperatingSystem.IsWindows()) { SsoCredentials.Forget(); return; }
        Settings.Sso.AutoLogin = false;
        Run("secret-tool", null, ["clear", .. Attrs]);
    }

    /// <summary>Chạy lệnh, ghi <paramref name="stdin"/> nếu có; trả stdout, null khi không chạy được hay lỗi.</summary>
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
