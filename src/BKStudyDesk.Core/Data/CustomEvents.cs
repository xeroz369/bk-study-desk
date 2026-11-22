using SoHocTap.Core;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SoHocTap.Data;

public sealed record CustomEvent
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Date { get; init; } = "";
    public string Start { get; init; } = "";
    public string End { get; init; } = "";
    public string Location { get; init; } = "";
    public string Kind { get; init; } = CustomEvents.KindEvent;
    public string Note { get; init; } = "";
    public long CreatedAt { get; init; }

    [JsonIgnore] public bool IsMakeup => Kind == CustomEvents.KindMakeup;

    [JsonIgnore] public DateTime? Day => DateTime.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    [JsonIgnore] public int? StartMin => Core.VnTime.ParseClock(Start);

    [JsonIgnore] public int? EndMin => Core.VnTime.ParseClock(End) is { } e && StartMin is { } s && e > s ? e : StartMin is { } st ? Math.Min(st + 60, 24 * 60) : null;

    [JsonIgnore] public DateTime? StartWall => Day is { } d && StartMin is { } m ? d.AddMinutes(m) : null;
}

public sealed class CustomEventFile
{
    public int Version { get; set; } = 1;
    public List<CustomEvent?>? Events { get; set; } = [];
}

public sealed class CustomEventStore
{
    private readonly Func<string> _path;
    private readonly Action<string, JsonNode> _write;
    private readonly CachedFile<List<CustomEvent>> _file;
    private readonly object _gate = new();

    public CustomEventStore(Func<string> path, Action<string, JsonNode> write, Action<string>? onProblem = null)
    {
        _path = path;
        _write = write;
        _file = new CachedFile<List<CustomEvent>>(path, CustomEvents.Parse) { OnProblem = onProblem };
    }

    public string? Problem { get { _file.Get(); return _file.Problem; } }

    public IReadOnlyList<CustomEvent> All() => _file.Get() ?? [];

    public CustomEvent? Find(string id) => All().FirstOrDefault(e => e.Id == id);

    public CustomEvent Save(CustomEvent e)
    {
        lock (_gate)
        {
            var item = e with
            {
                Id = e.Id.Length > 0 ? e.Id : Guid.NewGuid().ToString("N"),
                CreatedAt = e.CreatedAt > 0 ? e.CreatedAt : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            var list = All().Where(x => x.Id != item.Id).Append(item).ToList();
            WriteAll(list);
            return item;
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            var list = All().ToList();
            if (list.RemoveAll(x => x.Id == id) == 0) return false;
            WriteAll(list);
            return true;
        }
    }

    private void WriteAll(List<CustomEvent> list)
    {
        if (_file.Problem is { } p) throw new InvalidOperationException(p);
        _write(_path(), CustomEvents.Serialize(list));
        _file.Invalidate();
    }
}

public static class CustomEvents
{
    public const string KindEvent = "event";
    public const string KindMakeup = "makeup";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = CoreJson.Default,
    };

    public static List<CustomEvent> Parse(string text)
    {
        var file = DataJson.Parse<CustomEventFile>(text);
        return Normalize(file?.Events ?? []);
    }

    public static List<CustomEvent> Normalize(IEnumerable<CustomEvent?> events) =>
        [.. events.OfType<CustomEvent>()
            .Select(e => e with
            {
                Id = (e.Id ?? "").Trim(),
                Title = (e.Title ?? "").Trim(),
                Date = (e.Date ?? "").Trim(),
                Start = (e.Start ?? "").Trim(),
                End = (e.End ?? "").Trim(),
                Location = (e.Location ?? "").Trim(),
                Kind = e.Kind == KindMakeup ? KindMakeup : KindEvent,
                Note = e.Note ?? "",
            })
            .Where(e => e.Id.Length > 0)
            .OrderBy(e => e.Date, StringComparer.Ordinal).ThenBy(e => e.StartMin ?? 0).ThenBy(e => e.CreatedAt)];

    public static JsonNode Serialize(IEnumerable<CustomEvent> events) =>
        Js.SerializeToNode(new CustomEventFile { Events = [.. Normalize(events)] }, WriteOptions)!;

    public static long? StartTime(CustomEvent e) => e.StartWall is { } w ? Core.VnTime.FromWall(w) : null;

    public static Core.IcsEvent? ToIcs(CustomEvent e, string makeupLabel)
    {
        if (e.Day is not { } day || e.StartMin is not { } s || e.EndMin is not { } end) return null;
        var description = string.Join("\n", new[] { e.IsMakeup ? makeupLabel : "", e.Note.Trim() }.Where(x => x.Length > 0));
        return new Core.IcsEvent($"ce-{e.Id}", day.AddMinutes(s), day.AddMinutes(end), e.Title, e.Location, description);
    }

    public static List<(CustomEvent Event, long Time, string Key)> DueSoon(IEnumerable<CustomEvent> events, long now, long window, Func<string, bool> alreadySent) =>
        [.. events.Select(e => (Event: e, Time: StartTime(e) ?? 0, Key: $"ce-{e.Id}@{e.Date}T{e.Start}"))
            .Where(x => x.Time > now && x.Time - now <= window && !alreadySent(x.Key))
            .OrderBy(x => x.Time)];

    public static (string Code, string Name)? MatchCourse(string title, IEnumerable<(string Code, string Name)> courses)
    {
        var t = title ?? "";
        (string Code, string Name)? best = null;
        var bestLen = 0;
        foreach (var c in courses)
        {
            var len = 0;
            if (c.Name.Length > 0 && t.Contains(c.Name, StringComparison.OrdinalIgnoreCase)) len = c.Name.Length;
            else if (c.Code.Length >= 3 && t.Contains(c.Code, StringComparison.OrdinalIgnoreCase)) len = c.Code.Length;
            if (len > bestLen) { best = c; bestLen = len; }
        }
        return best;
    }
}
