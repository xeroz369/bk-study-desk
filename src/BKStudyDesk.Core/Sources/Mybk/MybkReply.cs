using System.Text.Json;
using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Sources.Mybk;

public static class MybkReply
{
    public readonly record struct Reply(JsonNode? Data, string? Why, bool Retry, string? Missing = null);

    public static Reply Read(FetchResult r)
    {
        if (r.Status != 200)
        {
            if (Expired(r)) throw new SessionExpiredException("Phiên MyBK hết hạn, cần đăng nhập HCMUT lại.");
            var why = r.Status <= 0 ? "không kết nối được" : $"HTTP {r.Status}";
            return new(null, why, r.Status <= 0 || (r.Status >= 500 && !Pace.IsThrottle(r.Status)));
        }
        JsonObject? body;
        try { body = JsonNode.Parse(r.Body) as JsonObject; }
        catch (JsonException) { body = null; }
        if (body?["code"]?.ToString() is "200" or "204") return new(body["data"]?.DeepClone(), null, false);
        if (body?["code"]?.ToString() == "404") return new(null, null, false, (body["msg"]?.ToString() ?? "").Trim());
        return new(null, body is null ? "trả về không phải JSON" : $"mã {body["code"]} {body["msg"]}".Trim(), true);
    }

    public static bool Expired(FetchResult r) => r.Status is 401 or 403 || (r.Status > 0 && r.Status != 200 && !r.HasToken);

    public static async Task<(Dictionary<string, FetchResult> Results, IReadOnlyList<string> Retried)> FetchAsync(IBrowserRunner browser,
        IReadOnlyList<FetchRequest> requests, TimeSpan retryAfter, CancellationToken ct, Action<string>? onEach = null)
    {
        var res = new Dictionary<string, FetchResult>(await browser.FetchAsync(requests, ct, onEach));
        var expired = requests.Where(q => res.TryGetValue(q.Name, out var r) && Expired(r)).ToList();
        if (expired.Count > 0)
        {
            browser.Reenter();
            foreach (var (k, v) in await browser.FetchAsync(expired, ct)) res[k] = v;
        }
        var again = requests.Where(q => !expired.Contains(q) && res.TryGetValue(q.Name, out var r) && !Expired(r) && Read(r).Retry).ToList();
        if (again.Count == 0) return (res, [.. expired.Select(q => q.Name)]);
        await Task.Delay(retryAfter, ct);
        foreach (var (k, v) in await browser.FetchAsync(again, ct)) res[k] = v;
        return (res, [.. expired.Concat(again).Select(q => q.Name)]);
    }

    public static bool Silent(string api) => api is "curriculumInfo" or "components" or "decisions" or "socialWork" or "fees";
}
