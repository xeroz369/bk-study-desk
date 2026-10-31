using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace SoHocTap.Shell;

/// <summary>
/// Báo tin nhắn riêng LMS mới bằng thông báo hệ điều hành, sau mỗi lượt đồng bộ LMS. Mốc đã báo lưu ở data/notified.json
/// (messagesSeen), chung file với nhắc hạn: tắt mở app không báo lại. Lần đầu chỉ lấy mốc, không báo cả hộp thư cũ.
/// Đang "tạm im tới sáng mai" thì chưa báo và chưa dời mốc, sáng ra báo bù. Nhiều tin thì gộp một thông báo.
/// </summary>
internal sealed class MessageNotifier(Action<string, string, string> show)
{
    private static string StateFile => Paths.DataFile("notified.json");

    public void Check()
    {
        try { CheckCore(); }
        catch (Exception e) { Log.Error("Báo tin nhắn mới", e); }   // notified.json bị khóa, ổ đầy: không làm hỏng việc sau lượt đồng bộ
    }

    /// <summary>Đăng xuất: quên mốc đã báo (người đăng nhập sau có hộp thư khác).</summary>
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
