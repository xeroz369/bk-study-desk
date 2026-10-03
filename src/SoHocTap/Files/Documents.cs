using System.Diagnostics;
using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Files;

/// <summary>Đọc cây tài liệu trên máy cho UI (chỉ đọc đĩa, không gửi request nào ra mạng).</summary>
public static class Documents
{
    /// <summary>Liệt kê các môn trong thư mục Môn học, kèm số file và lần sửa gần nhất.</summary>
    public static JsonArray ListSubjects()
    {
        var list = new JsonArray();
        if (!Directory.Exists(Organizer.SubjectsRoot)) return list;
        foreach (var d in Directory.GetDirectories(Organizer.SubjectsRoot).Order())
        {
            var files = Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)).Select(f => new FileInfo(f)).ToList();
            list.Add(new JsonObject
            {
                ["name"] = Path.GetFileName(d),
                ["files"] = files.Count,
                ["modified"] = files.Count == 0 ? 0 : files.Max(f => new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeSeconds()),
            });
        }
        return list;
    }

    /// <summary>File của một môn, mới nhất lên đầu (tối đa <paramref name="limit"/>), kèm các folder cấp một.</summary>
    public static JsonObject SubjectFiles(string name, int limit = 400)
    {
        var root = Path.Combine(Organizer.SubjectsRoot, Path.GetFileName(name));
        var result = new JsonObject { ["name"] = name, ["folders"] = new JsonArray(), ["files"] = new JsonArray(), ["total"] = 0 };
        if (!Directory.Exists(root)) return result;
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
            .Select(f => new FileInfo(f)).OrderByDescending(f => f.LastWriteTimeUtc).ToList();
        result["total"] = files.Count;
        result["folders"] = new JsonArray(Directory.GetDirectories(root).Select(Path.GetFileName).Order().Select(x => (JsonNode)x!).ToArray());
        result["files"] = new JsonArray(files.Take(limit).Select(f => (JsonNode)new JsonObject
        {
            ["name"] = f.Name,
            ["path"] = Paths.RelativeToStudy(f.FullName),
            ["folder"] = Path.GetRelativePath(root, f.DirectoryName!),
            ["size"] = f.Length,
            ["modified"] = new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeSeconds(),
        }).ToArray());
        return result;
    }

    // ------------------------------------------------------------------ bản async (không chặn UI thread), có cache

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Stamp, JsonNode Value)> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <see cref="ListSubjects"/> chạy trên thread pool. Thư mục môn không đổi (mtime của mọi thư mục con như cũ) thì trả kết quả
    /// đã quét, không đi lại cả cây. Trả bản sao: người gọi sửa thoải mái.
    /// </summary>
    public static Task<JsonArray> ListSubjectsAsync(CancellationToken ct = default) =>
        Task.Run(() => (JsonArray)Cached("subjects", Organizer.SubjectsRoot, ListSubjects, ct), ct);

    /// <summary><see cref="SubjectFiles"/> chạy trên thread pool, có cache theo mtime các thư mục của môn.</summary>
    public static Task<JsonObject> SubjectFilesAsync(string name, int limit = 400, CancellationToken ct = default) =>
        Task.Run(() => (JsonObject)Cached($"files|{limit}|{Path.GetFileName(name)}", Path.Combine(Organizer.SubjectsRoot, Path.GetFileName(name)),
            () => SubjectFiles(name, limit), ct), ct);

    private static JsonNode Cached(string key, string root, Func<JsonNode> scan, CancellationToken ct)
    {
        var stamp = TreeStamp(root, ct);
        if (Cache.TryGetValue(key, out var hit) && hit.Stamp == stamp) return hit.Value.DeepClone();
        var value = scan();
        Cache[key] = (stamp, value.DeepClone());
        return value;
    }

    /// <summary>
    /// Dấu của một cây thư mục: số thư mục và mtime lớn nhất của chúng. Thêm, xóa, đổi tên file ở đâu trong cây cũng làm mtime
    /// của thư mục chứa nó đổi; chỉ đi qua thư mục (ít hơn file rất nhiều) nên rẻ hơn quét lại.
    /// </summary>
    private static string TreeStamp(string root, CancellationToken ct)
    {
        if (!Directory.Exists(root)) return "none";
        var max = Directory.GetLastWriteTimeUtc(root).Ticks;
        var count = 0;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var d in new DirectoryInfo(root).EnumerateDirectories("*", options))
        {
            ct.ThrowIfCancellationRequested();
            count++;
            max = Math.Max(max, d.LastWriteTimeUtc.Ticks);
        }
        return $"{count}:{max}";
    }

    /// <summary>Mở tài liệu (path trong Study) bằng app ngoài; PDF thì dùng viewer.pdfApp nếu có. Nếu là folder thì mở Explorer.</summary>
    public static bool Open(string? rel)
    {
        var full = Paths.StudyPath(rel);
        if (full is null) return false;
        if (Directory.Exists(full)) { Process.Start(new ProcessStartInfo(Explorer) { ArgumentList = { full } }); return true; }
        // File chạy được (exe, lnk, hta, script…) trong tài liệu tải về: không chạy, chỉ chỉ ra trong Explorer cho người dùng tự quyết.
        if (Runnable.Contains(Path.GetExtension(full))) return Reveal(rel);
        var pdfApp = Environment.ExpandEnvironmentVariables(Config.Str("viewer.pdfApp"));
        if (full.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && pdfApp.Length > 0 && File.Exists(pdfApp))
            Process.Start(new ProcessStartInfo(pdfApp) { ArgumentList = { full }, UseShellExecute = false });
        else
            Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
        return true;
    }

    /// <summary>
    /// Tìm file nguồn của bài học ("Bài giảng/pt_phi_tuyen", "BKeL/GK251.pdf") trong thư mục môn.
    /// Khớp đường dẫn trước, rồi tới tên file (không cần đuôi); ưu tiên PDF. Trả về path trong Study hoặc null.
    /// </summary>
    public static string? FindSource(string? subject, string? file)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(file)) return null;
        var root = Path.Combine(Organizer.SubjectsRoot, Path.GetFileName(subject));
        if (!Directory.Exists(root)) return null;
        static string Norm(string s) => s.Normalize().Replace('\\', '/').Trim().ToLowerInvariant();
        var want = Norm(file);
        var hasExt = DocExt.Contains(Path.GetExtension(file));   // "Chương 1. Sai số": dấu chấm trong tên, không phải đuôi file
        var wantStem = hasExt ? Norm(Path.GetFileNameWithoutExtension(file)) : Norm(Path.GetFileName(file));
        static int Rank(string f) => Path.GetExtension(f).ToLowerInvariant() switch { ".pdf" => 0, ".pptx" or ".ppt" => 1, _ => 2 };
        var hits = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => DocExt.Contains(Path.GetExtension(f)))   // chỉ tài liệu: gói chia sẻ không được mở exe, bat, lnk…
            .Select(f => (full: f, rel: Norm(Path.GetRelativePath(root, f))))
            .Select(x => (x.full, score:
                x.rel == want || (!hasExt && Path.ChangeExtension(x.rel, null) == want) ? 0
                : x.rel.EndsWith("/" + want, StringComparison.Ordinal) || (!hasExt && Path.ChangeExtension(x.rel, null).EndsWith("/" + want, StringComparison.Ordinal)) ? 1
                : Norm(Path.GetFileNameWithoutExtension(x.full)) == wantStem ? 2 : -1))
            .Where(x => x.score >= 0)
            .OrderBy(x => x.score).ThenBy(x => Rank(x.full)).ThenBy(x => x.full.Length);
        var best = hits.FirstOrDefault().full;
        return best is null ? null : Paths.RelativeToStudy(best);
    }

    /// <summary>Đuôi file "Mở slide" được mở: tài liệu, không phải file chạy được.</summary>
    private static readonly HashSet<string> DocExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".ppt", ".pptx", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".md", ".png", ".jpg", ".jpeg", ".webp",
    };

    /// <summary>Mở PDF ở trang <paramref name="page"/>: SumatraPDF (viewer.pdfApp) dùng -page, không có thì Edge với #page=.</summary>
    public static bool Open(string? rel, int page)
    {
        var full = Paths.StudyPath(rel);
        if (full is null || !File.Exists(full)) return false;
        if (page <= 1 || !full.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return Open(rel);
        var pdfApp = Environment.ExpandEnvironmentVariables(Config.Str("viewer.pdfApp"));
        if (pdfApp.Length > 0 && File.Exists(pdfApp))
            Process.Start(new ProcessStartInfo(pdfApp) { ArgumentList = { "-page", page.ToString(), full }, UseShellExecute = false });
        else
            Process.Start(new ProcessStartInfo("msedge.exe", new Uri(full).AbsoluteUri + "#page=" + page) { UseShellExecute = true });
        return true;
    }

    /// <summary>Hiện file/thư mục trong File Explorer.</summary>
    public static bool Reveal(string? rel)
    {
        var full = Paths.StudyPath(rel);
        if (full is null) return false;
        Process.Start(new ProcessStartInfo(Explorer) { ArgumentList = { "/select,", full } });
        return true;
    }

    /// <summary>Đường dẫn đầy đủ tới explorer.exe, để không bị một explorer.exe giả đặt cạnh app chạy thay.</summary>
    private static string Explorer => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    /// <summary>Đuôi file Windows chạy được hoặc tự thực thi khi mở: không mở trực tiếp từ app.</summary>
    private static readonly HashSet<string> Runnable = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".scr", ".pif", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta",
        ".lnk", ".url", ".msi", ".msp", ".cpl", ".msc", ".jar", ".reg", ".scf", ".chm", ".application", ".appref-ms",
        ".settingcontent-ms", ".library-ms", ".iso", ".img", ".vhd", ".vhdx", ".xll", ".diagcab", ".appx", ".msix", ".appinstaller",
        ".docm", ".dotm", ".xlsm", ".xltm", ".xlam", ".pptm", ".potm", ".ppam", ".website", ".mht", ".mhtml",
    };

    /// <summary>Liệt kê đúng một tầng: folder con (kèm số mục bên trong) và file. Không quét cả cây.</summary>
    public static JsonObject ListDir(string rel)
    {
        var full = Paths.StudyPath(rel);
        if (full is null || !Directory.Exists(full)) return new JsonObject { ["path"] = rel, ["dirs"] = new JsonArray(), ["files"] = new JsonArray(), ["missing"] = true };
        bool Visible(string n) => !n.StartsWith('.') && !n.EndsWith(".part", StringComparison.OrdinalIgnoreCase) && !n.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase);
        var dirs = new DirectoryInfo(full).GetDirectories().Where(d => Visible(d.Name)).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(d => (JsonNode)new JsonObject { ["name"] = d.Name, ["count"] = d.EnumerateFileSystemInfos().Count(x => Visible(x.Name)) });
        var files = new DirectoryInfo(full).GetFiles().Where(f => Visible(f.Name)).OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(f => (JsonNode)new JsonObject
            {
                ["name"] = f.Name,
                ["size"] = f.Length,
                ["modified"] = new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeSeconds(),
                ["ext"] = f.Extension.ToLowerInvariant(),
            });
        return new JsonObject { ["path"] = Paths.RelativeToStudy(full).Replace('\\', '/'), ["dirs"] = new JsonArray(dirs.ToArray()), ["files"] = new JsonArray(files.ToArray()) };
    }
}
