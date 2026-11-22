using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;

namespace SoHocTap.Files;

public sealed record SubjectName(string Subject, string Code, string? Part);

public static partial class Organizer
{
    public static string SubjectsRoot => Config.Folder("subjects");

    [GeneratedRegex(@"^(.*?)\s*\(([A-Z]{2}\d{4})\)")] private static partial Regex CourseRx();
    [GeneratedRegex(@"\(([^()]+)\)\s*$")] private static partial Regex PartRx();
    [GeneratedRegex(@"HK(\d{3})")] private static partial Regex TermRx();

    public static string Canonical(string name)
    {
        var aliases = Config.Map("subjects.aliases");
        if (aliases.TryGetValue(name.ToLowerInvariant(), out var alias)) name = alias;
        name = NameMatch.Nfc(name);
        if (Directory.Exists(SubjectsRoot))
        {
            var dirs = Directory.GetDirectories(SubjectsRoot).Select(d => Path.GetFileName(d)!).ToList();
            var existing = dirs.FirstOrDefault(d => NameMatch.Same(d, name)) ?? dirs.FirstOrDefault(d => NameMatch.SameFolded(d, name));
            if (existing is not null) return existing;
        }
        return name;
    }

    public static SubjectName? SubjectOf(string fullname)
    {
        var m = CourseRx().Match(fullname);
        if (!m.Success) return null;
        string baseName = m.Groups[1].Value.Trim(), code = m.Groups[2].Value;
        string? part = null;
        var parts = Config.Map("subjects.parts");
        var pm = PartRx().Match(baseName);
        if (pm.Success && parts.TryGetValue(pm.Groups[1].Value.ToLowerInvariant(), out var p))
        {
            part = p;
            baseName = baseName[..pm.Index].Trim();
        }
        var btl = Config.Str("subjects.btlMarker");
        if (btl.Length > 0 && fullname.Contains(btl)) part = "BTL";
        part = SharedPart(fullname, part, Config.Str("subjects.sharedMarker"));
        return new SubjectName(SafeFolderName(Canonical(baseName)), code, part);
    }

    public static string? SharedPart(string fullname, string? part, string marker) =>
        marker.Length == 0 || !fullname.Contains(marker, StringComparison.OrdinalIgnoreCase) ? part
            : part is null ? SharedName : $"{part} ({SharedName.ToLowerInvariant()})";

    public const string SharedName = "Kho chung";

    public static bool IsShared(string? part) => part is not null && part.Contains(SharedName, StringComparison.OrdinalIgnoreCase);

