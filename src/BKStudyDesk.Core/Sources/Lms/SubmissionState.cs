using System.Text.Json.Nodes;

namespace SoHocTap.Sources.Lms;

public enum SubmissionState { Unknown, NotSubmitted, Draft, Submitted, Graded }

public readonly record struct SubmissionAsk(long AssignId, long? Cmid, long Due);

public static class SubmissionStates
{
    public const long SubmittedRecheck = 86400;

    public static SubmissionState FromMoodle(JsonObject? res)
    {
        if (res is null) return SubmissionState.Unknown;
        var last = res["lastattempt"] as JsonObject;
        if (last is null && res["assignmentdata"] is null && res["feedback"] is null) return SubmissionState.Unknown;
        if (res["feedback"]?["grade"]?["grade"] is not null || last?["gradingstatus"]?.ToString() == "graded") return SubmissionState.Graded;
        return last?["submission"]?["status"]?.ToString() switch
        {
            "submitted" => SubmissionState.Submitted,
            "draft" => SubmissionState.Draft,
            _ => SubmissionState.NotSubmitted,
        };
    }

    public static string Key(SubmissionState s) => s switch
    {
        SubmissionState.NotSubmitted => "notsubmitted",
        SubmissionState.Draft => "draft",
        SubmissionState.Submitted => "submitted",
        SubmissionState.Graded => "graded",
        _ => "unknown",
    };

    public static SubmissionState Parse(string? key) => key switch
    {
        "notsubmitted" => SubmissionState.NotSubmitted,
        "draft" => SubmissionState.Draft,
        "submitted" => SubmissionState.Submitted,
        "graded" => SubmissionState.Graded,
        _ => SubmissionState.Unknown,
    };

    public static bool IsDone(SubmissionState s) => s is SubmissionState.Submitted or SubmissionState.Graded;

    public static async Task<Dictionary<long, SubmissionState>> ResolveAsync(IEnumerable<SubmissionAsk> asks, JsonObject cache, long now,
        Func<long, CancellationToken, Task<JsonNode?>> fetch, Func<Exception, bool> recoverable, Action<Exception> onError, CancellationToken ct)
    {
        var result = new Dictionary<long, SubmissionState>();
        var kept = new JsonObject();
        var stop = false;
        var warned = false;
        foreach (var ask in asks)
        {
            if (ask.Due < now - AssignPairing.WindowBefore) continue;
            var key = ask.Cmid?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var old = key is null ? null : cache[key] as JsonObject;
            var oldState = Parse(old?["state"]?.ToString());
            var oldAt = old?["at"] is JsonValue v && v.TryGetValue<long>(out var at) ? at : 0;
            var reuse = oldState == SubmissionState.Graded || (oldState == SubmissionState.Submitted && now - oldAt < SubmittedRecheck);
            if (!reuse && !stop)
            {
                try
                {
                    var state = FromMoodle(await fetch(ask.AssignId, ct) as JsonObject);
                    if (state != SubmissionState.Unknown)
                    {
                        result[ask.AssignId] = state;
                        if (key is not null) kept[key] = new JsonObject { ["state"] = Key(state), ["at"] = now };
                        continue;
                    }
                }
                catch (Exception e) when (recoverable(e))
                {
                    if (e is not LmsException) stop = true;
                    if (!warned) { warned = true; onError(e); }
                }
            }
            if (oldState != SubmissionState.Unknown)
            {
                result[ask.AssignId] = oldState;
                if (key is not null) kept[key] = old!.DeepClone();
            }
        }
        cache.Clear();
        foreach (var p in kept.ToList()) { kept.Remove(p.Key); cache[p.Key] = p.Value; }
        return result;
    }
}
