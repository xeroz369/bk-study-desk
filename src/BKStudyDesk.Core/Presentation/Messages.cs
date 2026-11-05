using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>Một hội thoại trong danh sách bên trái trang Tin nhắn. Preview: tin cuối, một dòng.</summary>
public sealed record ConversationRow(long Id, string Name, string Preview, string When, int Unread, long LastTime)
{
    public bool HasUnread => Unread > 0;
    public string UnreadText => Unread > 0 ? Unread.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
}

/// <summary>Một tin trong hội thoại. Who: tên người gửi, tin của mình thì là "Bạn".</summary>
public sealed record MessageRow(string Who, string When, bool Mine, IReadOnlyList<string> Paragraphs, IReadOnlyList<LmsLink> Links,
    IReadOnlyList<ReadingBlock>? Blocks = null);

/// <summary>Một mục trong khung hội thoại kiểu LMS: vạch ngày hay một nhóm tin liền nhau của cùng một người.</summary>
public abstract record ThreadItem;

/// <summary>Vạch ngăn ngày: "Hôm nay", "Hôm qua", "Thứ hai 26/10" (năm khác thì thêm năm).</summary>
public sealed record ThreadDay(string Text) : ThreadItem;

/// <summary>Các tin liền nhau của cùng một người trong cùng ngày, cách nhau không quá 30 phút: tên ghi một lần, mỗi tin ghi giờ.</summary>
public sealed record ThreadGroup(string Who, bool Mine, IReadOnlyList<ThreadMessage> Messages) : ThreadItem;

public sealed record ThreadMessage(long Time, string Hm, IReadOnlyList<ReadingBlock> Blocks);

/// <summary>
/// Trang Tin nhắn: tin nhắn riêng trên LMS đã lưu (data/lms-messages.json), đọc được khi LMS sập. Chỉ đọc: trả lời trên LMS.
/// Hội thoại mới nhất lên đầu; tin trong hội thoại theo thời gian, cũ trước.
/// </summary>
public static class MessagesPresenter
{
    /// <param name="index">Chữ đã bỏ dấu của từng hội thoại (<see cref="SearchIndex"/>): dựng một lần mỗi lần đọc file, không dựng lại mỗi phím gõ.</param>
    public static IReadOnlyList<ConversationRow> Rows(LmsMessages? data, string filter = "", IReadOnlyDictionary<long, string>? index = null)
    {
        var rows = (data?.Conversations ?? [])
            .OrderByDescending(c => c.LastTime)
            .Select(c => new ConversationRow(c.Id, c.Name.Length > 0 ? c.Name : string.Join(", ", c.Members), Preview(c), c.LastTime > 0 ? Format.Ago(c.LastTime) : "", c.Unread, c.LastTime));
        var f = Files.NameMatch.Fold(filter.Trim());
        if (f.Length == 0) return [.. rows];
        index ??= SearchIndex(data);
        return [.. rows.Where(r => index.TryGetValue(r.Id, out var text) && text.Contains(f, StringComparison.Ordinal))];
    }

