using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Files;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public enum InboxKind { Message, News, System }

public sealed record InboxRow(string Key, InboxKind Kind, string Title, string Sub, string Preview, string When, long Time, bool Unread, bool Quiet = false)
{
    public string KindText => L.T(Kind switch { InboxKind.Message => "inbox.kind.message", InboxKind.News => "inbox.kind.news", _ => "inbox.kind.system" });
}

public sealed class InboxReads(JsonObject saved)
{
    public static string FilePath => Paths.DataFile("inbox-read.json");

    public static InboxReads Load() => new(JsonStore.ReadObject(FilePath));

    public long? ReadAt(string key) => saved[key] is JsonValue v && v.TryGetValue<long>(out var t) ? t : null;

    public bool Mark(string key, long time)
    {
        if (ReadAt(key) is { } t && t >= time) return false;
        saved[key] = time;
        return true;
    }

    public bool Unmark(string key) => saved.Remove(key);

    public void Save() => JsonStore.Write(FilePath, saved);
}

public static class Inbox
{
    public static IReadOnlyList<InboxRow> Rows(LmsMessages? messages, IReadOnlyList<LmsAnnouncement> news, InboxReads reads, long now, int days)
    {
        var rows = new List<InboxRow>();
        var quietEvents = Config.List("sources.lms.quietEvents");
        foreach (var c in messages?.Conversations ?? [])
        {
            var key = "msg:" + c.Id;
            var lastOther = c.Messages.Where(m => m.Mine != true).Select(m => m.Time).DefaultIfEmpty(0).Max();
            var unread = reads.ReadAt(key) is { } at ? lastOther > at : c.Unread > 0;
            var name = c.Name.Length > 0 ? c.Name : string.Join(", ", c.Members);
            rows.Add(new(key, InboxKind.Message, name, "", MessagesPresenter.PreviewOf(c), c.LastTime > 0 ? Format.Ago(c.LastTime) : "", c.LastTime, unread));
        }
        foreach (var a in news)
        {
            var key = "news:" + a.Id;
            var quiet = a.Event is { Length: > 0 } ev && quietEvents.Contains(ev, StringComparer.OrdinalIgnoreCase);
            var unread = !quiet && reads.ReadAt(key) is null && a.Time > now - days * 86400L;
            var kind = a.Kind == "system" ? InboxKind.System : InboxKind.News;
            var preview = string.Join(" ", LmsReading.Paragraphs(a.Message)).Replace('\n', ' ');
            if (preview.Length > 90) preview = preview[..90] + "...";
            rows.Add(new(key, kind, a.Title, string.Join(", ", new[] { a.Subject, a.Author ?? "" }.Where(x => x.Length > 0)), preview, Format.Ago(a.Time), a.Time, unread, quiet));
        }
        return [.. rows.OrderByDescending(r => r.Time)];
    }

    public static IReadOnlyList<InboxRow> Filter(IReadOnlyList<InboxRow> rows, InboxKind? kind, string query, IReadOnlyDictionary<string, string> index)
    {
        var f = NameMatch.Fold(query.Trim());
        return [.. rows.Where(r => (kind is null || r.Kind == kind)
            && (f.Length == 0 || (index.TryGetValue(r.Key, out var text) ? text : NameMatch.Fold(r.Title)).Contains(f, StringComparison.Ordinal)))];
    }

    public static IReadOnlyDictionary<string, string> SearchIndex(LmsMessages? messages, IReadOnlyList<LmsAnnouncement> news)
    {
        var index = new Dictionary<string, string>();
        foreach (var (id, text) in MessagesPresenter.SearchIndex(messages)) index["msg:" + id] = text;
        foreach (var a in news)
            index["news:" + a.Id] = NameMatch.Fold(string.Join(" ", new[] { a.Title, a.Subject, a.Author ?? "" }.Concat(LmsReading.Paragraphs(a.Message))));
        return index;
    }

    public static IReadOnlyList<InboxRow> Recent(IReadOnlyList<InboxRow> rows, long now, int days, int max = 6) =>
        [.. rows.Where(r => !r.Quiet && (r.Unread || r.Time > now - days * 86400L)).Take(max)];

    public static int UnreadCount(IReadOnlyList<InboxRow> rows) => rows.Count(r => r.Unread);
}
