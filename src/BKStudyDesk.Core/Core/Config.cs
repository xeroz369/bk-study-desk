using System.Reflection;
using System.Text.Json.Nodes;

namespace SoHocTap.Core;

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
        diff["configVersion"] = Version;
        JsonStore.Write(FilePath, diff);
    }

    private const int Version = 2;

    internal static void Migrate(JsonObject user)
    {
        var v = user["configVersion"] is JsonValue jv && jv.TryGetValue<int>(out var n) ? n : 1;
        if (v < 2 && user["sources"]?["lms"] is JsonObject lms)
        {
            lms.Remove("autoDownload");
            lms.Remove("pastTerms");
        }
    }

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

    public static void Set(string dotted, JsonNode? value)
    {
        lock (Gate)
        {
            var c = (JsonObject)Current.DeepClone();
            var parts = dotted.Split('.');
            var o = c;
            foreach (var p in parts[..^1]) o = o[p] as JsonObject ?? (JsonObject)(o[p] = new JsonObject());
            o[parts[^1]] = value;
            Save(c);
        }
    }

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

    public static string Folder(string key) =>
        Path.Combine(Paths.StudyRoot, Str("folders." + key).Replace('/', Path.DirectorySeparatorChar));

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
