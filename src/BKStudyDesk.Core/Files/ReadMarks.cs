using System.Text.Json.Nodes;

namespace SoHocTap.Files;

public sealed class ReadMarks
{
    private readonly Dictionary<string, long> _at;

    public ReadMarks(JsonObject? saved = null) =>
        _at = (saved ?? []).Where(kv => kv.Value is JsonValue v && v.TryGetValue<long>(out _)).ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<long>());

    private static string Key(string path) => NameMatch.Nfc(path).Replace('\\', '/').ToLowerInvariant();

    public bool IsRead(string path, long modified) => _at.TryGetValue(Key(path), out var at) && at >= modified;

    public bool Set(string path, long modified, bool read)
    {
        var k = Key(path);
        if (!read) return _at.Remove(k);
        if (_at.TryGetValue(k, out var at) && at == modified) return false;
        _at[k] = modified;
        return true;
    }

    public JsonObject ToJson() => new(_at.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)));
}
