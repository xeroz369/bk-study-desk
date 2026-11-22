namespace SoHocTap.Core;

public enum InstallKind { Store, Installed, Portable }

public static class AppDirs
{
    public static (string Root, InstallKind Kind) Resolve(string exeDir, bool packaged, string localAppData, string packageId,
        Func<string, bool> dirExists, Func<string, bool> fileExists, bool appImage = false)
    {
        exeDir = Path.TrimEndingDirectorySeparator(exeDir);
        if ((packaged || appImage) && !Path.IsPathRooted(localAppData)) throw new ArgumentException("Thư mục dữ liệu người dùng không hợp lệ", nameof(localAppData));
        if (packaged) return (Path.Combine(localAppData, packageId), InstallKind.Store);
        if (appImage) return (Path.Combine(localAppData, packageId), InstallKind.Installed);
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

    public static bool IsVelopackInstall(string exeDir, Func<string, bool> fileExists)
    {
        exeDir = Path.TrimEndingDirectorySeparator(exeDir);
        var parent = Path.GetDirectoryName(exeDir);
        return parent is not null && Path.GetFileName(exeDir).Equals("current", StringComparison.OrdinalIgnoreCase)
            && fileExists(Path.Combine(parent, "Update.exe"));
    }
}
