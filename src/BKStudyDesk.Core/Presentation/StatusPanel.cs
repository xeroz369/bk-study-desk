using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public sealed record SourceState(string Label, string State, string? Problem, string? Detail, bool Syncing, bool NeedsLogin, string Id = "", bool Partial = false,
    bool Off = false);

public sealed record Notice(long Time, string Title, string Body, string Page);

public static class StatusPanel
{
    public static SourceState Source(AppState s, string name, string label)
    {
        var syncing = s.Syncing(name);
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
            if (s.SyncedAt(name) is { } saved) state = L.F("status.savedCopy", label, When(saved));
        }
        var partial = false;
        if (problem is null && !syncing && s.Warnings(name) is { Count: > 0 } w)
            (problem, detail, partial) = (string.Join("; ", w.Select(x => x.What).Distinct()), string.Join("\n", w.Select(x => x.Detail)), true);
        var need = s.Account;
        var covers = need == AccountNeed.Both || (name == SourceIds.Lms ? need == AccountNeed.Lms : need == AccountNeed.Mybk);
        if (!syncing && problem is null && covers) problem = L.T("status.needLogin");
        var login = !syncing && problem is not null && covers;
        if (!syncing && (s.SyncedAt(name) is not null || s.Error(name) is not null) && s.NextAutoAt(name) is { } at
            && at - DateTimeOffset.UtcNow <= TimeSpan.FromDays(1))
            state = WithNext(state, at.ToUnixTimeSeconds());
        if (!syncing && name == SourceIds.Mybk && s.SyncedAt(name) is not null) state = L.F("status.onDemand", state);
        return new SourceState(label, state, problem, detail, syncing, login && !partial, name, partial);
    }

    public static string Line(string label, bool off, long? syncedAt) =>
        off ? syncedAt is { } o ? L.F("status.offSaved", label, When(o)) : L.F("status.off", label)
        : syncedAt is { } t ? L.F("status.syncedAt", label, When(t))
        : L.F("status.neverSynced", label);

    public static string WithNext(string state, long next) => L.F("status.withNext", state, When(next));

    public static string When(long t) => Format.DayDiff(t) == 0 ? Format.Hm(t) : Format.DateTime(t);

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
        Unread = Math.Min(Unread + 1, _items.Count);
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
