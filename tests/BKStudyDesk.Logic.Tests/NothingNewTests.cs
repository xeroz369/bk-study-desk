using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Shell;

namespace SoHocTap.Tests;

/// <summary>
/// Lượt đồng bộ thứ hai y như lượt đầu: không ghi file, không báo Changed (UI không đọc lại, không vẽ lại), không thông báo.
/// Chạy trên thư mục tạm, cùng cách store của app ghép CachedFile + SyncedFile + AtomicFile.
/// </summary>
public class NothingNewTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bk-nothing-" + Guid.NewGuid().ToString("N"));
    private string File1 => Path.Combine(_dir, "lms.json");

    public NothingNewTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    /// <summary>Store giống LmsStore: đọc có cache theo mtime, ghi qua SyncedFile.</summary>
    private (CachedFile<JsonObject> Cache, SyncedFile Synced, Func<int> Writes) Store()
    {
        var cache = new CachedFile<JsonObject>(() => File1, t => JsonNode.Parse(t) as JsonObject);
        var writes = 0;
        var synced = new SyncedFile(() => File.Exists(File1) ? JsonNode.Parse(File.ReadAllText(File1)) : null, node =>
        {
            var wrote = AtomicFile.WriteIfChanged(File1, node.ToJsonString());
            if (wrote) { writes++; cache.Invalidate(); }
            return wrote;
        });
        return (cache, synced, () => writes);
    }

    private static JsonObject Lms(long syncedAt, string eventName = "Nộp Lab 2") => new()
    {
        ["syncedAt"] = syncedAt,
        ["events"] = new JsonArray(new JsonObject { ["id"] = "e1", ["name"] = eventName, ["time"] = 2_000_000_000 }),
        ["lastRun"] = new JsonObject { ["checked"] = 5 },
    };

    [Fact]
    public void IdenticalSecondSyncWritesNothingAndIsQuiet()
    {
        var (cache, synced, writes) = Store();
        Assert.True(synced.Write(Lms(100)));
        var before = cache.Get();
        var stamp = File.GetLastWriteTimeUtc(File1);
        var text = File.ReadAllText(File1);

        // Lượt hai: cùng dữ liệu, chỉ khác dấu thời gian.
        Assert.False(synced.Write(Lms(200)));
        var after = cache.Get();
        Assert.Equal(1, writes());
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(File1));
        Assert.Equal(text, File.ReadAllText(File1));
        Assert.Same(before, after);                                     // Hub so tham chiếu: không bắn "data", UI không đọc lại
        Assert.True(SyncedFile.IsQuiet(before, after, sawNewData: false, warnings: 0));
        Assert.Equal(200, synced.SyncedAt(100));                       // "lần cuối" vẫn đúng, giữ trong RAM
        Assert.Equal(200, DataDiff.Seconds(synced.Latest(), "syncedAt"));
    }

    [Fact]
    public void ChangedDataIsWrittenAndNotQuiet()
    {
        var (cache, synced, writes) = Store();
        synced.Write(Lms(100));
        var before = cache.Get();
        Assert.True(synced.Write(Lms(200, "Nộp Lab 3")));
        var after = cache.Get();
        Assert.Equal(2, writes());
        Assert.NotSame(before, after);
        Assert.False(SyncedFile.IsQuiet(before, after, false, 0));
    }

    [Fact]
    public void WarningsOrMidSyncDataAreNotQuiet()
    {
        var o = new object();
        Assert.False(SyncedFile.IsQuiet(o, o, sawNewData: true, warnings: 0));
        Assert.False(SyncedFile.IsQuiet(o, o, sawNewData: false, warnings: 1));
        Assert.False(SyncedFile.IsQuiet(null, o, false, 0));            // lần đầu có dữ liệu
    }

    [Fact]
    public void LastCheckIsWrittenOnlyOnFlush()
    {
        var (_, synced, writes) = Store();
        synced.Write(Lms(100));
        synced.Write(Lms(300));
        Assert.Equal(1, writes());
        Assert.True(synced.Flush());
        Assert.Equal(2, writes());
        Assert.Equal(300, DataDiff.Seconds(JsonNode.Parse(File.ReadAllText(File1)), "syncedAt"));
        Assert.False(synced.Flush());                                   // không còn gì để ghi
    }

    [Fact]
    public void AtomicWriteSkipsIdenticalContent()
    {
        var f = Path.Combine(_dir, "a.json");
        Assert.True(AtomicFile.WriteIfChanged(f, "{\"x\":1}"));
        var stamp = File.GetLastWriteTimeUtc(f);
        Assert.False(AtomicFile.WriteIfChanged(f, "{\"x\":1}"));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(f));
        Assert.True(AtomicFile.WriteIfChanged(f, "{\"x\":2}"));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void VolatileFieldsOnlyAreSameData()
    {
        Assert.True(DataDiff.SameData(Lms(1), Lms(2)));
        var a = Lms(1);
        a["registrationAt"] = 5;
        Assert.True(DataDiff.SameData(a, Lms(9)));
        Assert.False(DataDiff.SameData(Lms(1), Lms(1, "khác")));
    }
}

