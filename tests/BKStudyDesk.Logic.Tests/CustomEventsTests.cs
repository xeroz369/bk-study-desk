using System.Text.Json.Nodes;
using SoHocTap.Data;

namespace SoHocTap.Tests;

/// <summary>Lưu sự kiện tự thêm (data/custom-events.json): round-trip qua file tạm, đọc chịu lỗi, tìm môn trong tiêu đề.</summary>
public sealed class CustomEventsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bk-custom-events-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "custom-events.json");

    public CustomEventsTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private CustomEventStore NewStore() => new(() => FilePath, (p, n) => File.WriteAllText(p, n.ToJsonString()));

    private static CustomEvent Sample(string title = "Học bù GT2") => new()
    {
        Title = title, Date = "2026-10-12", Start = "07:00", End = "08:50", Location = "H1-201", Kind = CustomEvents.KindMakeup, Note = "Mang máy tính",
    };

    [Fact]
    public void Save_AssignsIdAndCreatedAt_AndRoundTrips()
    {
        var store = NewStore();
        Assert.Empty(store.All());                               // chưa có file: rỗng, không lỗi
        Assert.Null(store.Problem);
        var saved = store.Save(Sample());
        Assert.Equal(32, saved.Id.Length);
        Assert.True(saved.CreatedAt > 0);

        var again = NewStore().All();                            // store mới: đọc từ đĩa
        var e = Assert.Single(again);
        Assert.Equal(saved, e);
        Assert.True(e.IsMakeup);
        Assert.Equal(new DateTime(2026, 10, 12, 7, 0, 0), e.StartWall);
        Assert.Equal(530, e.EndMin);
    }

    [Fact]
    public void File_UsesCamelCase_AndNoComputedFields()
    {
        NewStore().Save(Sample());
        var json = JsonNode.Parse(File.ReadAllText(FilePath))!.AsObject();
        Assert.Equal(1, json["version"]!.GetValue<int>());
        var e = json["events"]![0]!.AsObject();
        Assert.Equal(["id", "title", "date", "start", "end", "location", "kind", "note", "createdAt"], e.Select(p => p.Key).ToArray());
        Assert.Equal("Học bù GT2", e["title"]!.GetValue<string>());
    }

    [Fact]
    public void Save_SameId_Replaces_Remove_Deletes()
    {
        var store = NewStore();
        var a = store.Save(Sample("A"));
        var b = store.Save(Sample("B") with { Date = "2026-10-05" });
        Assert.Equal(["B", "A"], store.All().Select(x => x.Title).ToArray());   // sắp theo ngày
        store.Save(a with { Title = "A2" });
        Assert.Equal(2, store.All().Count);
        Assert.Equal("A2", store.Find(a.Id)!.Title);
        Assert.Equal(a.CreatedAt, store.Find(a.Id)!.CreatedAt);              // sửa không đổi giờ tạo
        Assert.True(store.Remove(b.Id));
        Assert.False(store.Remove(b.Id));
        Assert.Equal("A2", Assert.Single(store.All()).Title);
    }

    [Fact]
    public void Parse_Lenient()
    {
        const string json = """
            { "version": 1, "events": [
              null,
              { "id": "x1", "title": "  Họp  ", "date": "2026-10-05", "start": "19:00", "kind": "weird", "createdAt": "1760000000" },
              { "title": "không có id" },
              { "id": "x2", "title": "Sai ngày", "date": "05/10", "start": "7h", "end": null, "location": null }
            ] }
            """;
        var list = CustomEvents.Parse(json);
        Assert.Equal(2, list.Count);
        var x1 = list.Single(e => e.Id == "x1");
        Assert.Equal("Họp", x1.Title);
        Assert.Equal(CustomEvents.KindEvent, x1.Kind);   // kind lạ: coi là sự kiện thường
        Assert.Equal(1760000000, x1.CreatedAt);
        Assert.Equal(20 * 60, x1.EndMin);                // không có giờ kết thúc: dài một tiếng
        var x2 = list.Single(e => e.Id == "x2");
        Assert.Null(x2.Day);                             // ngày sai: giữ lại nhưng không đặt lên lịch
        Assert.Equal("", x2.Location);
        Assert.Equal("", x2.End);
    }

    [Fact]
    public void BrokenFile_NotOverwritten()
    {
        File.WriteAllText(FilePath, "{ this is not json");
        var store = NewStore();
        Assert.Empty(store.All());
        Assert.NotNull(store.Problem);
        Assert.Throws<InvalidOperationException>(() => store.Save(Sample()));
        Assert.Equal("{ this is not json", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Ics_IncludesCustomEvent()
    {
        var ev = Sample() with { Id = "abc", Title = "Học bù GT2, chương 3", Location = "H1-201; tầng 2" };
        var ics = CustomEvents.ToIcs(ev, "Học bù");
        Assert.NotNull(ics);
        Assert.Equal(new DateTime(2026, 10, 12, 7, 0, 0), ics.Start);
        Assert.Equal(new DateTime(2026, 10, 12, 8, 50, 0), ics.End);
        var text = SoHocTap.Core.Ics.Build("BK Study Desk", [ics], new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc));
        Assert.Contains("UID:ce-abc@bkstudydesk\r\n", text, StringComparison.Ordinal);
        Assert.Contains("DTSTART:20261012T000000Z\r\n", text, StringComparison.Ordinal);    // 07:00 giờ VN = 00:00 UTC
        Assert.Contains("DTEND:20261012T015000Z\r\n", text, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:Học bù GT2\\, chương 3\r\n", text, StringComparison.Ordinal);
        Assert.Contains("LOCATION:H1-201\\; tầng 2\r\n", text, StringComparison.Ordinal);
        Assert.Contains("DESCRIPTION:Học bù\\nMang máy tính\r\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ics_NoEnd_OneHour_BadDate_Skipped()
    {
        var noEnd = CustomEvents.ToIcs(Sample() with { Id = "a", End = "", Kind = CustomEvents.KindEvent, Note = "" }, "Học bù");
        Assert.Equal(new DateTime(2026, 10, 12, 8, 0, 0), noEnd!.End);
        Assert.True(string.IsNullOrEmpty(noEnd.Description));
        Assert.Null(CustomEvents.ToIcs(Sample() with { Id = "b", Date = "12/10" }, "Học bù"));
        Assert.Null(CustomEvents.ToIcs(Sample() with { Id = "c", Start = "sáng" }, "Học bù"));
    }

    [Fact]
    public void DueSoon_OneHourWindow_DedupByIdAndTime()
    {
        var ev = Sample() with { Id = "x" };
        var start = CustomEvents.StartTime(ev)!.Value;
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), start);
        Assert.Empty(CustomEvents.DueSoon([ev], start - 3601, 3600, _ => false));          // còn hơn 1 giờ
        var due = Assert.Single(CustomEvents.DueSoon([ev], start - 3000, 3600, _ => false));
        Assert.Equal("ce-x@2026-10-12T07:00", due.Key);
        Assert.Empty(CustomEvents.DueSoon([ev], start - 3000, 3600, k => k == due.Key));    // đã nhắc
        Assert.Empty(CustomEvents.DueSoon([ev], start, 3600, _ => false));                  // đã bắt đầu
        var moved = ev with { Start = "07:30" };                                               // sửa giờ: nhắc lại
        Assert.Single(CustomEvents.DueSoon([moved], start, 3600, k => k == due.Key));
    }

    [Fact]
    public void MatchCourse_LongestNameOrCode()
    {
        var courses = new[] { ("MT1003", "Giải tích 1"), ("MT1005", "Giải tích 2"), ("CO1023", "Hệ thống số"), ("MT", "") };
        Assert.Equal(("MT1005", "Giải tích 2"), CustomEvents.MatchCourse("Học bù giải tích 2 - cô Như", courses));
        Assert.Equal(("CO1023", "Hệ thống số"), CustomEvents.MatchCourse("Học bù co1023", courses));
        Assert.Null(CustomEvents.MatchCourse("Học bù Vật lý", courses));
        Assert.Null(CustomEvents.MatchCourse("MT", courses));   // mã quá ngắn không tính
    }
}
