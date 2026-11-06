using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>
/// Tình trạng một nguồn trong bảng Thông báo: dòng chữ (đang làm bước nào, xong lúc nào), vấn đề nếu có (lỗi, phần không đọc được),
/// chi tiết kỹ thuật, và có cần đăng nhập lại không (View hiện nút Đăng nhập lại, còn lại là Thử lại).
/// </summary>
/// <param name="Id">Tên nguồn trong SourceHub (SourceIds), để đồng bộ riêng nguồn này từ bảng Thông báo.</param>
/// <param name="Partial">Đồng bộ xong nhưng một phần không đọc được (cảnh báo): không tính là lỗi trên thanh trên cùng.</param>
/// <param name="Off">MyBK sync turned off in Settings (the View shows no sync or retry action for it).</param>
public sealed record SourceState(string Label, string State, string? Problem, string? Detail, bool Syncing, bool NeedsLogin, string Id = "", bool Partial = false,
    bool Off = false);

/// <summary>Một thông báo trong app (nhắc hạn, có bản mới, đăng nhập xong); Page là trang mở khi bấm.</summary>
public sealed record Notice(long Time, string Title, string Body, string Page);

/// <summary>
/// Trạng thái đồng bộ không chiếm chỗ trên trang (cùng nội dung với thanh trạng thái của 1.x): thanh trên cùng chỉ có một nút chữ ngắn,
/// bấm vào là bảng liệt kê từng nguồn (nguồn nào lỗi, vì sao, cách xử lý) và các thông báo gần đây.
/// </summary>
public static class StatusPanel
{
    public static SourceState Source(AppState s, string name, string label)
    {
        var syncing = s.Syncing(name);
        // MyBK sync turned off in Settings: the hub never runs it, so old errors and "next sync" no longer apply.
        if (!syncing && name == SourceIds.Mybk && !Settings.Sync.MybkEnabled)
            return new SourceState(label, Line(label, off: true, s.SyncedAt(name)), null, null, false, false, name, Off: true);
        string state;
        if (syncing)
            state = s.Step(name) is { } st ? L.F("status.step", label, L.F(st.Key, st.Count, st.Of, st.Detail ?? "")) : L.F("status.syncing", label);
        else state = Line(label, off: false, s.SyncedAt(name));
        string? problem = null, detail = null;
        if (!syncing && s.ExplainError(name, label) is { } err)
        {
            (problem, detail) = err;
            if (s.SyncedAt(name) is { } saved) state = L.F("status.savedCopy", label, When(saved));   // không vào được: đang xem bản lưu
        }
        var partial = false;
        if (problem is null && !syncing && s.Warnings(name) is { Count: > 0 } w)
            (problem, detail, partial) = (string.Join("; ", w.Select(x => x.What).Distinct()), string.Join("\n", w.Select(x => x.Detail)), true);
        var need = s.Account;
        var covers = need == AccountNeed.Both || (name == SourceIds.Lms ? need == AccountNeed.Lms : need == AccountNeed.Mybk);
        // Chưa đăng nhập hay đã đăng xuất mà chưa có lượt nào lỗi: vẫn báo cần đăng nhập, để nút trạng thái và dải báo nói cùng một điều.
        if (!syncing && problem is null && covers) problem = L.T("status.needLogin");
        var login = !syncing && problem is not null && covers;
        // Next automatic run on the same line (the hub gives none while Offline or not connected). No line about the HCMUT session:
        // nothing keeps it alive or checks it, the app only learns it ended when a sync is rejected.
        // Only when it is within a day: a date weeks away (long period in config) reads like a bug and helps nobody.
        if (!syncing && (s.SyncedAt(name) is not null || s.Error(name) is not null) && s.NextAutoAt(name) is { } at
            && at - DateTimeOffset.UtcNow <= TimeSpan.FromDays(1))
            state = WithNext(state, at.ToUnixTimeSeconds());
        // MyBK has no schedule any more: say how it updates instead of a "next sync" time.
        if (!syncing && name == SourceIds.Mybk && s.SyncedAt(name) is not null) state = L.F("status.onDemand", state);
        return new SourceState(label, state, problem, detail, syncing, login && !partial, name, partial);
    }

