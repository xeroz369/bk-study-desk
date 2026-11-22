using System.Net.NetworkInformation;
using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Sources;

public sealed class SourceHub : IDisposable
{
    private sealed class State
    {
        public bool Running;
        public DateTimeOffset FailedAt;
        public DateTimeOffset StartedAt;
        public string? Error;
        public SyncErrorKind? Kind;
        public int NetFails;
        public List<string> Log = [];
        public SyncProgress? Progress;
        public DateTimeOffset StepShownAt;
        public List<string> Warnings = [];
        public object? Seen;
        public bool DataChanged;
        public bool Quiet;
    }

    private readonly Dictionary<string, ISource> _sources;
    private readonly Dictionary<string, State> _state;
    private readonly CancellationTokenSource _stop = new();

    private static string StateFile => Paths.DataFile("sync-state.json");

    private static readonly TimeSpan MinGap = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan MinRestart = TimeSpan.FromSeconds(30);

    private static readonly int[] NetRetryMinutes = [10, 30, 60];

    public event Action<string, string>? Changed;

    public event Action<string, ChangeSet>? ChangeSetReady;

    public Func<bool>? BatterySaver { get; set; }

    public Func<bool> Online { get; set; } = NetworkInterface.GetIsNetworkAvailable;

    public SourceHub(IEnumerable<ISource> sources)
    {
        _sources = sources.ToDictionary(s => s.Name);
        _state = _sources.Keys.ToDictionary(k => k, _ => new State());
        var saved = JsonStore.ReadObject(StateFile);
        foreach (var (name, st) in _state)
            if (saved[name] is JsonObject o)
            {
                if (o["failedAt"] is JsonValue f && f.TryGetValue<long>(out var t))
                {
                    st.FailedAt = DateTimeOffset.FromUnixTimeSeconds(t);
                    st.Error = o["error"]?.ToString();
                    st.Kind = SyncErrorText.Parse(o["errorKind"]?.ToString());
                }
                st.Warnings = (o["warnings"] as JsonArray ?? []).Select(w => w?.ToString() ?? "").Where(w => w.Length > 0).ToList();
            }
    }

    private static readonly object StateGate = new();

    private void SaveState()
    {
        lock (StateGate)
        {
            var o = new JsonObject();
            foreach (var (name, st) in _state)
                lock (st)
                {
                    var s = new JsonObject();
                    if (st.Error is not null)
                    {
                        s["failedAt"] = st.FailedAt.ToUnixTimeSeconds();
                        s["error"] = st.Error;
                        if (st.Kind is { } k) s["errorKind"] = k.ToString();
                    }
                    if (st.Warnings.Count > 0) s["warnings"] = new JsonArray(st.Warnings.Select(w => (JsonNode)w).ToArray());
                    if (s.Count > 0) o[name] = s;
                }
            try { JsonStore.Write(StateFile, o); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Error("Không ghi được sync-state.json", e); }
        }
    }

    public ISource? Get(string name) => _sources.GetValueOrDefault(name);

    public bool Failed(string name) => _state.TryGetValue(name, out var st) && st.Error is not null;

    public bool Quiet(string name) => _state.TryGetValue(name, out var st) && st.Quiet && st.Error is null;

    public SyncErrorKind? ErrorKind(string name) => _state.TryGetValue(name, out var st) && st.Error is not null ? st.Kind : null;

    public void ForgetSessionExpired()
    {
        var changed = false;
        foreach (var st in _state.Values)
            lock (st)
                if (st.Error is not null && st.Kind == SyncErrorKind.SessionExpired)
                {
                    st.Error = null; st.Kind = null; changed = true;
                    if (!st.Running) st.StartedAt = default;
                }
        if (changed) SaveState();
    }

    public JsonObject StatusJson()
    {
        var o = new JsonObject();
        foreach (var (name, src) in _sources)
        {
            var s = src.Status();
            var st = _state[name];
            lock (st)
            {
                o[name] = new JsonObject
                {
                    ["name"] = name,
                    ["connected"] = s.Connected,
                    ["syncedAt"] = s.SyncedAt,
                    ["user"] = s.User,
                    ["term"] = s.Term,
                    ["syncing"] = st.Running,
                    ["error"] = st.Error,
                    ["errorKind"] = st.Error is null ? null : st.Kind?.ToString(),
                    ["warnings"] = new JsonArray(st.Warnings.Select(w => (JsonNode)w).ToArray()),
                    ["step"] = st.Running ? st.Progress?.Key : null,
                    ["count"] = st.Progress?.Count ?? 0,
                    ["of"] = st.Progress?.Of ?? 0,
                    ["detail"] = st.Progress?.Detail,
                    ["permille"] = st.Progress?.Permille,
                    ["log"] = new JsonArray(st.Log.TakeLast(30).Select(x => (JsonNode)x).ToArray()),
                };
            }
        }
        return o;
    }

