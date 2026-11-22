using System.Diagnostics;
using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Files;

public static class Documents
{
    public static JsonArray ListSubjects() => DocumentScan.ListSubjects(Organizer.SubjectsRoot);

    public static JsonObject SubjectFiles(string name, int limit = 400) => DocumentScan.SubjectFiles(Organizer.SubjectsRoot, name, Paths.RelativeToStudy, limit);

    public static Task<JsonArray> ListSubjectsAsync(CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            var root = Organizer.SubjectsRoot;
            var list = (JsonArray)DocumentScan.Cached("subjects|" + root, root, () => DocumentScan.ListSubjects(root, ct), ct, out var hit);
            Log.Debug($"Quét danh sách môn: {list.Count} môn, {sw.ElapsedMilliseconds} ms{(hit ? " (cache)" : "")}");
            return list;
        }, ct);

    public static Task<JsonObject> SubjectFilesAsync(string name, int limit = 400, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var root = Organizer.SubjectsRoot;
            return (JsonObject)DocumentScan.Cached($"files|{limit}|{root}|{Path.GetFileName(name)}", Path.Combine(root, Path.GetFileName(name)),
                () => DocumentScan.SubjectFiles(root, name, Paths.RelativeToStudy, limit, ct), ct, out _);
        }, ct);

    public static bool Open(string? rel)
    {
        var full = Paths.StudyPath(rel);
        if (full is null) return false;
        if (Directory.Exists(full)) { Process.Start(new ProcessStartInfo(Explorer) { ArgumentList = { full } }); return true; }
        if (Runnable.Contains(Path.GetExtension(full))) return Reveal(rel);
        var pdfApp = Environment.ExpandEnvironmentVariables(Settings.OpenDocs.PdfApp);
        if (full.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && pdfApp.Length > 0 && File.Exists(pdfApp))
            Process.Start(new ProcessStartInfo(pdfApp) { ArgumentList = { full }, UseShellExecute = false });
        else
            Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
        return true;
    }

    public static string? FindSource(string? subject, string? file)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(file)) return null;
        var root = Path.Combine(Organizer.SubjectsRoot, Path.GetFileName(subject));
        if (!Directory.Exists(root)) return null;
        static string Norm(string s) => s.Normalize().Replace('\\', '/').Trim().ToLowerInvariant();
        var want = Norm(file);
        var hasExt = DocExt.Contains(Path.GetExtension(file));
        var wantStem = hasExt ? Norm(Path.GetFileNameWithoutExtension(file)) : Norm(Path.GetFileName(file));
        static int Rank(string f) => Path.GetExtension(f).ToLowerInvariant() switch { ".pdf" => 0, ".pptx" or ".ppt" => 1, _ => 2 };
        var hits = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => DocExt.Contains(Path.GetExtension(f)))
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

    private static readonly HashSet<string> DocExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".ppt", ".pptx", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".md", ".png", ".jpg", ".jpeg", ".webp",
    };

    public static bool Open(string? rel, int page)
    {
        var full = Paths.StudyPath(rel);
        if (full is null || !File.Exists(full)) return false;
        if (page <= 1 || !full.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return Open(rel);
        var pdfApp = Environment.ExpandEnvironmentVariables(Settings.OpenDocs.PdfApp);
        if (pdfApp.Length > 0 && File.Exists(pdfApp))
            Process.Start(new ProcessStartInfo(pdfApp) { ArgumentList = { "-page", page.ToString(System.Globalization.CultureInfo.InvariantCulture), full }, UseShellExecute = false });
        else
            Process.Start(new ProcessStartInfo("msedge.exe", new Uri(full).AbsoluteUri + "#page=" + page) { UseShellExecute = true });
        return true;
    }

    public static bool Reveal(string? rel)
    {
        var full = Paths.StudyPath(rel);
        if (full is null) return false;
        Process.Start(new ProcessStartInfo(Explorer) { ArgumentList = { "/select,", full } });
        return true;
    }

    private static string Explorer => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    public static bool IsRunnable(string path) => Runnable.Contains(Path.GetExtension(path));

    private static readonly HashSet<string> Runnable = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".scr", ".pif", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta",
        ".lnk", ".url", ".msi", ".msp", ".cpl", ".msc", ".jar", ".reg", ".scf", ".chm", ".application", ".appref-ms",
        ".settingcontent-ms", ".library-ms", ".iso", ".img", ".vhd", ".vhdx", ".xll", ".diagcab", ".appx", ".msix", ".appinstaller",
        ".docm", ".dotm", ".xlsm", ".xltm", ".xlam", ".pptm", ".potm", ".ppam", ".website", ".mht", ".mhtml",
    };

    public static JsonObject ListDir(string rel)
    {
        var full = Paths.StudyPath(rel);
        if (full is null || !Directory.Exists(full)) return new JsonObject { ["path"] = rel, ["dirs"] = new JsonArray(), ["files"] = new JsonArray(), ["missing"] = true };
        return DocumentScan.ListDir(full, f => Paths.RelativeToStudy(f).Replace('\\', '/'));
    }
}