    /// <summary>
    /// One line per source when it is not syncing: "LMS: đồng bộ lúc 01:08", "LMS: chưa đồng bộ", or for MyBK turned off in Settings
    /// "MyBK: đã tắt đồng bộ (Cài đặt), bản lưu lúc T5 05/11 18:28".
    /// </summary>
    public static string Line(string label, bool off, long? syncedAt) =>
        off ? syncedAt is { } o ? L.F("status.offSaved", label, When(o)) : L.F("status.off", label)
        : syncedAt is { } t ? L.F("status.syncedAt", label, When(t))
        : L.F("status.neverSynced", label);

    /// <summary>"LMS: đồng bộ lúc 01:08, lần tới khoảng 04:08".</summary>
    public static string WithNext(string state, long next) => L.F("status.withNext", state, When(next));

    /// <summary>Giờ đồng bộ ngắn gọn: hôm nay ghi giờ, ngày khác ghi ngày giờ.</summary>
    public static string When(long t) => Format.DayDiff(t) == 0 ? Format.Hm(t) : Format.DateTime(t);

    /// <summary>
    /// Chữ trên nút ở thanh trên cùng (ngắn, một dòng, nói luôn việc cần làm): đang đồng bộ; "MyBK: cần đăng nhập"; "LMS: chưa cập nhật
    /// được" (lỗi mạng, trường chậm: đang xem bản lưu); hay giờ xong gần nhất, đang Offline thì ghi Offline. Có thông báo chưa xem thì
    /// thêm số. Attention: nên in đậm (có việc cần để ý hay thông báo mới).
    /// </summary>
    public static (string Text, bool Attention) Bar(IReadOnlyList<SourceState> rows, int unread, long? lastSynced, bool offline = false)
    {
        var login = rows.Where(r => r.NeedsLogin).Select(r => r.Label).ToList();
        var failed = rows.Where(r => r.Problem is not null && !r.Partial && !r.NeedsLogin).Select(r => r.Label).ToList();
        var partial = rows.Any(r => r.Partial);
        var when = lastSynced is { } t ? Format.DayDiff(t) == 0 ? Format.Hm(t) : Format.Local(t).ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture) : null;
        var text = rows.Any(r => r.Syncing) ? L.T("status.bar.syncing")
            : login.Count > 0 ? L.F("status.bar.login", string.Join(", ", login))
            : failed.Count > 0 ? L.F("status.bar.failed", string.Join(", ", failed))
            : offline ? when is null ? L.T("status.bar.offline") : L.F("status.bar.offlineAt", when)
            : when is not null ? L.F("status.bar.synced", when)
            : L.T("status.bar.never");
        if (unread > 0) text += $" ({unread})";
        return (text, login.Count > 0 || failed.Count > 0 || partial || unread > 0);
    }
}

/// <summary>Các thông báo trong app gần đây (mới nhất trước, giữ tối đa 20), đếm số chưa xem. Chỉ trong bộ nhớ: tắt app là hết.</summary>
public sealed class NoticeLog
{
    private const int Max = 20;
    private readonly List<Notice> _items = [];

    public IReadOnlyList<Notice> Items => _items;
    public int Unread { get; private set; }
    public event Action? Changed;

    public void Add(Notice n)
    {
        _items.Insert(0, n);
        if (_items.Count > Max) _items.RemoveAt(_items.Count - 1);
        Unread = Math.Min(Unread + 1, _items.Count);   // không đếm thông báo đã rơi khỏi danh sách
        Changed?.Invoke();
    }

    public void MarkRead()
    {
        if (Unread == 0) return;
        Unread = 0;
        Changed?.Invoke();
    }

    public void Clear()
    {
        _items.Clear();
        Unread = 0;
        Changed?.Invoke();
    }
}