    public bool Start(string name, bool force = false, bool soon = false)
    {
        if (!_sources.TryGetValue(name, out var src)) return false;
        var status = src.Status();
        if (!status.Connected) return false;
        if (!force && status.SyncedAt is { } t && DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(t) < MinGap && _state[name].Error is null) return false;
        var st = _state[name];
        lock (st)
        {
            if (st.Running || (!soon && DateTimeOffset.UtcNow - st.StartedAt < MinRestart)) return false;
            st.Running = true; st.Error = null; st.Kind = null; st.Log = []; st.StartedAt = DateTimeOffset.UtcNow;
            st.Progress = null; st.Warnings = []; st.DataChanged = false; st.Quiet = false;
        }
        Changed?.Invoke(name, "start");
        Log.Debug($"Sync {name}: bắt đầu{(force ? " (bấm tay)" : "")}");
        _ = Task.Run(async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var before = Snapshot(name);
            lock (st) st.Seen = before;
            try
            {
                await src.SyncAsync(line => OnLine(name, st, line), force, _stop.Token);
                lock (st) st.NetFails = 0;
                SaveState();
                var after = Snapshot(name);
                bool quiet;
                lock (st)
                {
                    quiet = st.Quiet = SyncedFile.IsQuiet(st.Seen, after, st.DataChanged, st.Warnings.Count);
                    st.DataChanged |= !ReferenceEquals(after, st.Seen);
                }
                Log.Debug($"Sync {name}: xong sau {sw.Elapsed.TotalSeconds:0.0} giây{(quiet ? ", không có gì mới" : "")}");
                if (!quiet) ReportChanges(name, before);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { Log.Debug($"Sync {name}: dừng vì app thoát"); }
            catch (Exception e)
            {
                var kind = SyncErrors.Classify(e);
                lock (st)
                {
                    st.Error = e.Message; st.Kind = kind; st.FailedAt = DateTimeOffset.UtcNow;
                    st.NetFails = kind is SyncErrorKind.Network or SyncErrorKind.Timeout ? st.NetFails + 1 : 0;
                }
                SaveState();
                Log.Error($"Sync {name} lỗi ({kind})", e);
            }
            finally
            {
                lock (st) st.Running = false;
                Changed?.Invoke(name, "done");
            }
        });
        return true;
    }

    private void OnLine(string name, State st, string line)
    {
        if (line == SyncSignal.DataReady)
        {
            var now = Snapshot(name);
            bool fresh;
            lock (st)
            {
                fresh = !ReferenceEquals(now, st.Seen);
                if (fresh) { st.Seen = now; st.DataChanged = true; }
            }
            Changed?.Invoke(name, fresh ? "data" : "progress");
            return;
        }
        if (SyncSignal.TryParseWarn(line, out var warn))
        {
            lock (st) { if (st.Warnings.Count < 30) st.Warnings.Add(warn); st.Log.Add("  lỗi: " + warn); }
            return;
        }
        if (SyncSignal.TryParseProgress(line, out var p))
        {
            bool show;
            lock (st)
            {
                show = ShowProgress(st.Progress, p, DateTimeOffset.UtcNow - st.StepShownAt);
                st.Progress = p;
                if (show) st.StepShownAt = DateTimeOffset.UtcNow;
            }
            if (show) Changed?.Invoke(name, "progress");
            return;
        }
        lock (st) st.Log.Add(line);
    }

    internal static bool ShowProgress(SyncProgress? last, SyncProgress now, TimeSpan sinceShown) =>
        last is not { } l || l.Key != now.Key || (l.Permille is null) != (now.Permille is null) || now.Permille == 1000
        || sinceShown > TimeSpan.FromMilliseconds(250);

    private static object? Snapshot(string name) => name switch
    {
        SourceIds.Lms => LmsStore.Read(),
        SourceIds.Mybk => MybkStore.Read(),
        _ => null,
    };

    private void ReportChanges(string name, object? before)
    {
        try
        {
            var changes = (before, Snapshot(name)) switch
            {
                (LmsData b, LmsData a) => ChangeSet.Compare(b, a),
                (MybkData b, MybkData a) => ChangeSet.Compare(b, a),
                _ => ChangeSet.Empty,
            };
            Log.Debug($"ChangeSet {name}: {changes.Counts}");
            ChangeSetReady?.Invoke(name, changes);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Error($"ChangeSet {name}", e); }
    }

