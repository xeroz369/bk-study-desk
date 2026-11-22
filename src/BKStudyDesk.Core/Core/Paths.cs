namespace SoHocTap.Core;

public static class Paths
{
    private static readonly string[] AppDirs = ["data", "app", "ui", "src", "lang"];
    public static string AppRoot { get; } = LongPath(FindAppRoot());

    public static string LongPath(string path)
    {
        if (!OperatingSystem.IsWindows()) return path;
        var buf = new char[1024];
        var n = GetLongPathName(path, buf, (uint)buf.Length);
        return n > 0 && n < buf.Length ? new string(buf, 0, (int)n) : path;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "GetLongPathNameW")]
    private static extern uint GetLongPathName(string shortPath, char[] buffer, uint size);
    public static string StudyRoot
    {
        get
        {
            var root = Environment.ExpandEnvironmentVariables(Config.Str("folders.root")).Trim();
            if (root.Length > 0 && Path.IsPathRooted(root)) return Path.GetFullPath(root);
            return Kind == InstallKind.Portable ? AppRoot : SuggestedRoot;
        }
    }

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
    public static string Ui => Bundled("ui");
    public static string Content => Bundled("content");
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

    public static InstallKind Kind { get; private set; }

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

    public static string? StudyPath(string? relative)
    {
        var root = Path.GetFullPath(StudyRoot);
        var full = Path.GetFullPath(Path.Combine(root, (relative ?? "").Replace('/', Path.DirectorySeparatorChar)));
        static bool Under(string path, string dir) =>
            path.Equals(dir, StringComparison.OrdinalIgnoreCase) || path.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        var app = Path.GetFullPath(AppRoot);
        bool inApp = AppDirs.Any(d => Under(full, Path.Combine(app, d)))
                     || (Under(app, root) && !root.Equals(app, StringComparison.OrdinalIgnoreCase) && Under(full, app));
        if (!Under(full, root) || inApp) return null;
        return File.Exists(full) || Directory.Exists(full) ? full : null;
    }

    public static string RelativeToStudy(string full) => Path.GetRelativePath(StudyRoot, full);
}
