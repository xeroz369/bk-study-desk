using System.Reflection;
using System.Text.Json.Nodes;

namespace SoHocTap.Core;

/// <summary>
/// Config của app: data/config.json. Default chỉ nằm ở một chỗ là Core/DefaultConfig.json (nhúng vào exe).
/// Lúc load thì merge default với file của người dùng (file người dùng được ưu tiên), key mới tự được thêm vào.
/// </summary>
public static class Config
{
    private static readonly object Gate = new();
    private static JsonObject? _current;
    private static string FilePath => Paths.DataFile("config.json");

    public static JsonObject Current
    {
        get { lock (Gate) return _current ??= Load(); }
    }

    public static JsonObject Load()
    {
        var merged = Defaults();
        Merge(merged, JsonStore.ReadObject(FilePath));
        JsonStore.Write(FilePath, merged);
        return merged;
    }

    public static void Save(JsonObject value)
    {
        lock (Gate)
        {
            var merged = Defaults();
            Merge(merged, value);
            JsonStore.Write(FilePath, merged);
            _current = merged;
        }
    }

    /// <summary>Đổi một giá trị (path dạng chấm) rồi lưu ngay, cho các công tắc lưu liền không cần bấm Lưu.</summary>
    public static void Set(string dotted, JsonNode? value)
    {
        lock (Gate)   // clone + sửa + lưu trong một khóa: hai lần Set cùng lúc không làm mất thay đổi của nhau
        {
            var c = (JsonObject)Current.DeepClone();
            var parts = dotted.Split('.');
            var o = c;
            foreach (var p in parts[..^1]) o = o[p] as JsonObject ?? (JsonObject)(o[p] = new JsonObject());
            o[parts[^1]] = value;
            Save(c);
        }
    }

    /// <summary>Lấy value theo path dạng chấm, ví dụ "sources.lms.site".</summary>
    public static JsonNode? Node(string dotted)
    {
        JsonNode? cur = Current;
        foreach (var part in dotted.Split('.'))
        {
            if (cur is not JsonObject obj || !obj.TryGetPropertyValue(part, out cur)) return null;
        }
        return cur;
    }

    public static string Str(string dotted, string fallback = "") => Node(dotted)?.GetValue<string>() ?? fallback;
    public static int Int(string dotted, int fallback) => Node(dotted) is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;
    public static bool Bool(string dotted, bool fallback) => Node(dotted) is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    public static IReadOnlyList<string> List(string dotted) =>
        Node(dotted) is JsonArray a ? a.Select(x => x?.GetValue<string>() ?? "").Where(x => x.Length > 0).ToList() : [];

    public static IReadOnlyDictionary<string, string> Map(string dotted) =>
        Node(dotted) is JsonObject o ? o.ToDictionary(kv => kv.Key, kv => kv.Value?.GetValue<string>() ?? "") : new Dictionary<string, string>();

    /// <summary>Path tuyệt đối của một folder trong mục "folders" (trong config ghi tương đối so với thư mục Study).</summary>
    public static string Folder(string key) =>
        Path.Combine(Paths.StudyRoot, Str("folders." + key).Replace('/', Path.DirectorySeparatorChar));

    private static JsonObject Defaults()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SoHocTap.Core.DefaultConfig.json")
                      ?? throw new InvalidOperationException("Thiếu DefaultConfig.json trong exe.");
        return (JsonNode.Parse(s) as JsonObject)!;
    }

    private static void Merge(JsonObject target, JsonObject extra)
    {
        foreach (var (key, value) in extra.ToList())
        {
            if (value is JsonObject eo && target[key] is JsonObject to) Merge(to, eo);
            else target[key] = value?.DeepClone();
        }
    }
}
