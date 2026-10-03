using System.Text.Json.Nodes;

namespace SoHocTap.Files;

/// <summary>
/// Quét cây tài liệu (chỉ đọc đĩa), không phụ thuộc config: thư mục gốc truyền vào, test và benchmark chạy được trên thư mục tạm.
/// <see cref="Documents"/> gọi vào đây với thư mục Môn học thật. Bỏ file, thư mục rác (<see cref="JunkFilter"/>).
/// </summary>
public static class DocumentScan
{
    /// <summary>Các môn (thư mục con của <paramref name="subjectsRoot"/>), kèm số file và lần sửa gần nhất.</summary>
    public static JsonArray ListSubjects(string subjectsRoot, CancellationToken ct = default)
    {
        var list = new JsonArray();
        if (!Directory.Exists(subjectsRoot)) return list;
        foreach (var d in Directory.GetDirectories(subjectsRoot).Where(d => !JunkFilter.IsJunkDir(Path.GetFileName(d))).Order(StringComparer.Ordinal))
        {
            int count = 0;
            long newest = 0;
            foreach (var f in JunkFilter.Files(d, ct))
            {
                count++;
                newest = Math.Max(newest, f.LastWriteTimeUtc.Ticks);
            }
            list.Add(new JsonObject
            {
                ["name"] = Path.GetFileName(d),
                ["files"] = count,
                ["modified"] = count == 0 ? 0 : new DateTimeOffset(newest, TimeSpan.Zero).ToUnixTimeSeconds(),
            });
        }
        return list;
    }

    /// <summary>File của một môn, mới nhất lên đầu (tối đa <paramref name="limit"/>), kèm các folder cấp một.</summary>
    /// <param name="relative">Đường dẫn ghi vào "path" (app: tương đối so với thư mục Study).</param>
    public static JsonObject SubjectFiles(string subjectsRoot, string name, Func<string, string> relative, int limit = 400, CancellationToken ct = default)
    {
        var root = Path.Combine(subjectsRoot, Path.GetFileName(name));
        var result = new JsonObject { ["name"] = name, ["folders"] = new JsonArray(), ["files"] = new JsonArray(), ["total"] = 0 };
        if (!Directory.Exists(root)) return result;
        var files = JunkFilter.Files(root, ct).OrderByDescending(f => f.LastWriteTimeUtc).ToList();
        result["total"] = files.Count;
        result["folders"] = new JsonArray(Directory.GetDirectories(root).Select(d => Path.GetFileName(d)).Where(n => !JunkFilter.IsJunkDir(n))
            .Order(StringComparer.Ordinal).Select(x => (JsonNode)x).ToArray());
        result["files"] = new JsonArray(files.Take(limit).Select(f => (JsonNode)new JsonObject
        {
            ["name"] = f.Name,
            ["path"] = relative(f.FullName),
            ["folder"] = Path.GetRelativePath(root, f.DirectoryName!),
            ["size"] = f.Length,
            ["modified"] = new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeSeconds(),
        }).ToArray());
        return result;
    }

    /// <summary>
    /// Đúng một tầng của <paramref name="full"/>: folder con (kèm số mục bên trong, không tính rác) và file. Không quét cả cây.
    /// File bắt đầu bằng '.' cũng ẩn ở đây (như Explorer với file ẩn).
    /// </summary>
    public static JsonObject ListDir(string full, Func<string, string> relative)
    {
        static bool VisibleFile(string n) => !n.StartsWith('.') && !JunkFilter.IsJunkFile(n);
        static bool Visible(FileSystemInfo x) => x is DirectoryInfo ? !JunkFilter.IsJunkDir(x.Name) : VisibleFile(x.Name);
        var dirs = new DirectoryInfo(full).GetDirectories().Where(Visible).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(d => (JsonNode)new JsonObject { ["name"] = d.Name, ["count"] = d.EnumerateFileSystemInfos().Count(Visible) });
        var files = new DirectoryInfo(full).GetFiles().Where(f => VisibleFile(f.Name)).OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(f => (JsonNode)new JsonObject
            {
                ["name"] = f.Name,
                ["size"] = f.Length,
                ["modified"] = new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeSeconds(),
                ["ext"] = f.Extension.ToLowerInvariant(),
            });
        return new JsonObject { ["path"] = relative(full), ["dirs"] = new JsonArray(dirs.ToArray()), ["files"] = new JsonArray(files.ToArray()) };
    }

    // ------------------------------------------------------------------ cache theo mtime thư mục

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Stamp, JsonNode Value)> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Kết quả quét <paramref name="root"/> có cache: cây thư mục không đổi (<see cref="TreeStamp"/> như cũ) thì trả bản đã quét,
    /// không đi lại cả cây. Trả bản sao: người gọi sửa thoải mái. <paramref name="hit"/> cho biết có dùng cache không (log, test).
    /// </summary>
    public static JsonNode Cached(string key, string root, Func<JsonNode> scan, CancellationToken ct, out bool hit)
    {
        var stamp = TreeStamp(root, ct);
        if (Cache.TryGetValue(key, out var c) && c.Stamp == stamp) { hit = true; return c.Value.DeepClone(); }
        hit = false;
        var value = scan();
        Cache[key] = (stamp, value.DeepClone());
        return value;
    }

    /// <summary>
    /// Dấu của một cây thư mục: số thư mục và mtime lớn nhất của chúng. Thêm, xóa, đổi tên file ở đâu trong cây cũng làm mtime
    /// của thư mục chứa nó đổi; chỉ đi qua thư mục (ít hơn file rất nhiều) nên rẻ hơn quét lại. Sửa nội dung một file tại chỗ
    /// không đổi mtime thư mục: số "sửa lần cuối" có thể cũ tới lần thêm/xóa file sau (chấp nhận, đổi lại không quét cả cây).
    /// Không đi vào thư mục rác: .git, .history của KiCad, db của Quartus đổi liên tục, làm cache mất tác dụng.
    /// </summary>
    public static string TreeStamp(string root, CancellationToken ct = default)
    {
        if (!Directory.Exists(root)) return "none";
        var max = Directory.GetLastWriteTimeUtc(root).Ticks;
        var count = 0;
        foreach (var d in JunkFilter.Directories(root, ct))
        {
            count++;
            max = Math.Max(max, d.LastWriteTimeUtc.Ticks);
        }
        return $"{count}:{max}";
    }
}
