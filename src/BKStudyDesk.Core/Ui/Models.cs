global using SoHocTap.Data;
using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Ui;

public static class DataFiles
{
    public static T? Read<T>(string name) where T : class
    {
        var path = Paths.DataFile(name);
        try { return File.Exists(path) ? DataJson.Parse<T>(File.ReadAllText(path)) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DataReadException)
        {
            Log.Warn($"Không đọc được {name}: {e.Message}");
            return null;
        }
    }

    public static string Text(this JsonNode? n) => n is JsonValue v ? v.ToString() : "";
}