    public DateTimeOffset? NextAutoAt(string name)
    {
        if (OfflineMode.On || !UserActivity.Active || !_sources.TryGetValue(name, out var src) || !src.Scheduled) return null;
        var s = src.Status();
        if (!s.Connected) return null;
        var interval = IntervalOf(src);
        var st = _state[name];
        TimeSpan wait;
        DateTimeOffset failedAt;
        lock (st)
        {
            if (st.Running) return null;
            wait = st.Error is null ? TimeSpan.Zero : RetryAfter(st.Kind, st.NetFails, interval);
            failedAt = st.FailedAt;
        }
        var last = s.SyncedAt is { } t ? DateTimeOffset.FromUnixTimeSeconds(t) : DateTimeOffset.MinValue;
        var byInterval = last == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : last + interval;
        var next = wait <= TimeSpan.Zero ? byInterval
            : wait < interval ? failedAt + wait
            : (failedAt + wait > byInterval ? failedAt + wait : byInterval);
        return next > DateTimeOffset.UtcNow ? next : DateTimeOffset.UtcNow;
    }

    private TimeSpan IntervalOf(ISource src) => BatterySaverOn() ? src.Interval * 2 : src.Interval;

    private bool BatterySaverOn()
    {
        try { return BatterySaver?.Invoke() == true; }
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Debug($"Không đọc được trạng thái pin: {e.Message}"); return false; }
    }

    internal static TimeSpan RetryAfter(SyncErrorKind? kind, int netFails, TimeSpan interval)
    {
        if (kind is not (SyncErrorKind.Network or SyncErrorKind.Timeout) || netFails <= 0) return interval;
        var minutes = NetRetryMinutes[Math.Min(netFails, NetRetryMinutes.Length) - 1];
        var retry = TimeSpan.FromMinutes(minutes);
        return retry < interval ? retry : interval;
    }

    public void RunDue()
    {
        if (_stop.IsCancellationRequested) return;
        if (OfflineMode.On) return;
        if (!UserActivity.Active) { Log.Debug("Scheduler: user away, skip"); return; }
        if (!Online())
        {
            Log.Debug("Scheduler: máy đang offline, bỏ lượt");
            return;
        }
        foreach (var src in _sources.Values)
        {
            if (!src.Scheduled) continue;
            var s = src.Status();
            if (!s.Connected) continue;
            var interval = IntervalOf(src);
            var st = _state[src.Name];
            TimeSpan wait;
            DateTimeOffset failedAt;
            lock (st) { wait = st.Error is null ? TimeSpan.Zero : RetryAfter(st.Kind, st.NetFails, interval); failedAt = st.FailedAt; }
            if (wait > TimeSpan.Zero && DateTimeOffset.UtcNow - failedAt < wait) continue;
            var last = s.SyncedAt is { } t ? DateTimeOffset.FromUnixTimeSeconds(t) : DateTimeOffset.MinValue;
            var retryDue = wait > TimeSpan.Zero && wait < interval;
            if (retryDue || DateTimeOffset.UtcNow - last > interval) Start(src.Name);
        }
    }

    public void RunDueSoon(TimeSpan delay)
    {
        if (_stop.IsCancellationRequested || Interlocked.Exchange(ref _dueSoon, 1) == 1) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, _stop.Token);
                Interlocked.Exchange(ref _dueSoon, 0);
                RunDue();
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException) { }
            catch (Exception e) when (e is not OutOfMemoryException) { Log.Error("Scheduler (sau khi có mạng lại)", e); }
            finally { Interlocked.Exchange(ref _dueSoon, 0); }
        });
    }

    private int _dueSoon;

    public void StartScheduler()
    {
        var minutes = Math.Max(1, Config.Int("app.schedulerMinutes", 10));
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), _stop.Token);
                do
                {
                    try { RunDue(); }
                    catch (Exception e) when (e is not OutOfMemoryException) { Log.Error("Scheduler", e); }
                } while (await timer.WaitForNextTickAsync(_stop.Token));
            }
            catch (OperationCanceledException) { }
        });
    }

    public void Dispose()
    {
        foreach (var src in _sources.Values)
            try { src.Flush(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Warn($"Ghi trạng thái {src.Name} khi thoát: {e.Message}"); }
        _stop.Cancel();
        _stop.Dispose();
    }
}
