namespace SoHocTap.Core;

/// <summary>
/// Các thư mục gốc. Thư mục app là folder chứa cả <c>ui/</c> lẫn <c>data/</c>;
/// dò ngược lên từ chỗ đặt exe để chạy được cả bản build (bin/…) lẫn bản release (app/).
/// </summary>
public static class Paths
{
    private static readonly string[] AppDirs = ["data", "app", "ui", "src", "lang"];
    public static string AppRoot { get; } = FindAppRoot();
    /// <summary>
    /// Thư mục gốc chứa tài liệu (folders.subjects… nằm trong đây). Lấy từ config folders.root (path tuyệt đối, dùng được %USERPROFILE%);
    /// để trống thì dùng luôn thư mục app.
    /// </summary>
    public static string StudyRoot
    {
        get
        {
            var root = Environment.ExpandEnvironmentVariables(Config.Str("folders.root")).Trim();
            if (root.Length > 0 && Path.IsPathRooted(root)) return Path.GetFullPath(root);
            // Bản Store: folder cài đặt read-only và bị ẩn, nên default là folder gợi ý (user chọn lại ở lần mở đầu).
            return AppPackage.IsPackaged ? SuggestedRoot : AppRoot;
        }
    }

    /// <summary>
    /// Folder gợi ý để lưu tài liệu: ổ cứng khác ổ Windows còn trống nhiều nhất (từ 5 GB), không có thì Documents.
    /// Tính một lần mỗi phiên để không đổi giữa chừng.
    /// </summary>
    public static string SuggestedRoot => _suggested.Value;

    private static readonly Lazy<string> _suggested = new(() =>
    {
        try
        {
            var system = Path.GetPathRoot(Environment.SystemDirectory);
            var drive = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady && !d.Name.Equals(system, StringComparison.OrdinalIgnoreCase)
                            && d.AvailableFreeSpace >= 5L * 1024 * 1024 * 1024)
                .OrderByDescending(d => d.AvailableFreeSpace).FirstOrDefault();
            if (drive is not null) return Path.Combine(drive.RootDirectory.FullName, AppPackage.Product);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppPackage.Product);
    });
    public static string Ui => Path.Combine(AppRoot, "ui");
    public static string Content => Path.Combine(AppRoot, "content");
    public static string Data => Path.Combine(AppRoot, "data");
    public static string Secrets => Path.Combine(Data, "secrets");
    public static string WebViewProfile => Path.Combine(Data, "webview");

    public static string DataFile(string name) => Path.Combine(Data, name);

    /// <summary>
    /// Thư mục app: folder gần nhất (đi ngược lên từ exe) có data\. Nếu chưa có (lần chạy đầu của bản release) thì lấy folder
    /// cha của app\ (khi exe nằm trong app\), không thì lấy luôn folder chứa exe, rồi tạo data\.
    /// </summary>
    private static string FindAppRoot()
    {
        // Bản Store (MSIX): không ghi được vào folder cài đặt, data để trong LocalAppData (gỡ app là Windows dọn luôn).
        if (AppPackage.IsPackaged)
        {
            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppPackage.Id);
            Directory.CreateDirectory(Path.Combine(local, "data"));
            return local;
        }
        var exeDir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var dir = exeDir; dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "data"))) return dir.FullName;
        var root = exeDir.Name.Equals("app", StringComparison.OrdinalIgnoreCase) && exeDir.Parent is not null ? exeDir.Parent.FullName : exeDir.FullName;
        Directory.CreateDirectory(Path.Combine(root, "data"));
        return root;
    }

    /// <summary>
    /// Path tuyệt đối của <paramref name="relative"/> trong thư mục Study.
    /// Trả null nếu path ra ngoài Study, rơi vào thư mục app (data, secrets…) hoặc không tồn tại.
    /// </summary>
    public static string? StudyPath(string? relative)
    {
        var root = Path.GetFullPath(StudyRoot);
        var full = Path.GetFullPath(Path.Combine(root, (relative ?? "").Replace('/', Path.DirectorySeparatorChar)));
        static bool Under(string path, string dir) =>
            path.Equals(dir, StringComparison.OrdinalIgnoreCase) || path.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        // Không bao giờ mở/liệt kê phần của app: data (token, cookie), exe, UI, source code.
        var app = Path.GetFullPath(AppRoot);
        bool inApp = AppDirs.Any(d => Under(full, Path.Combine(app, d)))
                     || (Under(app, root) && !root.Equals(app, StringComparison.OrdinalIgnoreCase) && Under(full, app));
        if (!Under(full, root) || inApp) return null;
        return File.Exists(full) || Directory.Exists(full) ? full : null;
    }

    /// <summary>Path tương đối so với thư mục Study, dùng dấu '\'.</summary>
    public static string RelativeToStudy(string full) => Path.GetRelativePath(StudyRoot, full);
}
