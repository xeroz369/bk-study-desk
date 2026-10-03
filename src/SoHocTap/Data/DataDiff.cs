using System.Text.Json.Nodes;

namespace SoHocTap.Data;

/// <summary>
/// So dữ liệu đồng bộ trước/sau, bỏ qua các trường chỉ là dấu thời gian của lượt chạy (lần đồng bộ, thống kê lượt chạy, lần đọc
/// trang đăng ký môn). Giống nhau = lượt đồng bộ không có gì mới: không ghi file, không vẽ lại trang, không thông báo.
/// </summary>
public static class DataDiff
{
    /// <summary>Trường cấp một chỉ đổi theo lượt chạy, không phải dữ liệu.</summary>
    public static readonly IReadOnlySet<string> Volatile = new HashSet<string>(StringComparer.Ordinal) { "syncedAt", "lastRun", "registrationAt" };

    public static bool SameData(JsonNode? before, JsonNode? after)
    {
        if (before is not JsonObject b || after is not JsonObject a) return JsonNode.DeepEquals(before, after);
        var keys = b.Select(p => p.Key).Concat(a.Select(p => p.Key)).Where(k => !Volatile.Contains(k)).Distinct();
        return keys.All(k => JsonNode.DeepEquals(b[k], a[k]));
    }

    /// <summary>Số giây của một trường thời gian cấp một (syncedAt...), 0 nếu không có.</summary>
    public static long Seconds(JsonNode? o, string key) =>
        o?[key] is JsonValue v && v.TryGetValue<long>(out var t) ? t : o?[key] is JsonValue d && d.TryGetValue<double>(out var f) ? (long)f : 0;
}
