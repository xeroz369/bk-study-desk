using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Data;

/// <summary>
/// Một chỗ đọc/ghi data/lms.json. Đọc có kiểu (<see cref="LmsData"/>), cache theo mtime nên gọi bao nhiêu lần cũng chỉ parse
/// lại khi file đổi. Thay cho các chỗ tự parse JSON trước đây (DeadlineNotifier, ApiRouter, Organizer, LmsSource.Status).
/// Lượt đồng bộ không có gì mới thì không ghi (<see cref="SyncedFile"/>).
/// </summary>
public static class LmsStore
{
    public static string FilePath => Paths.DataFile("lms.json");

    private static readonly CachedFile<LmsData> File = new(() => FilePath, text => DataJson.Parse<LmsData>(text)?.Normalize())
    {
        OnProblem = Log.Warn,
    };

    private static readonly SyncedFile Synced = new(() => JsonStore.Read(FilePath), node =>
    {
        var wrote = JsonStore.Write(FilePath, node);
        File.Invalidate();
        return wrote;
    });

    /// <summary>Dữ liệu LMS đã đồng bộ; null nếu chưa có file hoặc file không đọc được (xem <see cref="Problem"/>).</summary>
    public static LmsData? Read() => File.Get();

    /// <summary>Lý do file có mà không đọc được (null = đọc được hoặc chưa có file).</summary>
    public static string? Problem { get { File.Get(); return File.Problem; } }

    /// <summary>Ghi cả file (atomic) nếu dữ liệu đổi; trả false nếu chỉ khác dấu thời gian (không ghi, giữ trong RAM).</summary>
    public static bool Write(JsonNode node) => Synced.Write(node);

    /// <summary>Bản mới nhất (kể cả phần chưa ghi ra đĩa), để lượt đồng bộ sau dựng tiếp.</summary>
    public static JsonObject Latest() => Synced.Latest();

    /// <summary>Lần đồng bộ thành công gần nhất (kể cả lượt không có gì mới chưa ghi).</summary>
    public static long? SyncedAt() => Synced.SyncedAt(Read() is { SyncedAt: > 0 } d ? d.SyncedAt : null);

    /// <summary>App thoát: ghi lần kiểm tra cuối.</summary>
    public static void Flush() => Synced.Flush();
}

/// <summary>Sự kiện tự thêm (data/custom-events.json): chỉ trên máy, không gửi đi đâu. Xem <see cref="CustomEventStore"/>.</summary>
public static class CustomEventsData
{
    public static string FilePath => Paths.DataFile("custom-events.json");

    public static CustomEventStore Store { get; } = new(() => FilePath, (path, node) => JsonStore.Write(path, node), Log.Warn);
}

/// <summary>Một chỗ đọc/ghi data/mybk.json, giống <see cref="LmsStore"/>.</summary>
public static class MybkStore
{
    public static string FilePath => Paths.DataFile("mybk.json");

    private static readonly CachedFile<MybkData> File = new(() => FilePath, text => DataJson.Parse<MybkData>(text)?.Normalize())
    {
        OnProblem = Log.Warn,
    };

    private static readonly SyncedFile Synced = new(() => JsonStore.Read(FilePath), node =>
    {
        var wrote = JsonStore.Write(FilePath, node);
        File.Invalidate();
        return wrote;
    });

    public static MybkData? Read() => File.Get();

    public static string? Problem { get { File.Get(); return File.Problem; } }

    public static bool Write(JsonNode node) => Synced.Write(node);

    public static JsonObject Latest() => Synced.Latest();

    public static long? SyncedAt() => Synced.SyncedAt(Read() is { SyncedAt: > 0 } d ? d.SyncedAt : null);

    public static void Flush() => Synced.Flush();
}
