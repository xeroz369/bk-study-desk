using System.Net.NetworkInformation;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Data;

namespace SoHocTap.Sources;

/// <summary>
/// Quản lý các nguồn: trạng thái sync, chạy sync (mỗi nguồn chỉ một lượt cùng lúc), scheduler, bắn event cho UI.
/// Scheduler thức dậy theo app.schedulerMinutes; chỉ gửi request khi nguồn đã kết nối, máy có mạng và đã quá chu kỳ.
/// </summary>
public sealed class SourceHub : IDisposable
{
    private sealed class State
    {
        public bool Running;
        public DateTimeOffset FailedAt;
        public DateTimeOffset StartedAt;
        public string? Error;
        public SyncErrorKind? Kind;          // loại lỗi; null = lỗi ghi bởi bản cũ (chỉ có message)
        public int NetFails;                 // số lần lỗi mạng liên tiếp, để retry sớm 10, 30, 60 phút
        public List<string> Log = [];
        public SyncProgress? Progress;       // tiến độ gần nhất nguồn báo (SyncPlan)
        public DateTimeOffset StepShownAt;
        public List<string> Warnings = [];   // phần không đọc được ở lần đồng bộ gần nhất
        public object? Seen;                 // dữ liệu (bản trong cache của store) UI đã được báo gần nhất
        public bool DataChanged;             // lượt này đã có dữ liệu mới (đã bắn "data" hoặc bản cuối khác lúc đầu)
        public bool Quiet;                   // lượt gần nhất xong mà không có gì mới: UI chỉ cập nhật thanh trạng thái
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

    /// <summary>Lỗi mạng thì retry sớm theo nấc này (phút) thay vì đợi hết một chu kỳ; quá nấc cuối thì giữ nấc cuối.</summary>
    private static readonly int[] NetRetryMinutes = [10, 30, 60];

    /// <summary>(nguồn, "start" | "progress" | "data" | "done"), Shell chuyển thành event cho UI.</summary>
    public event Action<string, string>? Changed;

    /// <summary>
    /// Sau mỗi lượt sync thành công: dữ liệu khác gì lần trước (nguồn, thay đổi). Bắn trên thread chạy sync.
    /// Lượt không có gì mới (Quiet) thì không bắn. Chưa có tính năng nào nghe (1.1.8), mới ghi log Debug số lượng.
    /// </summary>
    public event Action<string, ChangeSet>? ChangeSetReady;

    /// <summary>Đang bật Tiết kiệm pin (Shell cài, Core không biết API của Windows): scheduler giãn chu kỳ gấp đôi.</summary>
    public Func<bool>? BatterySaver { get; set; }

    /// <summary>Máy đang có mạng; mặc định hỏi NetworkInterface, test thì thay được.</summary>
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

    // LMS và MyBK sync song song, cùng ghi một file sync-state.json: khóa cả lúc dựng lẫn lúc ghi để bản ghi sau không đè
    // mất phần của nguồn kia, và hai lần ghi không giẫm lên nhau.
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

    /// <summary>Lượt sync gần nhất của nguồn bị lỗi (chưa có lượt nào thành công sau đó).</summary>
    public bool Failed(string name) => _state.TryGetValue(name, out var st) && st.Error is not null;

    /// <summary>
    /// Lượt sync gần nhất xong mà không có gì mới (dữ liệu y như cũ, không lỗi, không cảnh báo): UI chỉ cập nhật thanh trạng thái,
    /// không đọc lại, không vẽ lại trang, không thông báo.
    /// </summary>
    public bool Quiet(string name) => _state.TryGetValue(name, out var st) && st.Quiet && st.Error is null;

    /// <summary>Loại lỗi của lượt sync gần nhất; null nếu không lỗi hay lỗi của bản cũ.</summary>
    public SyncErrorKind? ErrorKind(string name) => _state.TryGetValue(name, out var st) && st.Error is not null ? st.Kind : null;

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
                // Store không ghi khi dữ liệu y như cũ, nên bản trong cache vẫn là đúng object lúc đầu: so tham chiếu là đủ.
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
            // Chỉ nuốt OperationCanceled khi app đang dừng. HttpClient hết Timeout cũng ném TaskCanceledException: đó là lỗi
            // Timeout thật, phải báo và ghi lại, không được coi như "đã hủy".
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
            // Phần chính đã lưu mà y như cũ (store không ghi): không bắt UI đọc lại, vẽ lại.
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

