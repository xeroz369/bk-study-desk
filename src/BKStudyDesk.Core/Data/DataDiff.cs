using System.Text.Json.Nodes;

namespace SoHocTap.Data;

public static class DataDiff
{
    public static readonly IReadOnlySet<string> Volatile = new HashSet<string>(StringComparer.Ordinal) { "syncedAt", "lastRun", "registrationAt" };

    public static bool SameData(JsonNode? before, JsonNode? after)
    {
        if (before is not JsonObject b || after is not JsonObject a) return JsonNode.DeepEquals(before, after);
        var keys = b.Select(p => p.Key).Concat(a.Select(p => p.Key)).Where(k => !Volatile.Contains(k)).Distinct();
        return keys.All(k => JsonNode.DeepEquals(b[k], a[k]));
    }

    public static long Seconds(JsonNode? o, string key) =>
        o?[key] is JsonValue v && v.TryGetValue<long>(out var t) ? t : o?[key] is JsonValue d && d.TryGetValue<double>(out var f) ? (long)f : 0;
}
