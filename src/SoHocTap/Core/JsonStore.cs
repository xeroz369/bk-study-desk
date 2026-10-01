using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace SoHocTap.Core;

/// <summary>Đọc/ghi JSON. Luôn ghi ra file .tmp rồi mới thay file thật, nên không bao giờ bị file ghi dở.</summary>
public static class JsonStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),   // giữ nguyên tiếng Việt trong file
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions Compact = new(Options) { WriteIndented = false };

    public static JsonNode? Read(string path)
    {
        try { return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null; }
        catch (Exception e) when (e is IOException or JsonException) { Log.Warn($"Không đọc được {path}: {e.Message}"); return null; }
    }

    public static JsonObject ReadObject(string path) => Read(path) as JsonObject ?? new JsonObject();

    public static void Write(string path, JsonNode? node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, node?.ToJsonString(Options) ?? "null");
        File.Move(tmp, path, overwrite: true);
    }

    public static void Write<T>(string path, T value) => Write(path, JsonSerializer.SerializeToNode(value, Options));

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Compact);
}
