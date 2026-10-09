namespace SoHocTap.Core;

/// <summary>Kiểu bản đang chạy: Store (MSIX), bản cài (Velopack: Setup.exe / AppImage) hay bản zip/local (portable).</summary>
public enum InstallKind { Store, Installed, Portable }

/// <summary>
/// Tìm thư mục gốc của app (nơi có data\). Hàm thuần, không đụng đĩa: kiểm tra file/thư mục qua delegate để test được.
/// Bản cài Velopack thay cả thư mục current\ mỗi lần cập nhật và Setup.exe cài đè xóa cả thư mục cài, nên data\ nằm ở
/// LocalAppData\&lt;pack id&gt;.Data, ngoài thư mục cài (người dùng chọn được thư mục cài).
/// </summary>
public static class AppDirs
{
    /// <param name="exeDir">thư mục chứa exe (AppContext.BaseDirectory)</param>
    /// <param name="packaged">đang chạy dạng MSIX (Store)</param>
    /// <param name="localAppData">%LOCALAPPDATA% trên Windows, $XDG_DATA_HOME trên Linux</param>
    /// <param name="appImage">đang chạy từ AppImage (biến môi trường APPIMAGE)</param>
    public static (string Root, InstallKind Kind) Resolve(string exeDir, bool packaged, string localAppData, string packageId,
        Func<string, bool> dirExists, Func<string, bool> fileExists, bool appImage = false)
    {
        exeDir = Path.TrimEndingDirectorySeparator(exeDir);
        // GetFolderPath trả "" khi thư mục chưa có (Linux mới cài, chưa có ~/.local/share): không để data rơi vào thư mục đang đứng.
        if ((packaged || appImage) && !Path.IsPathRooted(localAppData)) throw new ArgumentException("Thư mục dữ liệu người dùng không hợp lệ", nameof(localAppData));
        if (packaged) return (Path.Combine(localAppData, packageId), InstallKind.Store);
        // AppImage chạy từ thư mục mount tạm (/tmp/.mount_…): data để ở thư mục dữ liệu của người dùng.
        if (appImage) return (Path.Combine(localAppData, packageId), InstallKind.Installed);
        // Bản cài: data ở LocalAppData\<pack id>.Data, dù người dùng cài app vào thư mục nào. Không để trong thư mục cài, vì
        // Setup.exe cài đè (cài lại, sửa lỗi cài đặt) xóa cả thư mục cài; LocalAppData thì riêng tư theo tài khoản.
        // Gỡ app thì hook gỡ của Velopack xóa thư mục data này.
        if (IsVelopackInstall(exeDir, fileExists))
        {
            if (!Path.IsPathRooted(localAppData)) throw new ArgumentException("Thư mục dữ liệu người dùng không hợp lệ", nameof(localAppData));
            return (Path.Combine(localAppData, packageId + ".Data"), InstallKind.Installed);
        }

        for (var dir = exeDir; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            if (dirExists(Path.Combine(dir, "data"))) return (dir, InstallKind.Portable);
        var parent = Path.GetDirectoryName(exeDir);
        var root = Path.GetFileName(exeDir).Equals("app", StringComparison.OrdinalIgnoreCase) && parent is not null ? parent : exeDir;
        return (root, InstallKind.Portable);
    }

    /// <summary>Bản cài Velopack trên Windows: exe nằm trong current\ và thư mục cha có Update.exe.</summary>
    public static bool IsVelopackInstall(string exeDir, Func<string, bool> fileExists)
    {
        exeDir = Path.TrimEndingDirectorySeparator(exeDir);
        var parent = Path.GetDirectoryName(exeDir);
        return parent is not null && Path.GetFileName(exeDir).Equals("current", StringComparison.OrdinalIgnoreCase)
            && fileExists(Path.Combine(parent, "Update.exe"));
    }
}