    /// <summary>Tên và chữ (đã bỏ thẻ HTML, bỏ dấu) của từng hội thoại, để lọc: gõ "div" không khớp mọi tin vì thẻ HTML.</summary>
    public static IReadOnlyDictionary<long, string> SearchIndex(LmsMessages? data) =>
        (data?.Conversations ?? []).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => Files.NameMatch.Fold(string.Join(" ",
            g.SelectMany(c => new[] { c.Name, string.Join(" ", c.Members) }.Concat(c.Messages.SelectMany(m => LmsReading.Paragraphs(m.Text)))))));

    public static IReadOnlyList<MessageRow> Messages(LmsConversation c) =>
        [.. c.Messages.OrderBy(m => m.Time).Select(m => new MessageRow(
            m.Mine == true ? L.T("messages.you") : m.FromName ?? c.Name, Format.DateTime(m.Time), m.Mine == true,
            LmsReading.Paragraphs(m.Text), [.. LmsReading.Links(m.Text).Select(LmsReading.Browsable)], LmsReading.Blocks(m.Text)))];

    /// <summary>Hai tin cách nhau quá bấy nhiêu giây thì tách nhóm, dù cùng người gửi.</summary>
    public const long GroupGap = 30 * 60;

    /// <summary>
    /// Khung hội thoại như trên LMS: <paramref name="last"/> tin mới nhất (cũ trước), vạch ngăn mỗi ngày, tin liền nhau của cùng
    /// một người gộp nhóm. Older: số tin cũ hơn chưa hiện (nút Xem tin cũ hơn).
    /// </summary>
    public static (IReadOnlyList<ThreadItem> Items, int Older) Thread(LmsConversation c, long now, int last = int.MaxValue)
    {
        var all = c.Messages.OrderBy(m => m.Time).ToList();
        var shown = all.Skip(Math.Max(0, all.Count - last)).ToList();
        var items = new List<ThreadItem>();
        DateTime? day = null;
        List<ThreadMessage>? group = null;
        (bool Mine, long From) who = default;
        long lastTime = 0;
        foreach (var m in shown)
        {
            var d = Format.Local(m.Time).Date;
            var mine = m.Mine == true;
            if (d != day)
            {
                items.Add(new ThreadDay(DayText(d, Format.Local(now).Date)));
                day = d;
                group = null;
            }
            if (group is null || who != (mine, mine ? 0 : m.From) || m.Time - lastTime > GroupGap)
            {
                group = [];
                who = (mine, mine ? 0 : m.From);
                items.Add(new ThreadGroup(mine ? L.T("messages.you") : m.FromName ?? c.Name, mine, group));
            }
            group.Add(new ThreadMessage(m.Time, Format.Hm(m.Time), LmsReading.Blocks(m.Text)));
            lastTime = m.Time;
        }
        return (items, all.Count - shown.Count);
    }

    /// <summary>"Hôm nay", "Hôm qua", hay thứ và ngày (năm khác năm nay thì ghi thêm năm).</summary>
    public static string DayText(DateTime day, DateTime today) =>
        day == today ? L.T("messages.today") : day == today.AddDays(-1) ? L.T("messages.yesterday")
        : Format.DateLong(day) + (day.Year != today.Year ? "/" + day.Year.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");

    /// <summary>
    /// Thẻ Tin nhắn mới ở trang Hôm nay: hội thoại còn tin chưa đọc (theo LMS lúc đồng bộ) hay có tin của người khác trong
    /// <paramref name="days"/> ngày, mới nhất trước, tối đa <paramref name="max"/> hội thoại.
    /// </summary>
    public static IReadOnlyList<ConversationRow> Recent(LmsMessages? data, long now, int days, int max = 5)
    {
        var since = now - days * 86400L;
        var ids = (data?.Conversations ?? []).Where(c => c.Unread > 0 || c.Messages.Any(m => m.Mine != true && m.Time >= since)).Select(c => c.Id).ToHashSet();
        return [.. Rows(data).Where(r => ids.Contains(r.Id)).Take(max)];
    }

    /// <summary>
    /// Tin của người khác mới hơn <paramref name="seen"/> (giây Unix): để báo bằng thông báo hệ điều hành. Trả kèm mốc mới để lần sau
    /// không báo lại. Lần đầu (seen null) không báo gì, chỉ lấy mốc: không dội cả hộp thư cũ ra màn hình.
    /// </summary>
    public static (IReadOnlyList<(string From, string Text)> Fresh, long? Seen) Fresh(LmsMessages? data, long? seen)
    {
        var all = (data?.Conversations ?? []).SelectMany(c => c.Messages.Where(m => m.Mine != true).Select(m => (c, m))).ToList();
        // Chưa có tin nào (chưa đồng bộ được tin nhắn): giữ nguyên mốc. Lấy mốc 0 thì lần đồng bộ được sau báo cả hộp thư cũ.
        if (all.Count == 0) return ([], seen);
        var max = all.Max(x => x.m.Time);
        if (seen is not { } s) return ([], max);
        var fresh = all.Where(x => x.m.Time > s).OrderBy(x => x.m.Time)
            .Select(x => (x.m.FromName ?? x.c.Name, string.Join(" ", LmsReading.Paragraphs(x.m.Text)).Replace('\n', ' '))).ToList();
        return (fresh, Math.Max(max, s));
    }

    /// <summary>Số hội thoại có tin chưa đọc (theo LMS lúc đồng bộ): chấm trên thanh điều hướng, thẻ Hôm nay.</summary>
    public static int UnreadConversations(LmsMessages? data) => data?.Conversations.Count(c => c.Unread > 0) ?? 0;

    public static string PreviewOf(LmsConversation c) => Preview(c);

    private static string Preview(LmsConversation c)
    {
        if (c.Messages.Count == 0) return "";
        var last = c.Messages.MaxBy(m => m.Time)!;
        var text = string.Join(" ", LmsReading.Paragraphs(last.Text)).Replace('\n', ' ');
        if (text.Length > 90) text = text[..90] + "...";
        return last.Mine == true ? $"{L.T("messages.you")}: {text}" : text;
    }
}
