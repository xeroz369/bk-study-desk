using System.Text.Json.Nodes;

namespace SoHocTap.Data;

/// <summary>
/// File dữ liệu của một nguồn (lms.json, mybk.json) mà mỗi lượt đồng bộ ghi lại. Lượt không có gì mới (chỉ khác syncedAt, lastRun,
/// registrationAt: <see cref="DataDiff"/>) thì không ghi đĩa: giữ "lần kiểm tra cuối" trong RAM, ghi ra khi dữ liệu đổi hoặc khi app
/// thoát (<see cref="Flush"/>). File không đổi thì cache theo mtime vẫn trúng, UI không phải đọc lại, trang không vẽ lại.
/// Đọc/ghi file truyền vào (store cài bằng JsonStore) để test được với thư mục tạm.
/// </summary>
public sealed class SyncedFile(Func<JsonNode?> readFile, Func<JsonNode, bool> writeFile)
{
    private readonly object _gate = new();
    private JsonObject? _pending;   // bản mới nhất chưa ghi (cùng dữ liệu với file, chỉ mới hơn ở dấu thời gian)

    /// <summary>Ghi bản mới; trả true nếu dữ liệu đổi và đã ghi ra đĩa, false nếu chỉ khác dấu thời gian (giữ trong RAM).</summary>
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

    /// <summary>Bản mới nhất (chưa ghi thì lấy trong RAM), để lượt sau dựng tiếp (giữ phần cũ, registrationAt...).</summary>
    public JsonObject Latest()
    {
        lock (_gate) return (JsonObject?)_pending?.DeepClone() ?? readFile() as JsonObject ?? [];
    }

    /// <summary>Lần đồng bộ thành công gần nhất, kể cả lượt không có gì mới chưa ghi ra đĩa; null nếu chưa có.</summary>
    public long? SyncedAt(long? fromFile)
    {
        long mem;
        lock (_gate) mem = DataDiff.Seconds(_pending, "syncedAt");
        var t = Math.Max(fromFile ?? 0, mem);
        return t > 0 ? t : null;
    }

    /// <summary>
    /// Lượt đồng bộ xong mà không có gì mới: bản dữ liệu trong cache sau lượt chạy vẫn đúng là object lúc đầu (store không ghi nên
    /// CachedFile không đọc lại), không có phần nào đã báo "dữ liệu mới" giữa chừng, không có cảnh báo. Khi đó UI không đọc lại,
    /// không vẽ lại trang, không thông báo.
    /// </summary>
    public static bool IsQuiet(object? before, object? after, bool sawNewData, int warnings) =>
        ReferenceEquals(before, after) && !sawNewData && warnings == 0;

    /// <summary>App thoát: ghi lần kiểm tra cuối (nếu có bản trong RAM mới hơn file). Trả true nếu đã ghi.</summary>
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
