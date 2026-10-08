using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>
/// Tình trạng một nguồn trong bảng Thông báo: dòng chữ (đang làm bước nào, xong lúc nào), vấn đề nếu có (lỗi, phần không đọc được),
/// chi tiết kỹ thuật, và có cần đăng nhập lại không (View hiện nút Đăng nhập lại, còn lại là Thử lại).
/// </summary>
public sealed record SourceState(string Label, string State, string? Problem, string? Detail, bool Syncing, bool NeedsLogin);

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
        string state;
        if (syncing)
            state = s.Step(name) is { } st ? L.F("status.step", label, L.F(st.Key, st.Count, st.Of, st.Detail ?? "")) : L.F("status.syncing", label);
        else if (s.SyncedAt(name) is { } t)
            state = L.F("status.syncedAt", label, Format.DayDiff(t) == 0 ? Format.Hm(t) : Format.DateTime(t));
        else state = L.F("status.neverSynced", label);
        string? problem = null, detail = null;
        if (!syncing && s.ExplainError(name, label) is { } err) (problem, detail) = err;
        else if (!syncing && s.Warnings(name) is { Count: > 0 } w) (problem, detail) = (string.Join("; ", w.Select(x => x.What).Distinct()), string.Join("\n", w.Select(x => x.Detail)));
        var need = s.Account;
        var login = !syncing && problem is not null && (need == AccountNeed.Both || (name == SourceIds.Lms ? need == AccountNeed.Lms : need == AccountNeed.Mybk));
        return new SourceState(label, state, problem, detail, syncing, login);
    }

    /// <summary>
    /// Chữ trên nút ở thanh trên cùng (ngắn, một dòng): đang đồng bộ, lỗi ở nguồn nào, hay giờ xong gần nhất; có thông báo chưa xem thì
    /// thêm số. Attention: nên in đậm (có lỗi hay thông báo mới).
    /// </summary>
    public static (string Text, bool Attention) Bar(IReadOnlyList<SourceState> rows, int unread, long? lastSynced)
    {
        var failed = rows.Where(r => r.Problem is not null).Select(r => r.Label).ToList();
        var text = rows.Any(r => r.Syncing) ? L.T("status.bar.syncing")
            : failed.Count > 0 ? L.F("status.bar.failed", string.Join(", ", failed))
            : lastSynced is { } t ? L.F("status.bar.synced", Format.DayDiff(t) == 0 ? Format.Hm(t) : Format.Local(t).ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture))
            : L.T("status.bar.never");
        if (unread > 0) text += $" ({unread})";
        return (text, failed.Count > 0 || unread > 0);
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
        Unread++;
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
