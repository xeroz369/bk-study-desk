using System.ComponentModel;
using System.Runtime.InteropServices;
using SoHocTap.Core;

namespace SoHocTap.Shell;

/// <summary>
/// Tài khoản HCMUT cho tự đăng nhập lại (sso.autoLogin), lưu trong Windows Credential Manager của tài khoản Windows đang dùng,
/// không nằm trong thư mục data (không bị chép theo khi sao lưu hay chuyển dữ liệu). Tắt tính năng hay đăng xuất thì xóa.
/// </summary>
internal static class SsoCredentials
{
    // Mỗi thư mục app một mục riêng (như khóa chống mở hai lần): bản demo, bản zip không đọc hay xóa tài khoản của bản cài.
    private static string Target => AppInfo.InstanceKey + "/sso";
    private const int Generic = 1, LocalMachine = 2, NotFound = 1168;

    /// <summary>Được tự đăng nhập không: người dùng đã bật, đang ghi nhớ đăng nhập, và có tài khoản đã lưu.</summary>
    public static (string User, string Password)? Usable() =>
        Settings.Sso.AutoLogin && Settings.Sso.RememberDays > 0 ? Read() : null;

    public static void Save(string user, string password)
    {
        var blob = Marshal.StringToHGlobalUni(password);
        try
        {
            var c = new Credential { Type = Generic, TargetName = Target, UserName = user, CredentialBlob = blob, CredentialBlobSize = password.Length * 2, Persist = LocalMachine };
            if (!CredWrite(ref c, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.ZeroFreeGlobalAllocUnicode(blob); }
    }

    public static (string User, string Password)? Read()
    {
        if (!CredRead(Target, Generic, 0, out var p)) return null;
        try
        {
            var c = Marshal.PtrToStructure<Credential>(p);
            return (c.UserName ?? "", Marshal.PtrToStringUni(c.CredentialBlob, c.CredentialBlobSize / 2) ?? "");
        }
        finally { CredFree(p); }
    }

    /// <summary>Tắt tự đăng nhập và xóa tài khoản đã lưu (người dùng tắt, tắt Ghi nhớ đăng nhập, hay đăng xuất).</summary>
    public static void Forget()
    {
        Settings.Sso.AutoLogin = false;
        Delete();
    }

    private static void Delete()
    {
        if (!CredDelete(Target, Generic, 0) && Marshal.GetLastWin32Error() != NotFound)
            Log.Warn($"Không xóa được tài khoản đã lưu: lỗi {Marshal.GetLastWin32Error()}");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
