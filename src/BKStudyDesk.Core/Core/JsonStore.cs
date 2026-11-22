using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace SoHocTap.Core;

public static class JsonStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = CoreJson.Default,
    };

    private static readonly JsonSerializerOptions Compact = new(Options) { WriteIndented = false };

    public static JsonNode? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs);
            return JsonNode.Parse(reader.ReadToEnd());
        }
        catch (Exception e) when (e is IOException or JsonException) { Log.Warn($"Không đọc được {path}: {e.Message}"); return null; }
    }

    public static JsonObject ReadObject(string path) => Read(path) as JsonObject ?? new JsonObject();

    public static bool Write(string path, JsonNode? node)
    {
        var text = node?.ToJsonString(Options) ?? "null";
        return AtomicFile.WriteIfChanged(path, text, a => Retry(path, a));
    }

    private static void Retry(string path, Action write)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { write(); return; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 5)
            {
                Log.Debug($"Ghi {Path.GetFileName(path)}: file đang bận, thử lại ({attempt})");
                Thread.Sleep(100 * attempt);
            }
        }
    }

    public static bool Write<T>(string path, T value) => Write(path, value as JsonNode ?? Js.SerializeToNode(value, Options));

    public static string Serialize<T>(T value) => Js.Serialize(value, Compact);
}
