using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace SoHocTap.Shell;

internal sealed class MessageNotifier(Action<string, string, string> show)
{
    private static string StateFile => Paths.DataFile("notified.json");

    public void Check()
    {
        try { CheckCore(); }
        catch (Exception e) { Log.Error("Báo tin nhắn mới", e); }
    }

    public static void Forget()
    {
        var ledger = JsonStore.ReadObject(StateFile);
        if (ledger.Remove("messagesSeen")) JsonStore.Write(StateFile, ledger);
    }

    private void CheckCore()
    {
        if (DeadlineNotifier.MutedUntil() is not null) return;
        var ledger = JsonStore.ReadObject(StateFile);
        long? seen = ledger["messagesSeen"] is JsonValue v && v.TryGetValue<long>(out var t) ? t : null;
        var (fresh, mark) = MessagesPresenter.Fresh(LmsMessagesStore.Read(), seen);
        if (mark is { } m && m != seen)
        {
            ledger["messagesSeen"] = m;
            JsonStore.Write(StateFile, ledger);
        }
        if (fresh.Count == 1) show(L.F("notify.message.one", fresh[0].From), Short(fresh[0].Text), "hop-thu");
        else if (fresh.Count > 1) show(L.F("notify.message.many", fresh.Count), string.Join(", ", fresh.Select(f => f.From).Distinct()), "hop-thu");
    }

    private static string Short(string text) => text.Length > 140 ? text[..140] + "..." : text;
}
