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

/// <summary>Tài liệu đã đọc (data/doc-read.json, <see cref="Files.ReadMarks"/>): đọc một lần, ghi mỗi lần đổi. Chỉ trên máy này.</summary>
public static class DocReads
{
    public static string FilePath => Paths.DataFile("doc-read.json");

    private static Files.ReadMarks? _marks;

    public static Files.ReadMarks Marks => _marks ??= new Files.ReadMarks(JsonStore.ReadObject(FilePath));

    public static void Set(string path, long modified, bool read)
    {
        if (Marks.Set(path, modified, read)) JsonStore.Write(FilePath, Marks.ToJson());
    }
}

/// <summary>Cấu trúc một khóa LMS lưu khi đồng bộ (data/lms-course/&lt;id&gt;.json): mục, module, file, link. Null khi chưa đọc khóa đó.</summary>
public static class LmsCourseStore
{
    public static JsonObject? Read(long id) => JsonStore.Read(Path.Combine(Paths.DataFile("lms-course"), $"{id}.json")) as JsonObject;
}

/// <summary>
/// Tin nhắn riêng trên LMS (data/lms-messages.json): mã hóa DPAPI theo tài khoản Windows như token (SecretStore), vì tin nhắn riêng nhạy
/// cảm hơn lịch, điểm. File cũ còn là JSON thường thì lần đọc đầu mã hóa lại. Đọc có kiểu, chỉ đọc lại khi file đổi (giờ sửa, kích thước).
/// </summary>
public static class LmsMessagesStore
{
    public static string FilePath => Paths.DataFile("lms-messages.json");

    private static readonly object Gate = new();
    private static (DateTime At, long Length)? _stamp;
    private static LmsMessages? _value;

    public static LmsMessages? Read()
    {
        lock (Gate)
        {
            var info = new FileInfo(FilePath);
            if (!info.Exists) { _stamp = null; _value = null; return null; }
            if (_stamp == (info.LastWriteTimeUtc, info.Length)) return _value;
            // Đọc lỗi (file đang bị khóa, không giải mã được, JSON hỏng): giữ bản đang có và thử lại lần sau, không coi là hộp thư trống.
            try
            {
                if (SecretStore.Read(FilePath) is not { } raw) return _value;
                var parsed = DataJson.Parse<LmsMessages>(raw.ToJsonString())?.Normalize();
                if (parsed is null) return _value;
                info.Refresh();   // file JSON cũ vừa được mã hóa lại: lấy dấu mới để lần sau không đọc lại
                (_value, _stamp) = (parsed, (info.LastWriteTimeUtc, info.Length));
            }
            catch (Exception e) when (e is not OutOfMemoryException) { Log.Warn($"Đọc tin nhắn đã lưu: {e.Message}"); }
            return _value;
        }
    }

    public static JsonObject Raw() => SecretStore.Read(FilePath) ?? new JsonObject();

    public static void Write(JsonNode node)
    {
        lock (Gate)
        {
            SecretStore.Write(FilePath, node as JsonObject ?? new JsonObject());
            _stamp = null;
        }
    }
}

/// <summary>Nội dung trang (mod_page) của các lớp kỳ này (data/lms-pages.json), theo cmid. Null khi chưa đồng bộ trang đó.</summary>
public static class LmsPagesStore
{
    public static string FilePath => Paths.DataFile("lms-pages.json");

    private static readonly CachedFile<JsonObject> File = new(() => FilePath, text => JsonNode.Parse(text) as JsonObject) { OnProblem = Log.Warn };

    public static JsonObject Raw() => JsonStore.ReadObject(FilePath);

    /// <summary>Tên và HTML của trang; null nếu chưa có.</summary>
    public static (string Name, string Html, long? Time)? Read(long cmid) =>
        File.Get()?["pages"]?[cmid.ToString(System.Globalization.CultureInfo.InvariantCulture)] is JsonObject p
            ? (p["name"]?.GetValue<string>() ?? "", p["html"]?.GetValue<string>() ?? "", p["time"]?.GetValue<long?>()) : null;

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
