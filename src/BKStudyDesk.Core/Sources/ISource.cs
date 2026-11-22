using System.Text.Json.Nodes;

namespace SoHocTap.Sources;

public interface ISource
{
    string Name { get; }

    SourceStatus Status();

    JsonNode? Data();

    Task SyncAsync(Action<string> log, bool force, CancellationToken ct);

    TimeSpan Interval { get; }

    bool Scheduled => true;

    void Flush() { }
}

public sealed record SourceStatus(bool Connected, long? SyncedAt, string? User = null, string? Term = null);

public interface IBrowserRunner
{
    Task<IReadOnlyDictionary<string, FetchResult>> FetchAsync(IReadOnlyList<FetchRequest> requests, CancellationToken ct, Action<string>? onEach = null);

    Task<string> PageAsync(string url, CancellationToken ct, string? mustContain = null);

    void Reenter() { }
}

public static class SyncSignal
{
    public const string DataReady = "\u0001data-ready";

    private const char StepMark = '\u0002';
    private const char WarnMark = '\u0003';

    public static string Warn(string what, string detail) => $"{WarnMark}{what}{DetailMark}{detail}";

    public const char DetailMark = '\u001f';

    public static bool TryParseWarn(string line, out string text)
    {
        text = line.Length > 1 && line[0] == WarnMark ? line[1..] : "";
        return text.Length > 0;
    }

    public static string Progress(SyncProgress p) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{StepMark}{p.Count}/{p.Of}/{p.Permille ?? -1}{StepMark}{p.Key}{(p.Detail is { Length: > 0 } d ? DetailMark + d : "")}");

    public static bool TryParseProgress(string line, out SyncProgress progress)
    {
        progress = default;
        if (line.Length < 4 || line[0] != StepMark) return false;
        var end = line.IndexOf(StepMark, 1);
        if (end < 0) return false;
        var n = line[1..end].Split('/');
        if (n.Length != 3) return false;
        var v = new int[3];
        for (var i = 0; i < 3; i++)
            if (!int.TryParse(n[i], System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out v[i])) return false;
        var rest = line[(end + 1)..].Split(DetailMark, 2);
        if (rest[0].Length == 0) return false;
        progress = new SyncProgress(rest[0], v[0], v[1], rest.Length > 1 ? rest[1] : null, v[2] < 0 ? null : Math.Min(v[2], 1000));
        return true;
    }
}

public sealed class SessionExpiredException(string message) : InvalidOperationException(message);

public sealed record FetchRequest(string Name, string Url, string Method = "GET", string? Body = null, TimeSpan? Timeout = null);

public sealed record FetchResult(int Status, string Body, bool HasToken);