    internal static string SafeFolderName(string name)
    {
        var s = new string(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return s.Length == 0 || s.All(c => c == '.') ? "_" : s;
    }

    public static string TermOf(string fullname)
    {
        var m = TermRx().Match(fullname);
        return m.Success ? "HK" + m.Groups[1].Value : "";
    }

    private static readonly object HashGate = new();
    private static JsonObject? _hashCache;
    private static string HashCachePath => Paths.DataFile("hash-cache.json");

    public static string Sha256(string path)
    {
        var info = new FileInfo(path);
        var key = $"{path.ToLowerInvariant()}|{info.Length}|{new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds()}";
        lock (HashGate)
        {
            _hashCache ??= JsonStore.ReadObject(HashCachePath);
            if (_hashCache[key] is JsonValue v) return v.GetValue<string>();
        }
        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
        lock (HashGate) _hashCache![key] = hash;
        return hash;
    }

    public static void SaveHashCache()
    {
        lock (HashGate) { if (_hashCache is not null) JsonStore.Write(HashCachePath, _hashCache); }
    }

    public static Dictionary<string, string> HashesIn(string folder)
    {
        var map = new Dictionary<string, string>();
        foreach (var f in JunkFilter.Files(folder))
            try { map.TryAdd(Sha256(f.FullName), f.FullName); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return map;
    }

    public static string UniquePath(string dest) => FileOps.UniquePath(dest);

    public static void MoveFile(string src, string dest) => FileOps.MoveFile(src, dest);

    public static string? PlaceFile(string src, string dest, Dictionary<string, string> known, string archiveRelative)
    {
        var h = Sha256(src);
        if (known.TryGetValue(h, out var existing) && File.Exists(existing) && !existing.Equals(src, StringComparison.OrdinalIgnoreCase))
        {
            MoveFile(src, UniquePath(Path.Combine(Config.Folder("archiveDuplicates"), archiveRelative)));
            return null;
        }
        var final = UniquePath(dest);
        MoveFile(src, final);
        known[h] = final;
        return final;
    }

    public static void RemoveIfEmpty(string folder)
    {
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
    }

    public static bool IsArchive(string path) =>
        Config.List("archives.extensions").Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    private static string? SevenZip() => Config.List("archives.sevenZip").FirstOrDefault(File.Exists);

    public static (string? Folder, string Archived)? ExtractArchive(string path, Dictionary<string, string>? known, Action<string> log)
    {
        if (Config.List("archives.skip").Any(s => path.Contains(s, StringComparison.OrdinalIgnoreCase))) return null;
        var exe = SevenZip();
        if (exe is null) { log("  chưa có 7-Zip, bỏ qua giải nén"); return null; }
        var dest = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path).TrimEnd(' ', '.'));

        var tmp = Path.Combine(Paths.Data, "tmp", "unz-" + Guid.NewGuid().ToString("N"));
        try
        {
            var limit = Settings.Archives.MaxMB * 1024L * 1024;
            if (ListSize(exe, path) is not { } size || size > limit)
            {
                log($"  bỏ qua {Path.GetFileName(path)}: không đọc được dung lượng hoặc lớn hơn {limit >> 20} MB khi giải nén");
                return null;
            }
            if (!Run7z(exe, path, tmp)) { log($"  không giải nén được {Path.GetFileName(path)}"); return null; }
            var inside = Directory.EnumerateFiles(tmp, "*", NoLinks).ToList();
            if (Directory.Exists(dest))
            {
                var have = HashesIn(dest).Keys.ToHashSet();
                if (inside.Count == 0 || !inside.All(f => have.Contains(Sha256(f))))
                {
                    Log.Info($"Bỏ qua giải nén {Path.GetFileName(path)}: đã có thư mục {Paths.RelativeToStudy(dest)} với nội dung khác");
                    log($"  bỏ qua giải nén {Path.GetFileName(path)}: đã có thư mục cùng tên với nội dung khác");
                    return null;
                }
                log($"  đã giải nén từ trước: {Paths.RelativeToStudy(dest)}");
                return (dest, ArchiveOriginal(path));
            }
            var tops = Directory.GetFileSystemEntries(tmp);
            var src = tops.Length == 1 && Directory.Exists(tops[0]) ? tops[0] : tmp;
            int kept = 0;
            foreach (var f in Directory.EnumerateFiles(src, "*", NoLinks).ToList())
            {
                var d = Path.Combine(dest, Path.GetRelativePath(src, f));
                if (known is null) { MoveFile(f, UniquePath(d)); kept++; }
                else if (PlaceFile(f, d, known, Path.Combine("Giải nén", Path.GetRelativePath(SubjectsRoot, d))) is not null) kept++;
            }
            var archived = ArchiveOriginal(path);
            log(kept > 0 ? $"  giải nén: {Paths.RelativeToStudy(dest)} ({kept} file)" : $"  {Path.GetFileName(path)}: mọi file bên trong đã có trong môn");
            return (kept > 0 ? dest : null, archived);
        }
        finally
        {
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true); } catch (IOException) { }
        }
    }

    private static readonly EnumerationOptions NoLinks = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = true,
    };

    private static long? ListSize(string exe, string archive)
    {
        var psi = new ProcessStartInfo(exe) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "l", "-slt", "-ba", archive }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        long total = 0;
        string? line;
        while ((line = p.StandardOutput.ReadLine()) is not null)
            if (line.StartsWith("Size = ", StringComparison.Ordinal) && long.TryParse(line.AsSpan(7), out var n)) total += n;
        p.WaitForExit();
        return p.ExitCode == 0 ? total : null;
    }

    private static bool Run7z(string exe, string archive, string outDir)
    {
        var psi = new ProcessStartInfo(exe) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "x", "-y", "-snz", "-bso0", "-bsp0", "-o" + outDir, archive }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode is 0 or 1;
    }

    private static string ArchiveOriginal(string path)
    {
        var dest = UniquePath(Path.Combine(Config.Folder("archiveArchives"), Paths.RelativeToStudy(path)));
        MoveFile(path, dest);
        return dest;
    }

    public sealed record DownloadMove(string Source, string Subject, string Dest);

    public sealed record DownloadResult(int Moved, int Duplicates, IReadOnlyList<(string File, string Error)> Failed);

    public static string DownloadsFolder =>
        Environment.ExpandEnvironmentVariables(Config.Str("downloads.folder").Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

    private static Dictionary<string, HashSet<string>> KnownSubjects()
    {
        var map = new Dictionary<string, HashSet<string>>(NameMatch.NfcIgnoreCase);
        if (Directory.Exists(SubjectsRoot))
            foreach (var d in Directory.GetDirectories(SubjectsRoot))
                if (IsSubjectFolder(Path.GetFileName(d))) map.TryAdd(Path.GetFileName(d), []);
        foreach (var c in LmsStore.Read()?.Courses ?? [])
            if (c.Subject is { } s)
            {
                if (!map.TryGetValue(s, out var codes)) map[s] = codes = [];
                if (c.Code is { Length: > 0 } code) codes.Add(code);
            }
        return map;
    }

    public static bool IsSubjectFolder(string name) => name.Length >= 4 && name[0] is not ('_' or '.');

    internal static string? MatchSubject(string name, Dictionary<string, HashSet<string>> subjects) =>
        subjects.Where(kv => kv.Value.Any(c => name.Contains(c, StringComparison.OrdinalIgnoreCase)) || NameMatch.ContainsFolded(name, kv.Key))
                .Select(kv => kv.Key).OrderByDescending(s => s.Length).FirstOrDefault();

    public static List<DownloadMove> PlanDownloads() =>
        PlanDownloads(DownloadsFolder, SubjectsRoot, Config.Str("folders.downloadsSubfolder"), Config.List("downloads.extensions"), KnownSubjects());

    public static List<DownloadMove> PlanDownloads(string folder, string subjectsRoot, string subfolder,
        IReadOnlyCollection<string> exts, Dictionary<string, HashSet<string>> subjects)
    {
        var plan = new List<DownloadMove>();
        if (!Directory.Exists(folder) || subjects.Count == 0) return plan;
        foreach (var entry in Directory.EnumerateFileSystemEntries(folder).Order(StringComparer.OrdinalIgnoreCase).ToList())
        {
            if (File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint)) continue;
            if (MatchSubject(Path.GetFileName(entry), subjects) is not { } subject) continue;
            var files = Directory.Exists(entry) ? Directory.EnumerateFiles(entry, "*", NoLinks).Order(StringComparer.OrdinalIgnoreCase).ToList() : [entry];
            foreach (var f in files.Where(f => exts.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase))))
                plan.Add(new(f, subject, Path.Combine(subjectsRoot, subject, subfolder, Path.GetRelativePath(folder, f))));
        }
        return plan;
    }

    public static DownloadResult MoveDownloads(IReadOnlyList<DownloadMove> plan)
    {
        var folder = DownloadsFolder;
        var known = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var result = MoveDownloads(plan, folder, m =>
        {
            var subjDir = Path.Combine(SubjectsRoot, m.Subject);
            if (!known.TryGetValue(subjDir, out var k)) known[subjDir] = k = HashesIn(subjDir);
            return PlaceFile(m.Source, m.Dest, k, Path.Combine("Tải xuống", Path.GetRelativePath(folder, m.Source))) is not null;
        });
        SaveHashCache();
        Log.Info($"Tải xuống: chuyển {result.Moved} file vào môn, {result.Duplicates} file trùng, {result.Failed.Count} lỗi");
        foreach (var (file, error) in result.Failed) Log.Warn($"Tải xuống: không chuyển được {file}: {error}");
        return result;
    }

    public static DownloadResult MoveDownloads(IReadOnlyList<DownloadMove> plan, string folder, Func<DownloadMove, bool> place)
    {
        int moved = 0, dup = 0;
        var failed = new List<(string, string)>();
        foreach (var m in plan)
        {
            var name = Path.GetRelativePath(folder, m.Source);
            if (!File.Exists(m.Source)) { failed.Add((name, "file không còn trong Tải xuống")); continue; }
            try { if (place(m)) moved++; else dup++; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failed.Add((name, e.Message)); }
        }
        var tops = plan.Select(m => Path.GetRelativePath(folder, m.Source).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                       .Where(p => p.Length > 1).Select(p => Path.Combine(folder, p[0])).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var top in tops)
            try { if (Directory.Exists(top)) RemoveEmptyTree(top); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return new(moved, dup, failed);
    }

    private static void RemoveEmptyTree(string root)
    {
        foreach (var d in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length)) RemoveIfEmpty(d);
        RemoveIfEmpty(root);
    }
}
