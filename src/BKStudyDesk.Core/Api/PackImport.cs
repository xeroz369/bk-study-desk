using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SoHocTap.Api;

public static partial class PackImport
{
    public const int MaxBytes = 20 * 1024 * 1024;
    public const string Format = "studypack/1";

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,63}$")] public static partial Regex Id();

    public static string FileName(string id) => $"{id}.studypack.json";

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
