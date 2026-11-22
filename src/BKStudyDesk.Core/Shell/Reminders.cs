using System.Text.Json.Nodes;

namespace SoHocTap.Shell;

public sealed record DueItem(string Source, string Id, string Title, string Subject, long Time, bool Done = false);

public static class Reminders
{
    public const int SummaryFrom = 2;

    public static string Key(DueItem d, int hours) => $"{d.Source}:{d.Id}:due{hours}h:{d.Time}";

    public static (List<(DueItem Item, int Hours)> Fresh, bool Changed) Pick(IReadOnlyList<DueItem> all, int[] stages, long now, JsonObject ledger,
        Func<DueItem, bool>? eligible = null)
    {
        var fresh = new List<(DueItem, int)>();
        stages = [.. stages.Where(h => h > 0).Distinct().OrderByDescending(h => h)];
        var keys = ledger["keys"] as JsonObject ?? [];
        var marks = ledger["watermark"] as JsonObject ?? [];
        var seen = ((ledger["seen"] as JsonArray) ?? []).Select(x => x?.ToString() ?? "").ToHashSet();
        var before = ledger.ToJsonString();
        if (stages.Length > 0)
            foreach (var d in all.Where(d => !d.Done && d.Time > now && d.Time - now <= stages[0] * 3600L && (eligible?.Invoke(d) ?? true)).OrderBy(d => d.Time))
            {
                var stage = stages.Last(h => d.Time - now <= h * 3600L);
                if (keys.ContainsKey(Key(d, stage)) || ledger.ContainsKey($"{d.Id}@{stage}")) continue;
                var mark = $"{d.Source}@{stage}";
                if (seen.Contains($"{d.Source}:{d.Id}") && DataLong(marks[mark]) is { } wm && d.Time <= wm) continue;
                foreach (var h in stages.Where(h => h >= stage)) keys[Key(d, h)] = d.Time;
                marks[mark] = Math.Max(DataLong(marks[mark]) ?? 0, d.Time);
                fresh.Add((d, stage));
            }
        foreach (var k in keys.Where(p => DataLong(p.Value) is { } t && t < now - 86400).Select(p => p.Key).ToList()) keys.Remove(k);
        foreach (var k in ledger.Where(p => p.Key.Contains('@') && DataLong(p.Value) is { } t && t < now - 30 * 86400).Select(p => p.Key).ToList()) ledger.Remove(k);
        if (keys.Parent is null) ledger["keys"] = keys;
        if (marks.Parent is null) ledger["watermark"] = marks;
        ledger["seen"] = new JsonArray([.. all.Where(d => d.Time > now).Select(d => $"{d.Source}:{d.Id}").Distinct().Order(StringComparer.Ordinal)
            .Select(s => (JsonNode)s)]);
        return (fresh, ledger.ToJsonString() != before);
    }

    private static long? DataLong(JsonNode? n) => n is JsonValue v && v.TryGetValue<long>(out var t) ? t : null;
}
