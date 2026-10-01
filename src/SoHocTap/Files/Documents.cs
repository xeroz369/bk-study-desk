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

    /// <summary>Mở tài liệu (path trong Study) bằng app ngoài; PDF thì dùng viewer.pdfApp nếu có. Nếu là folder thì mở Explorer.</summary>
    public static bool Open(string? rel)
    {
        var full = Paths.StudyPath(rel);
        if (full is null) return false;
        if (Directory.Exists(full)) { Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { full } }); return true; }
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
        var wantStem = Norm(Path.GetFileNameWithoutExtension(file));
        var hasExt = Path.HasExtension(file);
        static int Rank(string f) => Path.GetExtension(f).ToLowerInvariant() switch { ".pdf" => 0, ".pptx" or ".ppt" => 1, _ => 2 };
        var hits = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
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
        Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { "/select,", full } });
        return true;
    }

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