    /// <summary>
    /// Báo lên UI ngay khi đổi câu, đổi từ vô định sang có số, hay xong hẳn; cùng câu chỉ tăng số thì tối đa 4 lần/giây (đỡ vẽ lại liên tục).
    /// </summary>
    internal static bool ShowProgress(SyncProgress? last, SyncProgress now, TimeSpan sinceShown) =>
        last is not { } l || l.Key != now.Key || (l.Permille is null) != (now.Permille is null) || now.Permille == 1000
        || sinceShown > TimeSpan.FromMilliseconds(250);

    // ------------------------------------------------------------------ ChangeSet

    /// <summary>Dữ liệu đã lưu trước lượt sync (store có cache, không đọc lại file nếu không đổi).</summary>
    private static object? Snapshot(string name) => name switch
    {
        "lms" => LmsStore.Read(),
        "mybk" => MybkStore.Read(),
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
        // So sánh lỗi không được làm hỏng lượt sync vừa xong.
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Error($"ChangeSet {name}", e); }
    }

    // ------------------------------------------------------------------ scheduler

    /// <summary>Chu kỳ thực tế của nguồn: gấp đôi khi bật Tiết kiệm pin.</summary>
    private TimeSpan IntervalOf(ISource src) => BatterySaverOn() ? src.Interval * 2 : src.Interval;

    private bool BatterySaverOn()
    {
        try { return BatterySaver?.Invoke() == true; }
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Debug($"Không đọc được trạng thái pin: {e.Message}"); return false; }
    }

    /// <summary>
    /// Lần trước lỗi thì bao lâu sau mới tự retry: lỗi mạng/timeout thì sớm (10, 30, 60 phút, không quá một chu kỳ), lỗi khác
    /// (thường do session hết hạn, server giới hạn) thì đợi hết một chu kỳ, đỡ tốn pin và đỡ spam request.
    /// </summary>
    internal static TimeSpan RetryAfter(SyncErrorKind? kind, int netFails, TimeSpan interval)
    {
        if (kind is not (SyncErrorKind.Network or SyncErrorKind.Timeout) || netFails <= 0) return interval;
        var minutes = NetRetryMinutes[Math.Min(netFails, NetRetryMinutes.Length) - 1];
        var retry = TimeSpan.FromMinutes(minutes);
        return retry < interval ? retry : interval;
    }

    /// <summary>
    /// Một lượt của scheduler: nguồn nào tới hạn thì chạy. Không có mạng thì bỏ lượt, không ghi lỗi (lỗi "mất mạng" lúc máy
    /// đang offline chỉ làm người dùng lo). Login lại hoặc bấm Đồng bộ thì vẫn chạy ngay (không qua đây).
    /// </summary>
    public void RunDue()
    {
        if (_stop.IsCancellationRequested) return;
        if (!Online())
        {
            Log.Debug("Scheduler: máy đang offline, bỏ lượt");
            return;
        }
        foreach (var src in _sources.Values)
        {
            var s = src.Status();
            if (!s.Connected) continue;
            var interval = IntervalOf(src);
            var st = _state[src.Name];
            TimeSpan wait;
            DateTimeOffset failedAt;
            lock (st) { wait = st.Error is null ? TimeSpan.Zero : RetryAfter(st.Kind, st.NetFails, interval); failedAt = st.FailedAt; }
            if (wait > TimeSpan.Zero && DateTimeOffset.UtcNow - failedAt < wait) continue;
            var last = s.SyncedAt is { } t ? DateTimeOffset.FromUnixTimeSeconds(t) : DateTimeOffset.MinValue;
            // Lần trước lỗi mạng và đã tới lúc retry thì chạy luôn, dù chưa hết chu kỳ tính từ lần thành công.
            var retryDue = wait > TimeSpan.Zero && wait < interval;
            if (retryDue || DateTimeOffset.UtcNow - last > interval) Start(src.Name);
        }
    }

    /// <summary>
    /// Có mạng lại, máy vừa thức dậy: chờ một chút (mạng vừa lên thường chưa có DNS) rồi chạy một lượt scheduler.
    /// Gọi nhiều lần liền nhau thì chỉ chạy một lượt.
    /// </summary>
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
            // App đang thoát (_stop đã hủy hoặc đã Dispose): bỏ lượt.
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
        // Lượt không có gì mới chỉ giữ "lần kiểm tra cuối" trong RAM: ghi ra trước khi thoát.
        foreach (var src in _sources.Values)
            try { src.Flush(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Warn($"Ghi trạng thái {src.Name} khi thoát: {e.Message}"); }
        _stop.Cancel();
        _stop.Dispose();
    }
}
