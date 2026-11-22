using System.Text.Json.Nodes;

namespace SoHocTap.Data;

public sealed class SyncedFile(Func<JsonNode?> readFile, Func<JsonNode, bool> writeFile)
{
    private readonly object _gate = new();
    private JsonObject? _pending;

    public bool Write(JsonNode node)
    {
        lock (_gate)
        {
            if (node is JsonObject o && DataDiff.SameData(readFile(), o))
            {
                _pending = (JsonObject)o.DeepClone();
                return false;
            }
            var wrote = writeFile(node);
            _pending = null;
            return wrote;
        }
    }

    public JsonObject Latest()
    {
        lock (_gate) return (JsonObject?)_pending?.DeepClone() ?? readFile() as JsonObject ?? [];
    }

    public long? SyncedAt(long? fromFile)
    {
        long mem;
        lock (_gate) mem = DataDiff.Seconds(_pending, "syncedAt");
        var t = Math.Max(fromFile ?? 0, mem);
        return t > 0 ? t : null;
    }

    public static bool IsQuiet(object? before, object? after, bool sawNewData, int warnings) =>
        ReferenceEquals(before, after) && !sawNewData && warnings == 0;

    public bool Flush()
    {
        lock (_gate)
        {
            if (_pending is null) return false;
            var p = _pending;
            _pending = null;
            return DataDiff.Seconds(p, "syncedAt") > DataDiff.Seconds(readFile(), "syncedAt") && writeFile(p);
        }
    }
}
