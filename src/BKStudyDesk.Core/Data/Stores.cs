using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Data;

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

    public static LmsData? Read() => File.Get();

    public static string? Problem { get { File.Get(); return File.Problem; } }

    public static bool Write(JsonNode node) => Synced.Write(node);

    public static JsonObject Latest() => Synced.Latest();

    public static long? SyncedAt() => Synced.SyncedAt(Read() is { SyncedAt: > 0 } d ? d.SyncedAt : null);

    public static void Flush() => Synced.Flush();
}

public static class CustomEventsData
{
    public static string FilePath => Paths.DataFile("custom-events.json");

    public static CustomEventStore Store { get; } = new(() => FilePath, (path, node) => JsonStore.Write(path, node), Log.Warn);
}

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

public static class LmsCourseStore
{
    public static JsonObject? Read(long id) => JsonStore.Read(Path.Combine(Paths.DataFile("lms-course"), $"{id}.json")) as JsonObject;
}

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
            try
            {
                if (SecretStore.Read(FilePath) is not { } raw) return _value;
                var parsed = DataJson.Parse<LmsMessages>(raw.ToJsonString())?.Normalize();
                if (parsed is null) return _value;
                info.Refresh();
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

public static class LmsPagesStore
{
    public static string FilePath => Paths.DataFile("lms-pages.json");

    private static readonly CachedFile<JsonObject> File = new(() => FilePath, text => JsonNode.Parse(text) as JsonObject) { OnProblem = Log.Warn };

    public static JsonObject Raw() => JsonStore.ReadObject(FilePath);

    public static (string Name, string Html, long? Time)? Read(long cmid) =>
        File.Get()?["pages"]?[cmid.ToString(System.Globalization.CultureInfo.InvariantCulture)] is JsonObject p
            ? (p["name"]?.GetValue<string>() ?? "", p["html"]?.GetValue<string>() ?? "", p["time"]?.GetValue<long?>()) : null;

    public static void Write(JsonNode node)
    {
        JsonStore.Write(FilePath, node);
        File.Invalidate();
    }
}

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
