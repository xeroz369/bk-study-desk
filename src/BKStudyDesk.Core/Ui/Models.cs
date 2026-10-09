global using SoHocTap.Data;
using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Ui;

// Các record dữ liệu (LmsData, MybkData…) đã chuyển sang Data/Models.cs (namespace SoHocTap.Data). Dòng global using ở trên
// giữ cho code UI cũ (dùng tên ngắn LmsData, MybkClass…) vẫn build mà không phải sửa.

public static class DataFiles
{
    /// <summary>
    /// Đọc một file trong data\ bằng parser chịu lỗi. Giữ cho code cũ; lms.json và mybk.json nên đọc qua
    /// <see cref="LmsStore"/>, <see cref="MybkStore"/> (có cache, báo được "dữ liệu không đọc được").
    /// </summary>
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
