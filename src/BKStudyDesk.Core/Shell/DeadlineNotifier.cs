using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Ui;

namespace SoHocTap.Shell;

/// <summary>
/// Nhắc hạn nộp và quiz LMS bằng notification Windows (qua tray icon). Chạy phía app, không phụ thuộc UI,
/// nên vẫn nhắc được khi window đang thu nhỏ (WebView2 bị tạm dừng). Mỗi mốc nhắc hai lần: trước notify.hoursBefore giờ
/// và trước notify.lastHours giờ; mỗi lần gộp thành một thông báo, ghi lại vào data/notified.json (chống trùng qua lần khởi động
/// lại: <see cref="Reminders"/>). Có giờ gom (notify.digestTimes) thì bài còn xa hạn chờ tới giờ gom, bài sắp tới hạn (dưới mốc Rất gấp
/// hay mốc nhắc cuối) vẫn báo ngay (<see cref="NotifyDigest"/>). Không bao giờ thông báo "đồng bộ xong"; lỗi đồng bộ chỉ hiện trên InfoBar và thanh trạng thái.
/// </summary>
internal sealed class DeadlineNotifier(Action<string, string, string> show)
{
    private static string StateFile => Paths.DataFile("notified.json");

    public void Check()
    {
        try { Run(); }
        catch (Exception e) { Log.Error("Nhắc hạn", e); }
        try { RunCustom(); }
        catch (Exception e) { Log.Error("Nhắc sự kiện tự thêm", e); }
    }

    // Sự kiện tự thêm (issue #22): nhắc một lần trước 1 giờ. File đánh dấu riêng để không đụng cách chống trùng của notified.json.
    private static string CustomStateFile => Paths.DataFile("notified-custom.json");

    private void RunCustom()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var events = CustomEventsData.Store.All();
        if (events.Count == 0) return;
        var sent = JsonStore.ReadObject(CustomStateFile);
        var due = CustomEvents.DueSoon(events, now, 3600, sent.ContainsKey);
        if (due.Count == 0) return;
        foreach (var d in due) sent[d.Key] = now;
        foreach (var k in sent.Where(p => p.Value?.GetValue<long>() < now - 30 * 86400).Select(p => p.Key).ToList()) sent.Remove(k);
        JsonStore.Write(CustomStateFile, sent);
        // Thường chỉ một sự kiện trong một giờ tới; nhiều hơn thì cũng chỉ hiện 3 cái đầu, tránh dội thông báo.
        foreach (var (e, time, _) in due.Take(3))
        {
            var place = e.Location.Length > 0 ? e.Location : L.T(e.IsMakeup ? "kind.makeup" : "kind.custom");
            show(e.Title, L.F("notify.left", L.F("notify.custom", place, e.Start), Due.SpanText(time - now)), "lich");
        }
    }

    private void Run()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int[] stages = [Core.Settings.Notify.FirstHours, Core.Settings.Notify.LastHours];
        // Chọn mốc và chống trùng (khóa nguồn:id:mức:giờ hạn, mốc nước) ở Reminders; sổ chỉ ghi khi đổi (JsonStore bỏ qua nếu y như cũ).
        var ledger = JsonStore.ReadObject(StateFile);
        var slots = NotifyDigest.Parse(Core.Settings.Notify.DigestTimes) ?? [];   // sai dạng (sửa tay config) thì báo ngay như trước
        var gate = NotifyDigest.GateOpen(now, slots, ledger["digestAt"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<long>(out var at) ? at : 0);
        var immediate = Math.Max(Core.Settings.Notify.UrgentHours, Core.Settings.Notify.LastHours);
        var (fresh, changed) = Reminders.Pick(Collect(), stages, now, ledger, gate ? null : d => !NotifyDigest.Hold(d.Time, now, immediate));
        // Đã tới giờ gom: ghi lại để bài mới xuất hiện sau đó chờ giờ gom kế tiếp.
        if (gate && slots.Length > 0) { ledger["digestAt"] = now; changed = true; }
        if (changed) JsonStore.Write(StateFile, ledger);
        if (fresh.Count == 0) return;

        if (fresh.Count < Reminders.SummaryFrom)
        {
            var d = fresh[0].Item;
            show(d.Title, L.F("notify.left", d.Subject, Due.SpanText(d.Time - now)), "lich");
        }
        else
        {
            var names = string.Join("\n", fresh.Take(3).Select(f => L.F("notify.left", f.Item.Title, Due.SpanText(f.Item.Time - now))));
            show(L.F("notify.many", fresh.Count), names + (fresh.Count > 3 ? "\n" + L.F("notify.more", fresh.Count - 3) : ""), "lich");
        }
    }

    /// <summary>Mọi mốc LMS chưa xong (hạn nộp, quiz chưa làm có giờ đóng); Reminders tự lọc theo khung nhắc.</summary>
    private static List<DueItem> Collect()
    {
        var list = new List<DueItem>();
        if (LmsStore.Read() is not { } lms) return list;
        foreach (var e in lms.Events)
            if (e.Kind != "quiz" && !e.Done && !string.IsNullOrEmpty(e.Id))
                list.Add(new DueItem(SourceIds.Lms, e.Id, L.F("notify.title", e.Label ?? L.T("notify.due"), e.Name), e.Subject ?? "", e.Time));
        foreach (var q in lms.Quizzes)
            if (q.Attempts.Count == 0 && q.Close is { } t)
                list.Add(new DueItem(SourceIds.Lms, "qz" + q.Id, L.F("notify.quizClose", q.Name), q.Subject ?? "", t));
        return list;
    }

}
