using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Sources;

/// <summary>
/// Quản lý các nguồn: trạng thái sync, chạy sync (mỗi nguồn chỉ một lượt cùng lúc), scheduler, bắn event cho UI.
/// Scheduler thức dậy theo app.schedulerMinutes; chỉ gửi request khi nguồn đã kết nối và đã quá chu kỳ.
/// </summary>
public sealed class SourceHub : IDisposable
{
    private sealed class State
    {
        public bool Running;
        public DateTimeOffset FailedAt;
        public DateTimeOffset StartedAt;
        public string? Error;
        public List<string> Log = [];
        public string? Step;             // key ngôn ngữ của bước đang làm
        public int Done, Total;
        public DateTimeOffset StepShownAt;
        public List<string> Warnings = [];   // phần không đọc được ở lần đồng bộ gần nhất
    }

    private readonly Dictionary<string, ISource> _sources;
    private readonly Dictionary<string, State> _state;
    private readonly CancellationTokenSource _stop = new();

    /// <summary>Lỗi sync gần nhất của từng nguồn (data/sync-state.json): restart app vẫn nhớ, không retry ngay.</summary>
    private static string StateFile => Paths.DataFile("sync-state.json");

    /// <summary>Vừa sync thành công mà bấm Đồng bộ tiếp thì bỏ qua, tránh spam request lên server trường.</summary>
    private static readonly TimeSpan MinGap = TimeSpan.FromMinutes(2);

    /// <summary>Khoảng tối thiểu giữa hai lần bắt đầu sync, kể cả khi lần trước lỗi hay bấm "Đồng bộ lại": bấm liên tục không bắn request liên tục.</summary>
    private static readonly TimeSpan MinRestart = TimeSpan.FromSeconds(30);

    /// <summary>(nguồn, "start" | "done"), Shell chuyển thành event cho UI.</summary>
    public event Action<string, string>? Changed;

    public SourceHub(IEnumerable<ISource> sources)
    {
        _sources = sources.ToDictionary(s => s.Name);
        _state = _sources.Keys.ToDictionary(k => k, _ => new State());
        var saved = JsonStore.ReadObject(StateFile);
        foreach (var (name, st) in _state)
            if (saved[name] is JsonObject o)
            {
                if (o["failedAt"]?.GetValue<long>() is { } t)
                {
                    st.FailedAt = DateTimeOffset.FromUnixTimeSeconds(t);
                    st.Error = o["error"]?.GetValue<string>();
                }
                st.Warnings = (o["warnings"] as JsonArray ?? []).Select(w => w?.GetValue<string>() ?? "").Where(w => w.Length > 0).ToList();
            }
    }

    private void SaveState()
    {
        var o = new JsonObject();
        foreach (var (name, st) in _state)
            lock (st)
            {
                var s = new JsonObject();
                if (st.Error is not null) { s["failedAt"] = st.FailedAt.ToUnixTimeSeconds(); s["error"] = st.Error; }
                if (st.Warnings.Count > 0) s["warnings"] = new JsonArray(st.Warnings.Select(w => (JsonNode)w).ToArray());
                if (s.Count > 0) o[name] = s;
            }
        JsonStore.Write(StateFile, o);
    }

    public ISource? Get(string name) => _sources.GetValueOrDefault(name);

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
                    ["warnings"] = new JsonArray(st.Warnings.Select(w => (JsonNode)w).ToArray()),
                    ["step"] = st.Running ? st.Step : null,
                    ["done"] = st.Done,
                    ["total"] = st.Total,
                    ["log"] = new JsonArray(st.Log.TakeLast(30).Select(x => (JsonNode)x).ToArray()),
                };
            }
        }
        return o;
    }

    /// <summary>Chạy sync dưới nền. Trả false nếu nguồn đang chạy, chưa kết nối, hoặc vừa sync xong (dưới 2 phút, trừ khi force).</summary>
    public bool Start(string name, bool force = false)
    {
        if (!_sources.TryGetValue(name, out var src)) return false;
        var status = src.Status();
        if (!status.Connected) return false;
        if (!force && status.SyncedAt is { } t && DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(t) < MinGap && _state[name].Error is null) return false;
        var st = _state[name];
        lock (st)
        {
            if (st.Running || DateTimeOffset.UtcNow - st.StartedAt < MinRestart) return false;
            st.Running = true; st.Error = null; st.Log = []; st.StartedAt = DateTimeOffset.UtcNow;
            st.Step = null; st.Done = st.Total = 0; st.Warnings = [];
        }
        Changed?.Invoke(name, "start");
        Log.Debug($"Sync {name}: bắt đầu{(force ? " (bấm tay)" : "")}");
        _ = Task.Run(async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await src.SyncAsync(line =>
                {
                    if (line == SyncSignal.DataReady) { Changed?.Invoke(name, "data"); return; }
                    if (SyncSignal.TryParseWarn(line, out var warn))
                    {
                        lock (st) { if (st.Warnings.Count < 30) st.Warnings.Add(warn); st.Log.Add("  lỗi: " + warn); }
                        return;
                    }
                    if (SyncSignal.TryParseStep(line, out var key, out var done, out var total))
                    {
                        bool show;
                        lock (st)
                        {
                            // Đổi bước thì báo ngay; cùng bước chỉ tăng số thì tối đa 4 lần/giây (đỡ vẽ lại giao diện liên tục).
                            show = key != st.Step || DateTimeOffset.UtcNow - st.StepShownAt > TimeSpan.FromMilliseconds(250) || done == total;
                            st.Step = key; st.Done = done; st.Total = total;
                            if (show) st.StepShownAt = DateTimeOffset.UtcNow;
                        }
                        if (show) Changed?.Invoke(name, "progress");
                        return;
                    }
                    lock (st) st.Log.Add(line);
                }, force, _stop.Token);
                SaveState();
                Log.Debug($"Sync {name}: xong sau {sw.Elapsed.TotalSeconds:0.0} giây");
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                lock (st) { st.Error = e.Message; st.FailedAt = DateTimeOffset.UtcNow; }
                SaveState();
                Log.Error($"Sync {name} lỗi", e);
            }
            finally
            {
                lock (st) st.Running = false;
                Changed?.Invoke(name, "done");
            }
        });
        return true;
    }

    public void StartScheduler()
    {
        var minutes = Math.Max(1, Config.Int("app.schedulerMinutes", 10));
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
            await Task.Delay(TimeSpan.FromSeconds(10), _stop.Token).ContinueWith(_ => { });
            do
            {
                foreach (var src in _sources.Values)
                {
                    var s = src.Status();
                    var last = s.SyncedAt is { } t ? DateTimeOffset.FromUnixTimeSeconds(t) : DateTimeOffset.MinValue;
                    // Lần trước lỗi (thường do session hết hạn) thì đợi hết một chu kỳ mới retry, đỡ tốn pin và đỡ spam request.
                    // Login lại hoặc bấm Đồng bộ thì vẫn chạy ngay.
                    var st = _state[src.Name];
                    if (st.Error is not null && DateTimeOffset.UtcNow - st.FailedAt < src.Interval) continue;
                    if (s.Connected && DateTimeOffset.UtcNow - last > src.Interval) Start(src.Name);
                }
            } while (await timer.WaitForNextTickAsync(_stop.Token).AsTask().ContinueWith(t => !t.IsCanceled && t.Result));
        });
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}
