using System.Text.Json.Nodes;

namespace SoHocTap.Shell;

/// <summary>Một mốc có thể nhắc: nguồn, id, tên hiện trên thông báo, giờ hạn (Unix giây). Mốc đã nộp (<paramref name="Done"/>) không bao giờ được nhắc.</summary>
public sealed record DueItem(string Source, string Id, string Title, string Subject, long Time, bool Done = false);

/// <summary>
/// Chọn mốc cần nhắc và ghi sổ đã nhắc (data/notified.json), hàm thuần để test được (không đọc file, không hiện gì).
/// <list type="bullet">
/// <item>Khóa chống trùng: nguồn:id:mức:phiên bản, phiên bản là giờ hạn (giảng viên dời hạn thì nhắc lại theo hạn mới).
/// Sổ nằm trên đĩa nên khởi động lại app không nhắc lại.</item>
/// <item>Bỏ khóa của mốc đã qua (quá 1 ngày).</item>
/// <item>Mốc nước (watermark) mỗi nguồn và mỗi mức: giờ hạn mới nhất đã nhắc. Mốc đã biết từ trước (có trong <c>seen</c>) mà hạn không
/// muộn hơn mốc nước thì coi như đã nhắc, kể cả khi sổ thiếu khóa (dữ liệu được khôi phục từ bản sao lưu cũ). Mốc mới xuất hiện thì không
/// bị chặn: mốc vào khung nhắc theo thứ tự thời gian, nên mốc cũ hơn mốc nước mà chưa nhắc chỉ có thể là mốc mới thêm.</item>
/// </list>
/// </summary>
public static class Reminders
{
    /// <summary>
    /// Từ bao nhiêu mốc thì gộp thành một thông báo. Nghiên cứu khuyên gộp từ 3; app hiện thông báo bằng balloon của NotifyIcon,
    /// balloon sau đè balloon trước (không có Tag/Group như toast) nên 2 mốc cũng phải gộp, kẻo mất một.
    /// </summary>
    public const int SummaryFrom = 2;

    public static string Key(DueItem d, int hours) => $"{d.Source}:{d.Id}:due{hours}h:{d.Time}";

    /// <param name="all">Mọi mốc còn hạn của dữ liệu hiện tại (để biết mốc nào đã thấy từ trước).</param>
    /// <param name="stages">Các mức nhắc trước, giờ (24, 2).</param>
    /// <param name="ledger">Sổ đã nhắc (đọc từ notified.json, sửa tại chỗ). Trả true trong <c>Changed</c> nếu sổ đổi, cần ghi lại.</param>
    /// <param name="eligible">Mốc được nhắc ở lượt này không (chờ giờ gom thì không). Mốc bị hoãn vẫn tính là đã thấy, không bị đánh dấu đã nhắc.</param>
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
                // Lấy mức gần nhất mà mốc này đã lọt vào; mức xa hơn coi như đã qua (mở app trễ thì chỉ nhắc một lần).
                var stage = stages.Last(h => d.Time - now <= h * 3600L);
                if (keys.ContainsKey(Key(d, stage)) || ledger.ContainsKey($"{d.Id}@{stage}")) continue;   // "id@mức": sổ của bản trước 1.1.8
                var mark = $"{d.Source}@{stage}";
                if (seen.Contains($"{d.Source}:{d.Id}") && DataLong(marks[mark]) is { } wm && d.Time <= wm) continue;
                foreach (var h in stages.Where(h => h >= stage)) keys[Key(d, h)] = d.Time;
                marks[mark] = Math.Max(DataLong(marks[mark]) ?? 0, d.Time);
                fresh.Add((d, stage));
            }
        // Bỏ khóa của mốc đã qua; sổ kiểu cũ ("id@mức": giờ gửi) giữ 30 ngày như trước.
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
