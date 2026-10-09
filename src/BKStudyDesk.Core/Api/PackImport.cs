using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SoHocTap.Api;

/// <summary>
/// Phần kiểm gói luyện tập phía app, dùng chung cho POST /api/packs (khung Luyện tập) và nút "Cài vào Luyện tập" của Thư viện.
/// Chỉ chặn phần nguy hiểm cho file system (cỡ, format, id làm tên file). Kiểm nội dung đầy đủ là Presentation/Practice/PackValidator
/// (cùng quy tắc với studypack/lib/pack.ts): chạy lúc Luyện tập nạp gói, gói lỗi thì không nạp và hiện báo lỗi.
/// </summary>
public static partial class PackImport
{
    public const int MaxBytes = 20 * 1024 * 1024;   // gói có ảnh nhúng
    public const string Format = "studypack/1";

    // Id gói = tên file: chặn chặt để không ghi ra ngoài content/packs (cùng luật với validatePack bên UI).
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,63}$")] public static partial Regex Id();

    public static string FileName(string id) => $"{id}.studypack.json";

    /// <summary>Trả lỗi (tiếng Việt, ngắn) hoặc null nếu gói qua được; <paramref name="pack"/> là gói đã parse.</summary>
    public static string? Check(string? body, out JsonObject? pack)
    {
        pack = null;
        if (body is null || Encoding.UTF8.GetByteCount(body) > MaxBytes) return "gói rỗng hoặc quá 20 MB";
        try { pack = JsonNode.Parse(body) as JsonObject; }
        catch (JsonException) { return "không phải JSON"; }
        if (pack is null || (pack["format"] as JsonValue)?.TryGetValue<string>(out var f) != true || f != Format) return "không phải studypack/1";
        var id = (pack["id"] as JsonValue)?.TryGetValue<string>(out var s) == true ? s : "";
        return Id().IsMatch(id) ? null : "id gói không hợp lệ";
    }
}