/// <summary>Nhắc hạn chống trùng qua lần khởi động lại (sổ notified.json), gộp, mốc nước.</summary>
public partial class RemindersTests
{
    private const long Now = 1_800_000_000;
    private static readonly int[] Stages = [24, 2];

    private static DueItem Item(string id, double hoursLeft) => new("lms", id, "Nộp " + id, "Giải tích 2", Now + (long)(hoursLeft * 3600));

    /// <summary>Ghi sổ ra chữ rồi đọc lại, như khởi động lại app.</summary>
    private static JsonObject Restart(JsonObject ledger) => (JsonObject)JsonNode.Parse(ledger.ToJsonString())!;

    [Fact]
    public void SecondCheckAndRestartGiveNoNotification()
    {
        var items = new[] { Item("e1", 10) };
        var ledger = new JsonObject();
        var (first, changed) = Reminders.Pick(items, Stages, Now, ledger);
        Assert.Single(first);
        Assert.True(changed);

        var (again, changed2) = Reminders.Pick(items, Stages, Now + 60, ledger);
        Assert.Empty(again);
        Assert.False(changed2);                                         // sổ không đổi: không ghi file

        var (afterRestart, _) = Reminders.Pick(items, Stages, Now + 120, Restart(ledger));
        Assert.Empty(afterRestart);
    }

    [Fact]
    public void KeyIsSourceIdStageVersion() =>
        Assert.Equal($"lms:e1:due24h:{Now + 36000}", Reminders.Key(Item("e1", 10), 24));

    [Fact]
    public void LastStageNotifiesOnceMore()
    {
        var ledger = new JsonObject();
        Reminders.Pick([Item("e1", 10)], Stages, Now, ledger);
        // 9 giờ sau: còn 1 giờ, vào mức 2 giờ.
        var (late, _) = Reminders.Pick([Item("e1", 10)], Stages, Now + 9 * 3600, ledger);
        Assert.Equal(2, Assert.Single(late).Hours);
    }

    [Fact]
    public void MovedDeadlineIsNotifiedAgain()
    {
        var ledger = new JsonObject();
        Reminders.Pick([Item("e1", 10)], Stages, Now, ledger);
        var (moved, _) = Reminders.Pick([Item("e1", 20)], Stages, Now, ledger);
        Assert.Single(moved);
    }

    [Fact]
    public void PastEventKeysArePruned()
    {
        var ledger = new JsonObject();
        Reminders.Pick([Item("e1", 1)], Stages, Now, ledger);
        Assert.NotEmpty((JsonObject)ledger["keys"]!);
        Reminders.Pick([], Stages, Now + 3 * 86400, ledger);
        Assert.Empty((JsonObject)ledger["keys"]!);
    }

    [Fact]
    public void WatermarkStopsRenotifyingKnownItemsWhenKeysAreLost()
    {
        var ledger = new JsonObject();
        var a = Item("a", 30);   // ngoài khung 24 giờ, chỉ được "thấy"
        var b = Item("b", 20);
        Reminders.Pick([a, b], Stages, Now, ledger);                   // nhắc b, thấy a
        // Sau đó a vào khung; giả sử sổ mất khóa của các mốc (khôi phục dữ liệu) nhưng còn mốc nước và danh sách đã thấy.
        ledger["keys"] = new JsonObject();
        var (got, _) = Reminders.Pick([a, b], Stages, Now + 7 * 3600, ledger);
        Assert.Equal(["a"], got.Select(g => g.Item.Id));               // b (đã thấy, hạn trước mốc nước) không nhắc lại; a thì có
    }

    [Fact]
    public void NewItemEarlierThanWatermarkStillNotifies()
    {
        var ledger = new JsonObject();
        Reminders.Pick([Item("b", 20)], Stages, Now, ledger);
        var (got, _) = Reminders.Pick([Item("b", 20), Item("new", 5)], Stages, Now + 60, ledger);
        Assert.Equal("new", Assert.Single(got).Item.Id);
    }

    [Fact]
    public void LegacyLedgerKeysAreHonoured()
    {
        var ledger = new JsonObject { ["e1@24"] = Now - 100, ["e1@2"] = Now - 100 };
        var (got, _) = Reminders.Pick([Item("e1", 10)], Stages, Now, ledger);
        Assert.Empty(got);
    }

    [Fact]
    public void ManyAtOnceAreBatched()
    {
        var ledger = new JsonObject();
        var (got, _) = Reminders.Pick([Item("a", 3), Item("b", 4), Item("c", 5)], Stages, Now, ledger);
        Assert.Equal(3, got.Count);
        Assert.True(got.Count >= Reminders.SummaryFrom);
        Assert.Equal(["a", "b", "c"], got.Select(g => g.Item.Id));     // theo hạn
    }
}
