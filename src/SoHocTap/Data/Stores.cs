using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Data;

/// <summary>
/// Một chỗ đọc/ghi data/lms.json. Đọc có kiểu (<see cref="LmsData"/>), cache theo mtime nên gọi bao nhiêu lần cũng chỉ parse
/// lại khi file đổi. Thay cho các chỗ tự parse JSON trước đây (DeadlineNotifier, ApiRouter, Organizer, LmsSource.Status).
/// </summary>
public static class LmsStore
{
    public static string FilePath => Paths.DataFile("lms.json");

    private static readonly CachedFile<LmsData> File = new(() => FilePath, text => DataJson.Parse<LmsData>(text)?.Normalize())
    {
        OnProblem = Log.Warn,
    };

    /// <summary>Dữ liệu LMS đã đồng bộ; null nếu chưa có file hoặc file không đọc được (xem <see cref="Problem"/>).</summary>
    public static LmsData? Read() => File.Get();

    /// <summary>Lý do file có mà không đọc được (null = đọc được hoặc chưa có file).</summary>
    public static string? Problem { get { File.Get(); return File.Problem; } }

    /// <summary>Ghi cả file (atomic: file tạm rồi thay), cache được bỏ để lần đọc sau thấy bản mới.</summary>
    public static void Write(JsonNode node)
    {
        JsonStore.Write(FilePath, node);
        File.Invalidate();
    }
}

/// <summary>Một chỗ đọc/ghi data/mybk.json, giống <see cref="LmsStore"/>.</summary>
public static class MybkStore
{
    public static string FilePath => Paths.DataFile("mybk.json");

    private static readonly CachedFile<MybkData> File = new(() => FilePath, text => DataJson.Parse<MybkData>(text)?.Normalize())
    {
        OnProblem = Log.Warn,
    };

    public static MybkData? Read() => File.Get();

    public static string? Problem { get { File.Get(); return File.Problem; } }

    public static void Write(JsonNode node)
    {
        JsonStore.Write(FilePath, node);
        File.Invalidate();
    }
}
