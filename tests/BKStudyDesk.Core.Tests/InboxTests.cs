using System.Text.Json.Nodes;
using SoHocTap.Data;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests;

/// <summary>Hộp thư (Inbox, InboxReads) và gom việc giống nhau ở Hôm nay (TodoGroups).</summary>
public class InboxTests
{
    private const long Now = 1_793_400_000, Day = 86_400;

    private static LmsAnnouncement News(string id, long time, string kind = "news") =>
        new(id, kind, "Giải tích 2", "Các thông báo", "Nghỉ học thứ Hai", "Cô Lan", time, null, "<p>Lớp nghỉ</p>");

    private static LmsConversation Conv(long id, int unread, long time, bool mine = false) =>
        new(id, "Cô Lan", null, ["Cô Lan"], unread, time, null, [new(id, mine ? 1 : 2, "Cô Lan", mine ? true : null, "Chào em", time)]);

    [Fact]
    public void Rows_mix_messages_and_news_newest_first_with_unread()
    {
        var rows = Inbox.Rows(new LmsMessages(Now, [Conv(1, 1, Now - Day)]), [News("fd1", Now - 100), News("nt1", Now - 30 * Day, "system")], new InboxReads(new JsonObject()), Now, 7);
        Assert.Equal(["news:fd1", "msg:1", "news:nt1"], rows.Select(r => r.Key));
        Assert.Equal([true, true, false], rows.Select(r => r.Unread));
        Assert.Equal(InboxKind.System, rows[2].Kind);
    }

    [Fact]
    public void Reading_in_app_clears_unread_until_a_newer_message_arrives()
    {
        var reads = new InboxReads(new JsonObject());
        Assert.True(reads.Mark("msg:1", Now - Day));
        Assert.False(Inbox.Rows(new LmsMessages(Now, [Conv(1, 1, Now - Day)]), [], reads, Now, 7)[0].Unread);
        Assert.True(Inbox.Rows(new LmsMessages(Now, [Conv(1, 1, Now)]), [], reads, Now, 7)[0].Unread);
    }

    [Fact]
    public void Filter_by_kind_and_text()
    {
        var msgs = new LmsMessages(Now, [Conv(1, 0, Now)]);
        IReadOnlyList<LmsAnnouncement> news = [News("fd1", Now)];
        var rows = Inbox.Rows(msgs, news, new InboxReads(new JsonObject()), Now, 7);
        var index = Inbox.SearchIndex(msgs, news);
        Assert.Equal(["msg:1"], Inbox.Filter(rows, InboxKind.Message, "", index).Select(r => r.Key));
        Assert.Equal(["news:fd1"], Inbox.Filter(rows, null, "lop nghi", index).Select(r => r.Key));
    }

    private static TodoItem Todo(string id, string title, string sub) =>
        new(id, new DateTime(2026, 11, 9), title, sub, "còn 9 ngày", Urgency.None, "", "u");

    [Fact]
    public void Similar_todos_collapse_into_one_row_and_expand()
    {
        var items = new[]
        {
            Todo("a", "[261] KHẢO SÁT HỌC PHẦN/ STUDENT SATISFACTION SURVEY ON COURSE Vật lý", "Vật lý, Đóng"),
            Todo("b", "[261] KHẢO SÁT HỌC PHẦN/ STUDENT SATISFACTION SURVEY ON COURSE Giải tích 2", "Giải tích 2, Đóng"),
            Todo("c", "[261] KHẢO SÁT HỌC PHẦN/ STUDENT SATISFACTION SURVEY ON COURSE Kỹ thuật số", "Kỹ thuật số, Đóng"),
            Todo("d", "[261] Nộp bài PreLab #4", "Kỹ thuật số, Hạn nộp"),
        };
        var collapsed = TodoGroups.Collapse(items, new HashSet<string>());
        Assert.Equal(2, collapsed.Count);
        Assert.True(collapsed[0].Group);
        Assert.StartsWith("KHẢO SÁT HỌC PHẦN (3", collapsed[0].Title);
        Assert.Equal("Vật lý, Giải tích 2, Kỹ thuật số", collapsed[0].Sub);
        Assert.Equal("Nộp bài PreLab #4", collapsed[1].Title);
        var open = TodoGroups.Collapse(items, new HashSet<string> { collapsed[0].Id });
        Assert.Equal(5, open.Count);
        Assert.True(open[1].Indent);
    }
}
