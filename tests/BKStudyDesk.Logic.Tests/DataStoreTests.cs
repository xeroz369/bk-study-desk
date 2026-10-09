using System.Text.Json.Nodes;
using SoHocTap.Data;

namespace SoHocTap.Tests;

/// <summary>Đọc lms.json, mybk.json chịu lỗi (DataJson) và cache theo mtime (CachedFile). Toàn dữ liệu mẫu, file tạm.</summary>
public class DataStoreTests
{
    [Fact]
    public void Lenient_NumbersAsStrings_DecimalsAndNulls()
    {
        const string json = """
            {
              "syncedAt": "1760000000", "user": 123, "term": "HK261",
              "courses": [ { "id": "42", "name": "Lớp", "subject": "Môn", "code": "MT0001", "term": "HK261" } ],
              "events": [
                { "id": "ev1", "course": 42.0, "subject": "Môn", "name": "Bài", "kind": "assign", "time": null, "label": "Hạn", "done": "true" },
                null,
                { "id": "ev2", "course": 42, "subject": "Môn", "name": "Bài 2", "kind": "assign", "time": 1760000000.6, "label": "Hạn", "done": 1 }
              ],
              "quizzes": [ { "id": 7, "course": 42, "subject": "Môn", "name": "Quiz", "open": "", "close": "1760000000", "attempts": null, "url": "x" } ],
              "grades": [ { "course": 42, "subject": "Môn", "items": [ { "name": "Q1", "grade": "8,5", "max": "10" }, { "name": "Q2", "grade": { "x": 1 } } ] } ]
            }
            """;
        var d = DataJson.Parse<LmsData>(json)!.Normalize();
        Assert.Equal(1760000000, d.SyncedAt);
        Assert.Equal("123", d.User);
        Assert.Equal(42, d.Courses[0].Id);
        Assert.Equal(2, d.Events.Count);                     // phần tử null bị bỏ
        Assert.Equal(0, d.Events[0].Time);                   // null ở chỗ số không làm hỏng cả file
        Assert.True(d.Events[0].Done);
        Assert.Equal(1760000001, d.Events[1].Time);          // số thực làm tròn
        Assert.True(d.Events[1].Done);
        Assert.Null(d.Quizzes[0].Open);
        Assert.Equal(1760000000, d.Quizzes[0].Close);
        Assert.Empty(d.Quizzes[0].Attempts);                 // null thành danh sách rỗng
        Assert.Null(d.Grades![0].Items[0].Grade);            // "8,5" không phải số kiểu invariant: null, không throw
        Assert.Equal(10, d.Grades[0].Items[0].Max);
        Assert.Null(d.Grades[0].Items[1].Grade);             // object ở chỗ số: bỏ qua
    }

    [Fact]
    public void Lenient_MissingLists_BecomeEmpty()
    {
        var d = DataJson.Parse<MybkData>("""{ "syncedAt": 1, "schedule": [ { "code": "A", "day": "3", "weeks": null } ] }""")!.Normalize();
        Assert.Equal(3, d.Schedule[0].Day);
        Assert.Empty(d.Schedule[0].Weeks);
        Assert.Empty(d.Exams);
        Assert.Empty(d.Grades);
        Assert.Equal("", d.Student.Name);
        Assert.Equal("", d.Term.Code);
    }

    [Fact]
    public void BrokenJson_ReportsPath()
    {
        var e = Assert.Throws<DataReadException>(() => DataJson.Parse<LmsData>("""{ "syncedAt": 1, "events": { "not": "an array" } }"""));
        Assert.Contains("$.events", e.Message, StringComparison.Ordinal);
        Assert.Throws<DataReadException>(() => DataJson.Parse<LmsData>("{ \"syncedAt\": "));
    }

    [Fact]
    public void CachedFile_ReparsesOnlyWhenFileChanges()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cachedfile-{Guid.NewGuid():N}.json");
        var parses = 0;
        var cache = new CachedFile<JsonObject>(() => path, t => { parses++; return JsonNode.Parse(t) as JsonObject; });
        try
        {
            Assert.Null(cache.Get());                          // chưa có file
            File.WriteAllText(path, """{ "a": 1 }""");
            Assert.Equal(1, cache.Get()!["a"]!.GetValue<int>());
            Assert.Equal(1, cache.Get()!["a"]!.GetValue<int>());
            Assert.Equal(1, parses);                            // lần hai lấy từ cache

            File.WriteAllText(path, """{ "a": 22 }""");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
            Assert.Equal(22, cache.Get()!["a"]!.GetValue<int>());
            Assert.Equal(2, parses);

            File.WriteAllText(path, "{ hỏng");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(10));
            Assert.Null(cache.Get());
            Assert.NotNull(cache.Problem);

            File.Delete(path);
            Assert.Null(cache.Get());
            Assert.Null(cache.Problem);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void CachedFile_Invalidate_ForcesReread()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cachedfile-{Guid.NewGuid():N}.json");
        var parses = 0;
        var cache = new CachedFile<JsonObject>(() => path, t => { parses++; return JsonNode.Parse(t) as JsonObject; });
        try
        {
            File.WriteAllText(path, "{}");
            cache.Get();
            cache.Invalidate();
            cache.Get();
            Assert.Equal(2, parses);
        }
        finally { File.Delete(path); }
    }
}
