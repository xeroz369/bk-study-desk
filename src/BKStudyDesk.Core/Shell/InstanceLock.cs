using SoHocTap.Core;

namespace SoHocTap.Shell;

/// <summary>
/// Tên khóa chạy một bản (Mutex, named pipe của Platform/SingleInstance, kiểm bản đang chạy của UpdateService). Mặc định một bản cho
/// mỗi người dùng dù cài ở thư mục nào: bản cài GitHub, bản Store, bản zip cũ chạy cùng lúc thì đồng bộ hai lần và báo trùng (11/10/2026).
/// Đặt biến môi trường BKSD_INSTANCE (bản demo, bản thử, công cụ chụp) thì khóa theo thư mục app như trước, chạy song song được.
/// Không phải AppInfo.InstanceKey: khóa đó còn là tên chỗ lưu mật khẩu tự đăng nhập, đổi là mất mật khẩu đã lưu.
/// </summary>
public static class InstanceLock
{
    public static string Key(string? isolate, string appRoot) =>
        string.IsNullOrWhiteSpace(isolate) ? AppInfo.Id + ".app"
            : AppInfo.Id + "." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(appRoot.ToLowerInvariant())))[..8];

    /// <summary>Khóa của lần chạy này.</summary>
    public static string Current { get; } = Key(Environment.GetEnvironmentVariable("BKSD_INSTANCE"), Paths.AppRoot);
}
