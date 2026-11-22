using System.Text.Json.Nodes;

namespace SoHocTap.Files;

public static class DocumentScan
{
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
            list.Add((JsonNode)new JsonObject
            {
                ["name"] = Path.GetFileName(d),
                ["files"] = count,
                ["modified"] = count == 0 ? 0 : new DateTimeOffset(newest, TimeSpan.Zero).ToUnixTimeSeconds(),
            });
        }
        return list;
    }

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

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Stamp, JsonNode Value)> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static JsonNode Cached(string key, string root, Func<JsonNode> scan, CancellationToken ct, out bool hit)
    {
        var stamp = TreeStamp(root, ct);
        if (Cache.TryGetValue(key, out var c) && c.Stamp == stamp) { hit = true; return c.Value.DeepClone(); }
        hit = false;
        var value = scan();
        Cache[key] = (stamp, value.DeepClone());
        return value;
    }

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
