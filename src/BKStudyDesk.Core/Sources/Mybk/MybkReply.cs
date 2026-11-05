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
    /// <summary>
    /// Why null là đọc được. Retry: lỗi có thể thoáng qua (server lỗi, mất kết nối, trả trang HTML hay mã lỗi trong JSON).
    /// Missing: MyBK trả lời được nhưng không có dữ liệu này (code 404 trong JSON, kèm câu của MyBK), không phải lỗi.
    /// </summary>
    public readonly record struct Reply(JsonNode? Data, string? Why, bool Retry, string? Missing = null);

    /// <summary>
    /// Hết phiên (401, 403, trang không có token) thì ném SessionExpiredException như trước. Không gọi lại khi server bảo nghỉ (429, 503,
    /// Pace.IsThrottle) hay lỗi 4xx khác (sai đường dẫn, gọi lại cũng vậy).
    /// </summary>
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
        // MyBK báo không tìm thấy (sinh viên năm nhất chưa có CTĐT: "Không tìm thấy thông tin CTĐT", 09/10/2026): gọi lại cũng vậy.
        if (body?["code"]?.ToString() == "404") return new(null, null, false, (body["msg"]?.ToString() ?? "").Trim());
        return new(null, body is null ? "trả về không phải JSON" : $"mã {body["code"]} {body["msg"]}".Trim(), true);
    }

    /// <summary>
    /// Hết phiên: MyBK trả 401, 403, hay trả lời lỗi mà trang không có token. Không có phản hồi (Status 0 hay âm: mất mạng, fetch lỗi,
    /// quá giờ) thì không phải hết phiên: trước đây một API treo quá giờ bị báo là hết phiên dù SSO và LMS vẫn còn (05/11/2026).
    /// </summary>
    public static bool Expired(FetchResult r) => r.Status is 401 or 403 || (r.Status > 0 && r.Status != 200 && !r.HasToken);

    /// <summary>
    /// Gọi các API; API lỗi có thể thoáng qua (Read().Retry) thì gọi lại đúng các API đó một lần sau <paramref name="retryAfter"/>, trước
    /// khi báo. Mọi API trong sources.mybk.api chỉ đọc nên gọi lại không đổi gì trên trường; tối đa một lần mỗi lượt để không dồn request.
    /// Retried: tên các API đã gọi lại (để ghi log).
    /// </summary>
    public static async Task<(Dictionary<string, FetchResult> Results, IReadOnlyList<string> Retried)> FetchAsync(IBrowserRunner browser,
        IReadOnlyList<FetchRequest> requests, TimeSpan retryAfter, CancellationToken ct, Action<string>? onEach = null)
    {
        var res = new Dictionary<string, FetchResult>(await browser.FetchAsync(requests, ct, onEach));
        // MyBK báo hết phiên (401, 403, trang không có token): chưa báo người dùng ngay. Vào lại MyBK (trang /app mới, hết thì qua SSO)
        // rồi gọi lại đúng các API đó một lần. SSO đòi mật khẩu thì trình duyệt ném SessionExpiredException: lúc đó mới là hết phiên thật.
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

    /// <summary>
    /// API lỗi thì chỉ ghi log, không báo người dùng: curriculumInfo chỉ là phần đầu của chương trình đào tạo (tên, khoa, tín chỉ, điểm
    /// trung bình); MybkSource.Build giữ phần đầu của lần trước và vẫn cập nhật danh sách môn (một người dùng bị báo lỗi chương trình đào
    /// tạo, 09/10/2026).
    /// Các API phụ (điểm thành phần, quyết định học vụ, ngày CTXH, học phí) cũng vậy: MyBK hay lỗi phía máy chủ (mã 500 kèm câu Java,
    /// 05/11/2026), báo lên chỉ làm người dùng hoang mang; phần đó giữ bản lưu lần trước.
    /// </summary>
    public static bool Silent(string api) => api is "curriculumInfo" or "components" or "decisions" or "socialWork" or "fees";
}
