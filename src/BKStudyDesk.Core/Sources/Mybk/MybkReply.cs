using System.Text.Json;
using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Sources.Mybk;

/// <summary>
/// Đọc một phản hồi API MyBK (thuần, test được): dữ liệu <c>data</c>, hay lý do lỗi kèm có nên gọi lại một lần không. Tách khỏi
/// MybkSource.RunAsync để quy tắc gọi lại nằm một chỗ.
/// </summary>
public static class MybkReply
{
    /// <summary>Why null là đọc được. Retry: lỗi có thể thoáng qua (server lỗi, mất kết nối, trả trang HTML hay mã lỗi trong JSON).</summary>
    public readonly record struct Reply(JsonNode? Data, string? Why, bool Retry);

    /// <summary>
    /// Hết phiên (401, 403, trang không có token) thì ném SessionExpiredException như trước. Không gọi lại khi server bảo nghỉ (429, 503,
    /// Pace.IsThrottle) hay lỗi 4xx khác (sai đường dẫn, gọi lại cũng vậy).
    /// </summary>
    public static Reply Read(FetchResult r)
    {
        if (r.Status != 200)
        {
            if (r.Status is 401 or 403 || !r.HasToken) throw new SessionExpiredException("Phiên MyBK hết hạn, cần đăng nhập HCMUT lại.");
            var why = r.Status <= 0 ? "không kết nối được" : $"HTTP {r.Status}";
            return new(null, why, r.Status <= 0 || (r.Status >= 500 && !Pace.IsThrottle(r.Status)));
        }
        JsonObject? body;
        try { body = JsonNode.Parse(r.Body) as JsonObject; }
        catch (JsonException) { body = null; }
        if (body?["code"]?.ToString() is "200" or "204") return new(body["data"]?.DeepClone(), null, false);
        return new(null, body is null ? "trả về không phải JSON" : $"mã {body["code"]} {body["msg"]}".Trim(), true);
    }

    /// <summary>
    /// Gọi các API; API lỗi có thể thoáng qua (Read().Retry) thì gọi lại đúng các API đó một lần sau <paramref name="retryAfter"/>, trước
    /// khi báo. Mọi API trong sources.mybk.api chỉ đọc nên gọi lại không đổi gì trên trường; tối đa một lần mỗi lượt để không dồn request.
    /// Retried: tên các API đã gọi lại (để ghi log).
    /// </summary>
    public static async Task<(Dictionary<string, FetchResult> Results, IReadOnlyList<string> Retried)> FetchAsync(IBrowserRunner browser,
        IReadOnlyList<FetchRequest> requests, TimeSpan retryAfter, CancellationToken ct, Action<string>? onEach = null)
    {
        var res = new Dictionary<string, FetchResult>(await browser.FetchAsync(requests, ct, onEach));
        var again = requests.Where(q => res.TryGetValue(q.Name, out var r) && r.Status is not (401 or 403) && r.HasToken && Read(r).Retry).ToList();
        if (again.Count == 0) return (res, []);
        await Task.Delay(retryAfter, ct);
        foreach (var (k, v) in await browser.FetchAsync(again, ct)) res[k] = v;
        return (res, [.. again.Select(q => q.Name)]);
    }

    /// <summary>
    /// API lỗi thì chỉ ghi log, không báo người dùng: curriculumInfo chỉ là phần đầu của chương trình đào tạo (tên, khoa, tín chỉ, điểm
    /// trung bình); MybkSource.Build giữ phần đầu của lần trước và vẫn cập nhật danh sách môn (một người dùng bị báo lỗi chương trình đào
    /// tạo, 09/10/2026).
    /// </summary>
    public static bool Silent(string api) => api == "curriculumInfo";
}
