using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;

namespace SoHocTap.Files;

/// <summary>Tên môn chính thức lấy từ tên lớp LMS: môn, mã môn, phần phụ (Thí nghiệm, Bài tập, BTL).</summary>
public sealed record SubjectName(string Subject, string Code, string? Part);

/// <summary>
/// Xếp tài liệu theo môn: &lt;Study&gt;/&lt;folders.subjects&gt;/&lt;Tên môn&gt;/…
/// Không xóa file của người dùng: trùng nội dung thì chuyển vào lưu trữ trùng lặp; trùng tên mà khác nội dung thì thêm " (bản dd-MM-yyyy)".
/// </summary>
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
        if (Directory.Exists(SubjectsRoot))
        {
            var existing = Directory.GetDirectories(SubjectsRoot).Select(Path.GetFileName)
                .FirstOrDefault(d => string.Equals(d, name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) return existing;
        }
        return name;
    }

    /// <summary>"Tên môn (Thí nghiệm) (AB1234)_GV (CQ_HK251) [L01]" → (Tên môn, AB1234, Thí nghiệm).</summary>
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
        return new SubjectName(SafeFolderName(Canonical(baseName)), code, part);
    }

    /// <summary>Tên môn lấy từ LMS dùng làm tên thư mục: bỏ ký tự Windows không cho phép, không cho "." hay "..".</summary>
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

    // ------------------------------------------------------------------ hash nội dung file (có cache)

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

    /// <summary>Map hash → path cho mọi file trong một thư mục môn.</summary>
    public static Dictionary<string, string> HashesIn(string folder)
    {
        var map = new Dictionary<string, string>();
        if (!Directory.Exists(folder)) return map;
        foreach (var f in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(f);
            if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".part", StringComparison.OrdinalIgnoreCase)) continue;
            try { map.TryAdd(Sha256(f), f); } catch (IOException) { }
        }
        return map;
    }

    public static string UniquePath(string dest)
    {
        if (!File.Exists(dest) && !Directory.Exists(dest)) return dest;
        var dir = Path.GetDirectoryName(dest)!;
        var stem = Path.GetFileNameWithoutExtension(dest);
        var ext = Path.GetExtension(dest);
        var stamp = DateTime.Now.ToString("dd-MM-yyyy");
        var cand = Path.Combine(dir, $"{stem} (bản {stamp}){ext}");
        for (int i = 2; File.Exists(cand); i++) cand = Path.Combine(dir, $"{stem} (bản {stamp} {i}){ext}");
        return cand;
    }

    public static void MoveFile(string src, string dest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Move(src, dest);
    }

    /// <summary>Đưa một file vào môn; trùng nội dung thì chuyển vào lưu trữ trùng lặp. Trả về path cuối cùng, hoặc null nếu là bản trùng.</summary>
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

    /// <summary>Xóa folder rỗng. CHỈ dùng cho folder do chính app vừa tạo.</summary>
    public static void RemoveIfEmpty(string folder)
    {
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
    }

    // ------------------------------------------------------------------ giải nén (7-Zip)

    public static bool IsArchive(string path) =>
        Config.List("archives.extensions").Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    private static string? SevenZip() => Config.List("archives.sevenZip").FirstOrDefault(File.Exists);

    /// <summary>
    /// Giải nén vào folder cùng tên nằm bên cạnh, bỏ lớp folder bọc ngoài; file trùng nội dung thì chuyển vào lưu trữ.
    /// Nếu đã có folder cùng tên thì so nội dung (tên file trong zip có thể bị mất dấu); đủ hết rồi thì chỉ cất file nén đi.
    /// Trả về (folder đích hoặc null, chỗ cất file nén gốc), hoặc null nếu bỏ qua.
    /// </summary>
    public static (string? Folder, string Archived)? ExtractArchive(string path, Dictionary<string, string>? known, Action<string> log)
    {
        if (Config.List("archives.skip").Any(s => path.Contains(s, StringComparison.OrdinalIgnoreCase))) return null;
        var exe = SevenZip();
        if (exe is null) { log("  chưa có 7-Zip, bỏ qua giải nén"); return null; }
        var dest = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path).TrimEnd(' ', '.'));

        var tmp = Path.Combine(Paths.Data, "tmp", "unz-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Zip bomb: xem tổng dung lượng khai trong file nén trước khi giải (archives.maxMB, mặc định 2048 MB).
            var limit = Config.Int("archives.maxMB", 2048) * 1024L * 1024;
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
                if (inside.Count == 0 || !inside.All(f => have.Contains(Sha256(f)))) return null;
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

    /// <summary>Duyệt file sau khi giải nén: bỏ qua symlink và junction (file nén có thể chứa link trỏ ra ngoài thư mục tạm).</summary>
    private static readonly EnumerationOptions NoLinks = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = true,
    };

    /// <summary>Tổng dung lượng sau giải nén theo danh sách của 7-Zip (`7z l -slt`); null nếu không đọc được.</summary>
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
        // -snz: file giải nén mang theo dấu "tải từ Internet" (Zone.Identifier) của file nén, để SmartScreen/Protected View vẫn chặn.
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

    // ------------------------------------------------------------------ nhận file từ thư mục Tải xuống

    /// <summary>Tên môn và mã môn đã biết (lấy từ data LMS và các thư mục môn).</summary>
    private static Dictionary<string, HashSet<string>> KnownSubjects()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (JsonStore.Read(Paths.DataFile("lms.json"))?["courses"] is JsonArray courses)
            foreach (var c in courses)
                if (c?["subject"]?.GetValue<string>() is { } s)
                {
                    if (!map.TryGetValue(s, out var codes)) map[s] = codes = [];
                    if (c["code"]?.GetValue<string>() is { Length: > 0 } code) codes.Add(code);
                }
        if (Directory.Exists(SubjectsRoot))
            foreach (var d in Directory.GetDirectories(SubjectsRoot)) map.TryAdd(Path.GetFileName(d), []);
        return map;
    }

    private static string? MatchSubject(string name, Dictionary<string, HashSet<string>> subjects) =>
        subjects.Where(kv => kv.Value.Any(c => name.Contains(c, StringComparison.OrdinalIgnoreCase)) || name.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key).OrderByDescending(s => s.Length).FirstOrDefault();

    /// <summary>Xếp file trong thư mục Tải xuống vào môn (khớp theo tên hoặc mã môn). apply=false thì chỉ liệt kê, không chuyển.</summary>
    public static List<string> ImportDownloads(bool apply)
    {
        var lines = new List<string>();
        var folder = Environment.ExpandEnvironmentVariables(Config.Str("downloads.folder").Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
        if (!Directory.Exists(folder)) return lines;
        var exts = Config.List("downloads.extensions");
        var subjects = KnownSubjects();
        var known = new Dictionary<string, Dictionary<string, string>>();
        foreach (var entry in Directory.EnumerateFileSystemEntries(folder).ToList())
        {
            var subject = MatchSubject(Path.GetFileName(entry), subjects);
            if (subject is null) continue;
            var files = Directory.Exists(entry)
                ? Directory.EnumerateFiles(entry, "*", SearchOption.AllDirectories).ToList()
                : [entry];
            foreach (var f in files.Where(f => exts.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase))))
            {
                var rel = Path.GetRelativePath(folder, f);
                var dest = Path.Combine(SubjectsRoot, subject, Config.Str("folders.downloadsSubfolder"), rel);
                lines.Add($"{rel} -> {Paths.RelativeToStudy(dest)}");
                if (!apply) continue;
                var subjDir = Path.Combine(SubjectsRoot, subject);
                if (!known.TryGetValue(subjDir, out var k)) known[subjDir] = k = HashesIn(subjDir);
                PlaceFile(f, dest, k, Path.Combine("Tải xuống", rel));
            }
            if (apply && Directory.Exists(entry)) RemoveEmptyTree(entry);
        }
        SaveHashCache();
        return lines;
    }

    /// <summary>Dọn cây folder trong Tải xuống sau khi đã chuyển hết file (chỉ gọi cho folder app vừa xử lý).</summary>
    private static void RemoveEmptyTree(string root)
    {
        foreach (var d in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length)) RemoveIfEmpty(d);
        RemoveIfEmpty(root);
    }
}
