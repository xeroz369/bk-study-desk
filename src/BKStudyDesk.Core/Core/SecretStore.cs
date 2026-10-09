using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json.Nodes;

namespace SoHocTap.Core;

/// <summary>
/// Lưu secret (token LMS) và khóa ACL thư mục data.
/// <list type="bullet">
/// <item>Secret được encrypt bằng DPAPI theo user hiện tại: chép file sang máy khác hay tài khoản Windows khác thì không decrypt được.</item>
/// <item>Thư mục data\ chỉ cho tài khoản Windows đang dùng, SYSTEM và Administrators (bỏ quyền kế thừa kiểu Everyone của ổ đĩa).</item>
/// </list>
/// Giới hạn: malware chạy dưới chính tài khoản này vẫn đọc được (app Windows nào cũng vậy);
/// class này chỉ chặn user khác trên máy, bản backup, file bị chép đi. Logout trong Cài đặt sẽ xóa sạch session.
/// </summary>
public static class SecretStore
{
    private static readonly byte[] Entropy = "HCMUT.StudyDesk.secret.v1"u8.ToArray();

    /// <summary>Đọc secret. File cũ còn là JSON thường thì đọc xong encrypt lại luôn. Không decrypt được (máy/tài khoản khác) thì trả null.</summary>
    public static JsonObject? Read(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > 0 && bytes[0] == (byte)'{')
            {
                var plain = JsonNode.Parse(bytes) as JsonObject;
                if (plain is not null && OperatingSystem.IsWindows()) Write(path, plain);   // encrypt lại file cũ
                return plain;
            }
            if (!OperatingSystem.IsWindows()) return null;
            var data = ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return JsonNode.Parse(data) as JsonObject;
        }
        catch (Exception e) when (e is CryptographicException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Log.Warn($"Không đọc được {Path.GetFileName(path)}: {e.Message}");
            return null;
        }
    }

    public static void Write(string path, JsonObject value)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var json = Encoding.UTF8.GetBytes(value.ToJsonString());
        // Linux/macOS (bản đa nền tảng, đang làm): tạm lưu JSON với quyền 600; sẽ chuyển sang Keychain / libsecret (PLAN-DA-NEN-TANG.md).
        var bytes = OperatingSystem.IsWindows() ? ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser) : json;
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Chỉ cho tài khoản hiện tại (+ SYSTEM, Administrators) vào thư mục; folder con và file kế thừa ACL này.
    /// ACL đã đúng thì thôi. Ổ không hỗ trợ ACL (FAT, USB) thì bỏ qua.
    /// </summary>
    public static void Lockdown(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);   // chmod 700
                return;
            }
            var info = new DirectoryInfo(dir);
            var me = WindowsIdentity.GetCurrent().User!;
            var sec = info.GetAccessControl();
            if (sec.AreAccessRulesProtected && Allowed(sec).SetEquals(Expected(me))) return;
            var fresh = new DirectorySecurity();
            fresh.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            foreach (var sid in Expected(me))
                fresh.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            info.SetAccessControl(fresh);
            Log.Info($"Đã khóa ACL thư mục cho tài khoản hiện tại: {dir}");
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or PlatformNotSupportedException or InvalidOperationException
                                       or PrivilegeNotHeldException)
        {
            Log.Warn($"Không khóa ACL được cho thư mục {dir}: {e.Message}");
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static HashSet<SecurityIdentifier> Expected(SecurityIdentifier me) =>
        [me, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)];

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static HashSet<SecurityIdentifier> Allowed(DirectorySecurity sec) =>
        sec.GetAccessRules(true, false, typeof(SecurityIdentifier)).OfType<FileSystemAccessRule>()
            .Where(r => r.AccessControlType == AccessControlType.Allow).Select(r => (SecurityIdentifier)r.IdentityReference).ToHashSet();
}
