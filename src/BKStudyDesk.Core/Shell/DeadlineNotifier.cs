using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Shell;

internal sealed class DeadlineNotifier(Action<string, string, string> show)
{
    private static string StateFile => Paths.DataFile("notified.json");

    public static long? MutedUntil()
    {
        var at = JsonStore.ReadObject(StateFile)["muteUntil"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<long>(out var t) ? t : 0;
        return at > DateTimeOffset.UtcNow.ToUnixTimeSeconds() ? at : null;
    }

    public static void Mute(bool on)
    {
        var ledger = JsonStore.ReadObject(StateFile);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var morning = NotifyDigest.Parse(Core.Settings.Notify.Morning) is [var m, ..] ? m : 7 * 60;
        if (on) ledger["muteUntil"] = NotifyDigest.NextMorning(now, morning);
        else ledger.Remove("muteUntil");
        JsonStore.Write(StateFile, ledger);
    }

    public void Check()
    {
        if (MutedUntil() is not null) return;
        try { Run(); }
        catch (Exception e) { Log.Error("Nhắc hạn", e); }
        try { RunCustom(); }
        catch (Exception e) { Log.Error("Nhắc sự kiện tự thêm", e); }
    }

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
        var ledger = JsonStore.ReadObject(StateFile);
        var slots = NotifyDigest.Parse(Core.Settings.Notify.DigestTimes) ?? [];
        var gate = NotifyDigest.GateOpen(now, slots, ledger["digestAt"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<long>(out var at) ? at : 0);
        var immediate = Math.Max(Core.Settings.Notify.UrgentHours, Core.Settings.Notify.LastHours);
        var (fresh, changed) = Reminders.Pick(Collect(), stages, now, ledger, gate ? null : d => !NotifyDigest.Hold(d.Time, now, immediate));
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

    private static List<DueItem> Collect()
    {
        var list = new List<DueItem>();
        var lms = LmsStore.Read();
        foreach (var e in lms?.Events ?? [])
            if (e.Kind != "quiz" && e.Phase != "open" && !e.Done && !string.IsNullOrEmpty(e.Id))
                list.Add(new DueItem(SourceIds.Lms, e.Id, L.F("notify.title", e.Label ?? L.T("notify.due"), e.Name), e.Subject ?? "", e.Time));
        foreach (var q in lms?.Quizzes ?? [])
            if (q.Attempts.Count == 0 && q.Close is { } t)
                list.Add(new DueItem(SourceIds.Lms, "qz" + q.Id, L.F("notify.quizClose", q.Name), q.Subject ?? "", t));
        foreach (var x in MybkStore.Read()?.Exams ?? [])
            if (VnTime.ParseDate(x.Date) is { } day)
                list.Add(new DueItem(SourceIds.Mybk, $"ex-{x.Code}-{x.Type}-{x.Date}", L.F("notify.exam", x.Name), L.F("timeline.room", x.Room),
                    VnTime.FromWall(day.AddMinutes(VnTime.ParseClock(x.Time) ?? 0))));
        return list;
    }
}
