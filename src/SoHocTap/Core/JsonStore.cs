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
        try
        {
            if (!File.Exists(path)) return null;
            // Cho phép file bị thay (Write) ngay lúc đang đọc: giao diện đọc lms.json trong khi đồng bộ ghi lại file đó.
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs);
            return JsonNode.Parse(reader.ReadToEnd());
        }
        catch (Exception e) when (e is IOException or JsonException) { Log.Warn($"Không đọc được {path}: {e.Message}"); return null; }
    }

    public static JsonObject ReadObject(string path) => Read(path) as JsonObject ?? new JsonObject();

    public static void Write(string path, JsonNode? node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, node?.ToJsonString(Options) ?? "null");
        // Thay file thật: chương trình khác (antivirus, OneDrive, trình soạn thảo) đang mở file thì Windows từ chối; thử lại vài lần.
        for (var attempt = 1; ; attempt++)
        {
            try { File.Move(tmp, path, overwrite: true); return; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 5)
            {
                Log.Debug($"Ghi {Path.GetFileName(path)}: file đang bận, thử lại ({attempt})");
                Thread.Sleep(100 * attempt);
            }
        }
    }

    public static void Write<T>(string path, T value) => Write(path, JsonSerializer.SerializeToNode(value, Options));

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Compact);
}
