using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Ui;

namespace SoHocTap.Shell;

/// <summary>
/// Nhắc hạn nộp và quiz LMS bằng notification Windows (qua tray icon). Chạy phía app, không phụ thuộc UI,
/// nên vẫn nhắc được khi window đang thu nhỏ (WebView2 bị tạm dừng). Mỗi mốc nhắc hai lần: trước notify.hoursBefore giờ
/// và trước notify.lastHours giờ; mỗi lần gộp thành một thông báo, ghi lại vào data/notified.json.
/// </summary>
internal sealed class DeadlineNotifier(Action<string, string, string> show)
{
    private static string StateFile => Paths.DataFile("notified.json");

    private sealed record Due(string Id, string Title, string Subject, long Time);

    public void Check()
    {
        try { Run(); }
        catch (Exception e) { Log.Error("Nhắc hạn", e); }
    }

    private void Run()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var stages = new[] { Config.Int("notify.hoursBefore", 24), Config.Int("notify.lastHours", 2) }.Where(h => h > 0).Distinct().OrderByDescending(h => h).ToArray();
        if (stages.Length == 0) return;
        var items = Collect(now, stages[0] * 3600L);
        if (items.Count == 0) return;

        var sent = JsonStore.ReadObject(StateFile);
        var fresh = new List<(Due Item, int Hours)>();
        foreach (var d in items)
        {
            // Lấy mốc gần nhất mà mục này đã lọt vào; mốc xa hơn coi như đã qua (mở app trễ thì chỉ nhắc một lần).
            var stage = stages.Last(h => d.Time - now <= h * 3600L);
            var key = $"{d.Id}@{stage}";
            if (sent.ContainsKey(key)) continue;
            foreach (var h in stages.Where(h => h >= stage)) sent[$"{d.Id}@{h}"] = now;
            fresh.Add((d, stage));
        }
        if (fresh.Count == 0) return;

        // Bỏ bản ghi cũ hơn 30 ngày.
        foreach (var k in sent.Where(p => p.Value?.GetValue<long>() < now - 30 * 86400).Select(p => p.Key).ToList()) sent.Remove(k);
        JsonStore.Write(StateFile, sent);

        fresh.Sort((a, b) => a.Item.Time.CompareTo(b.Item.Time));
        if (fresh.Count == 1)
        {
            var d = fresh[0].Item;
            show(d.Title, L.F("notify.left", d.Subject, Left(d.Time - now)), "lich");
        }
        else
        {
            var names = string.Join("\n", fresh.Take(3).Select(f => L.F("notify.left", f.Item.Title, Left(f.Item.Time - now))));
            show(L.F("notify.many", fresh.Count), names + (fresh.Count > 3 ? "\n" + L.F("notify.more", fresh.Count - 3) : ""), "lich");
        }
    }

    private static List<Due> Collect(long now, long window)
    {
        var list = new List<Due>();
        if (LmsStore.Read() is not { } lms) return list;
        foreach (var e in lms.Events)
        {
            if (e.Kind == "quiz" || e.Done) continue;
            if (e.Time > now && e.Time - now <= window)
                list.Add(new Due(e.Id ?? "", L.F("notify.title", e.Label ?? L.T("notify.due"), e.Name), e.Subject ?? "", e.Time));
        }
        foreach (var q in lms.Quizzes)
        {
            if (q.Attempts.Count > 0 || q.Close is not { } t) continue;
            if (t > now && t - now <= window)
                list.Add(new Due("qz" + q.Id, L.F("notify.quizClose", q.Name), q.Subject ?? "", t));
        }
        return list;
    }

    private static string Left(long sec) => sec >= 86400 ? L.F("notify.daysHours", sec / 86400, sec % 86400 / 3600) : sec >= 3600 ? L.F("notify.hours", sec / 3600) : L.F("notify.minutes", Math.Max(1, sec / 60));
}
