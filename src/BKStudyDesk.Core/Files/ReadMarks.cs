using System.Text.Json.Nodes;

namespace SoHocTap.Files;

/// <summary>
/// Tài liệu đã đọc (danh sách Mới cập nhật của môn): đường dẫn tương đối, giờ sửa của file lúc đánh dấu. File có bản mới hơn
/// (LMS cập nhật, giờ sửa đổi) thì tự thành chưa đọc. Chỉ trên máy này (data/doc-read.json). Hàm thuần, không đọc ghi đĩa.
/// </summary>
public sealed class ReadMarks
{
    private readonly Dictionary<string, long> _at;

    public ReadMarks(JsonObject? saved = null) =>
        _at = (saved ?? []).Where(kv => kv.Value is JsonValue v && v.TryGetValue<long>(out _)).ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<long>());

    // So đường dẫn như Windows: NFC, không phân biệt hoa thường, gạch chéo nào cũng được.
    private static string Key(string path) => NameMatch.Nfc(path).Replace('\\', '/').ToLowerInvariant();

    public bool IsRead(string path, long modified) => _at.TryGetValue(Key(path), out var at) && at >= modified;

    /// <summary>Đánh dấu đã đọc (giữ giờ sửa hiện tại) hay chưa đọc; trả true khi có đổi (cần ghi lại).</summary>
    public bool Set(string path, long modified, bool read)
    {
        var k = Key(path);
        if (!read) return _at.Remove(k);
        if (_at.TryGetValue(k, out var at) && at == modified) return false;
        _at[k] = modified;
        return true;
    }

    public JsonObject ToJson() => new(_at.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)));
}
