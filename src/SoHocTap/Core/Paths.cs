namespace SoHocTap.Core;

/// <summary>
/// Các thư mục gốc. Thư mục app là folder chứa cả <c>ui/</c> lẫn <c>data/</c>;
/// dò ngược lên từ chỗ đặt exe để chạy được cả bản build (bin/…) lẫn bản release (app/).
/// </summary>
public static class Paths
{
    private static readonly string[] AppDirs = ["data", "app", "ui", "src", "lang"];
    public static string AppRoot { get; } = LongPath(FindAppRoot());

    /// <summary>Đường dẫn dài (C:\PROGRA~1\App → C:\Program Files\App): lối tắt 8.3 và đường dẫn thường phải ra cùng một chuỗi.</summary>
    public static string LongPath(string path)
    {
        if (!OperatingSystem.IsWindows()) return path;   // tên 8.3 chỉ có trên Windows
        var buf = new char[1024];
        var n = GetLongPathName(path, buf, (uint)buf.Length);
        return n > 0 && n < buf.Length ? new string(buf, 0, (int)n) : path;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "GetLongPathNameW")]
    private static extern uint GetLongPathName(string shortPath, char[] buffer, uint size);
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
            // Bản Store và bản cài: thư mục app bị ẩn và bị xóa khi gỡ, nên default là folder gợi ý (user chọn lại ở lần mở đầu).
            return Kind == InstallKind.Portable ? AppRoot : SuggestedRoot;
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
    /// <summary>Khung Luyện tập đã build: ui\ ở gốc app (bản riêng, dev), không có thì bản đi kèm exe (bản public, Store).</summary>
    public static string Ui => Bundled("ui");
    /// <summary>Bài học có sẵn: content\ ở gốc app, không có thì bản (trống) đi kèm exe.</summary>
    public static string Content => Bundled("content");
    /// <summary>Quiz tự soạn và đã nhập (app ghi vào): content\packs ở gốc app, không thì data\packs (thư mục cài của bản Store chỉ đọc).</summary>
    public static string Packs => Directory.Exists(Path.Combine(AppRoot, "content")) ? Path.Combine(AppRoot, "content", "packs") : Path.Combine(Data, "packs");

    private static string Bundled(string name)
    {
        var here = Path.Combine(AppRoot, name);
        return Directory.Exists(here) ? here : Path.Combine(AppContext.BaseDirectory, name);
    }
    public static string Data => Path.Combine(AppRoot, "data");
    public static string Secrets => Path.Combine(Data, "secrets");
    public static string WebViewProfile => Path.Combine(Data, "webview");

    public static string DataFile(string name) => Path.Combine(Data, name);

    /// <summary>Kiểu bản đang chạy (Store, bản cài Velopack, zip/local), xác định cùng lúc với AppRoot.</summary>
    public static InstallKind Kind { get; private set; }

    /// <summary>
    /// Thư mục app (nơi có data\), xem AppDirs.Resolve:
    /// - bản Store: LocalAppData\&lt;id gói&gt; (gỡ app là Windows dọn luôn);
    /// - bản cài Velopack: thư mục cài, cha của current\ (current\ bị thay mỗi lần cập nhật); AppImage: thư mục dữ liệu người dùng;
    /// - bản zip/local: folder gần nhất (đi ngược từ exe) có data\, chưa có thì cha của app\ hoặc chính folder exe.
    /// </summary>
    private static string FindAppRoot()
    {
        var (root, kind) = global::SoHocTap.Core.AppDirs.Resolve(AppContext.BaseDirectory, AppPackage.IsPackaged,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
            AppPackage.IsPackaged ? AppPackage.Id : InstalledPackId() ?? "BKStudyDesk",
            Directory.Exists, File.Exists, Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 });
        Kind = kind;
        Directory.CreateDirectory(Path.Combine(root, "data"));
        return root;
    }

    /// <summary>Pack id của bản cài Velopack (thẻ &lt;id&gt; trong current\sq.version), null nếu không phải bản cài.</summary>
    private static string? InstalledPackId()
    {
        try
        {
            var f = Path.Combine(AppContext.BaseDirectory, "sq.version");
            var m = File.Exists(f) ? System.Text.RegularExpressions.Regex.Match(File.ReadAllText(f), "<id>([A-Za-z0-9._-]+)</id>") : null;
            return m is { Success: true } ? m.Groups[1].Value : null;
        }
        catch (IOException) { return null; }
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
