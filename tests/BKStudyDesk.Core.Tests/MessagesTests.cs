using SoHocTap.Data;
using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

/// <summary>Trang Tin nhắn, thẻ Tin nhắn mới ở Hôm nay và thông báo tin mới (MessagesPresenter).</summary>
public class MessagesTests
{
    private const long Now = 1_793_400_000, Day = 86_400;

    private static LmsMessage Msg(long id, long time, bool mine = false, string text = "<p>Chào em</p>") =>
        new(id, mine ? 1 : 2, mine ? null : "Cô Lan", mine ? true : null, text, time);

    private static LmsConversation Conv(long id, int unread, params LmsMessage[] msgs) =>
        new(id, "Cô Lan " + id, null, ["Cô Lan"], unread, msgs.Max(m => m.Time), null, [.. msgs]);

    [Fact]
    public void Rows_newest_first_with_preview_of_last_message()
    {
        var data = new LmsMessages(Now, [Conv(1, 0, Msg(1, Now - 5 * Day)), Conv(2, 1, Msg(2, Now - Day), Msg(3, Now - 100, mine: true, text: "Dạ em cảm ơn cô"))]);
        var rows = MessagesPresenter.Rows(data);
        Assert.Equal([2L, 1L], rows.Select(r => r.Id));
        Assert.Equal("Bạn: Dạ em cảm ơn cô", rows[0].Preview);
        Assert.True(rows[0].HasUnread);
    }

    [Fact]
    public void Filter_matches_name_or_text_without_accents()
    {
        var data = new LmsMessages(Now, [Conv(1, 0, Msg(1, Now, text: "Nộp bài trễ")), Conv(2, 0, Msg(2, Now))]);
        Assert.Equal([1L], MessagesPresenter.Rows(data, "nop bai").Select(r => r.Id));
    }

    [Fact]
    public void Recent_keeps_unread_or_new_from_others()
    {
        var data = new LmsMessages(Now, [
            Conv(1, 0, Msg(1, Now - 30 * Day)),                 // cũ, đã đọc: không hiện
            Conv(2, 2, Msg(2, Now - 30 * Day)),                 // cũ nhưng chưa đọc: hiện
            Conv(3, 0, Msg(3, Now - Day)),                      // mới từ người khác: hiện
            Conv(4, 0, Msg(4, Now - Day, mine: true)),          // chỉ có tin mình gửi: không hiện
        ]);
        Assert.Equal([3L, 2L], MessagesPresenter.Recent(data, Now, 7).Select(r => r.Id));
    }

    [Fact]
    public void Fresh_skips_first_run_then_reports_only_newer_messages_from_others()
    {
        var data = new LmsMessages(Now, [Conv(1, 0, Msg(1, Now - Day), Msg(2, Now - 10, mine: true), Msg(3, Now - 5))]);
        var (first, seen) = MessagesPresenter.Fresh(data, null);
        Assert.Empty(first);
        Assert.Equal(Now - 5, seen);
        var (fresh, next) = MessagesPresenter.Fresh(data, Now - Day);
        Assert.Equal([("Cô Lan", "Chào em")], fresh);
        Assert.Equal(Now - 5, next);
        Assert.Empty(MessagesPresenter.Fresh(data, next).Fresh);
    }

    [Fact]
    public void Thread_groups_by_day_and_sender_like_lms()
    {
        var day1 = SoHocTap.Core.VnTime.FromWall(new DateTime(2026, 10, 26, 11, 0, 0));
        var day2 = SoHocTap.Core.VnTime.FromWall(new DateTime(2026, 11, 5, 9, 0, 0));
        var c = new LmsConversation(1, "Nhóm GT2", true, ["Cô Như"], 0, day2, null,
        [
            new(1, 7, "Cô Như", null, "a", day1),
            new(2, 7, "Cô Như", null, "b", day1 + 300),          // cùng người, 5 phút sau: cùng nhóm
            new(3, 9, "Bạn", true, "c", day1 + 600),             // mình
            new(4, 7, "Cô Như", null, "d", day1 + 600 + 3 * 3600), // người khác, sau 3 giờ: nhóm mới
            new(5, 7, "Cô Như", null, "e", day2),               // ngày khác: vạch ngày mới
        ]);
        var (items, older) = MessagesPresenter.Thread(c, day2 + 60);
        Assert.Equal(0, older);
        Assert.Equal(["day", "group:2", "group:1", "group:1", "day", "group:1"],
            items.Select(x => x is ThreadDay ? "day" : "group:" + ((ThreadGroup)x).Messages.Count));
        Assert.True(((ThreadGroup)items[2]).Mine);
        Assert.Equal(SoHocTap.Ui.L.T("messages.today"), ((ThreadDay)items[4]).Text);
        Assert.Equal("11:05", ((ThreadGroup)items[1]).Messages[1].Hm);

        var (lastTwo, rest) = MessagesPresenter.Thread(c, day2 + 60, last: 2);
        Assert.Equal(3, rest);
        Assert.Equal(2, lastTwo.OfType<ThreadGroup>().Sum(g => g.Messages.Count));
    }
}
