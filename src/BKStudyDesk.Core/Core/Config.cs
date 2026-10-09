using System.Reflection;
using System.Text.Json.Nodes;

namespace SoHocTap.Core;

/// <summary>
/// Config của app: data/config.json. Default chỉ nằm ở một chỗ là Core/DefaultConfig.json (nhúng vào exe).
/// Lúc load thì merge default với file của người dùng (file người dùng được ưu tiên). File chỉ lưu những gì khác default
/// (kèm configVersion), nên đổi default ở bản sau thì người dùng cũ cũng nhận được, trừ chỗ họ đã tự chỉnh.
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
        var user = JsonStore.ReadObject(FilePath);
        Migrate(user);
        var merged = Defaults();
        Merge(merged, user);
        Write(merged);
        return merged;
    }

    public static void Save(JsonObject value)
    {
        lock (Gate)
        {
            var merged = Defaults();
            Merge(merged, value);
            Write(merged);
            _current = merged;
        }
    }

    private static void Write(JsonObject merged)
    {
        var diff = Diff(Defaults(), merged);
        diff["configVersion"] = Version;   // luôn ghi: lần sau biết file đã qua bước nâng cấp nào
        JsonStore.Write(FilePath, diff);
    }

    private const int Version = 2;

    /// <summary>
    /// Nâng cấp file config cũ. Bản 1.0.6 trở về trước ghi cả default ra file, nên không biết chỗ nào người dùng tự chọn:
    /// v2 bỏ tự tải tài liệu (giờ tải theo mục người dùng chọn) và chỉ đọc lớp học kỳ hiện tại.
    /// </summary>
    internal static void Migrate(JsonObject user)
    {
        var v = user["configVersion"] is JsonValue jv && jv.TryGetValue<int>(out var n) ? n : 1;
        if (v < 2 && user["sources"]?["lms"] is JsonObject lms)
        {
            lms.Remove("autoDownload");
            lms.Remove("pastTerms");
        }
    }

    /// <summary>Phần của <paramref name="current"/> khác <paramref name="defaults"/> (đệ quy theo object; mảng so cả mảng).</summary>
    internal static JsonObject Diff(JsonObject defaults, JsonObject current)
    {
        var o = new JsonObject();
        foreach (var (key, value) in current)
        {
            var d = defaults[key];
            if (value is JsonObject co && d is JsonObject dob)
            {
                var sub = Diff(dob, co);
                if (sub.Count > 0) o[key] = sub;
            }
            else if (!JsonNode.DeepEquals(d, value)) o[key] = value?.DeepClone();
        }
        return o;
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

    /// <summary>Giá trị default (chưa tính phần người dùng chỉnh) theo path dạng chấm.</summary>
    internal static JsonNode? DefaultNode(string dotted)
    {
        JsonNode? cur = DefaultsCache.Value;
        foreach (var part in dotted.Split('.'))
        {
            if (cur is not JsonObject obj || !obj.TryGetPropertyValue(part, out cur)) return null;
        }
        return cur;
    }

    private static readonly Lazy<JsonObject> DefaultsCache = new(Defaults);

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
