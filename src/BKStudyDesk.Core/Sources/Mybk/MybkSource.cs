using System.Text.Json;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Data;

namespace SoHocTap.Sources.Mybk;

/// <summary>
/// Source MyBK, phần Thông tin sinh viên (docs/archive/DESIGN-1.x.md mục 8b).
/// Flow: /app/ bằng phiên đang có; MyBK đòi đăng nhập thì SSO → /app/login/cas → /app/ (Bearer token nằm trong #hid_Token).
/// Mọi request đều chạy trong trang MyBK qua <see cref="IBrowserRunner"/>. Chỉ gọi API đọc (sources.mybk.api), trừ refreshGrades/refreshCurriculum (POST, nút Cập nhật của MyBK) khi người dùng bấm nút.
/// Không lưu CCCD, địa chỉ, điện thoại, ngày sinh, email cá nhân.
/// </summary>
public sealed class MybkSource(Func<IBrowserRunner?> runner) : ISource
{
    public string Name => SourceIds.Mybk;
    public TimeSpan Interval => TimeSpan.FromHours(Settings.Sync.MybkHours);

    private static string DataFile => Paths.DataFile("mybk.json");
    private static string SessionFile => Paths.DataFile(Path.Combine("secrets", "mybk-session.json"));

    /// <summary>
    /// Login HCMUT xong (flow đã đi qua MyBK) thì true, logout thì false. Chưa login lần nào và chưa có data thì
    /// không coi là đã kết nối, scheduler sẽ không mở MyBK ngầm (đỡ spam request lên server trường).
    /// </summary>
    public static void SetSignedIn(bool on)
    {
        JsonStore.Write(SessionFile, new JsonObject { ["signedIn"] = on, ["at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
        Session.Invalidate();
    }

    // Cờ đăng nhập đọc có cache (khóa theo mtime): Status() được hỏi ở mỗi bước đồng bộ, không đọc file lại mỗi lần.
    private static readonly CachedFile<JsonObject> Session = new(() => SessionFile, text => JsonNode.Parse(text) as JsonObject) { OnProblem = Log.Warn };

    /// <summary>Đã đăng nhập HCMUT trong app (theo lần đăng nhập/đăng xuất gần nhất).</summary>
    public static bool SignedIn => Session.Get()?["signedIn"] is JsonValue v && v.TryGetValue<bool>(out var on) ? on : File.Exists(DataFile);

    public SourceStatus Status()
    {
        var d = MybkStore.Read();
        var signedIn = Session.Get()?["signedIn"] is JsonValue v && v.TryGetValue<bool>(out var on) ? on : File.Exists(DataFile);
        return new SourceStatus(runner() is not null && signedIn && Settings.Sync.MybkEnabled, MybkStore.SyncedAt(), d?.Student.Name);
    }

    public JsonNode? Data() => JsonStore.Read(DataFile);

    public void Flush() => MybkStore.Flush();

    // ------------------------------------------------------------------ what to load (docs/plans/2026-11-06-on-demand-sync.md)

    private static readonly object WantGate = new();
    private static IReadOnlyList<string> _visible = [];
    private static readonly HashSet<string> Pending = [];
    private static bool _pendingForce;
    private static readonly Dictionary<string, long> FailedAt = [];
    private static long _staleBefore;   // sync button: sections fetched before this reload when next opened

    /// <summary>MyBK never runs on the scheduler: it loads only what a page shows, when the page is opened or the user asks.</summary>
    public bool Scheduled => false;

    /// <summary>
    /// The page in view shows these sections. true: one of them is stale (or never fetched) and not recently failed, so the shell
    /// should start a sync; the saved copy stays on screen meanwhile.
    /// </summary>
    public static bool Show(IReadOnlyList<string> sections)
    {
        lock (WantGate) _visible = sections;
        return NeedsRun();
    }

    /// <summary>The user pressed sync (or "update program on MyBK"): fetch these sections even if fresh.</summary>
    public static void Want(IEnumerable<string> sections, bool force)
    {
        lock (WantGate)
        {
            Pending.UnionWith(sections);
            _pendingForce |= force;
        }
    }

    /// <summary>Sync button: the sections of the page in view, fresh or not.</summary>
    public static void WantVisible() { lock (WantGate) Want(_visible, force: true); }

    /// <summary>
    /// The MyBK sync button: reload the sections of the page in view now; every other section counts as stale and reloads when its page
    /// is next opened (not now, and never if it is not opened). Recent failures are forgiven.
    /// </summary>
    public static void RefreshButton()
    {
        lock (WantGate)
        {
            _staleBefore = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            FailedAt.Clear();
            Want(_visible, force: true);
        }
    }

    /// <summary>Something is waiting: a request from a button, or a stale section of the page in view.</summary>
    public static bool NeedsRun()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lock (WantGate)
            return Pending.Count > 0 || MybkSections.Pick(_visible, MybkStore.Latest(), now, force: false, FailedAt, staleBefore: _staleBefore).Count > 0;
    }

    private static (List<string> Sections, bool Force) TakeWanted(JsonObject? prev, long now)
    {
        lock (WantGate)
        {
            if (Pending.Count > 0)
            {
                List<string> list = [.. Pending];
                var force = _pendingForce;
                Pending.Clear();
                _pendingForce = false;
                return (MybkSections.Pick(list, prev, now, force, FailedAt), force);
            }
            return (MybkSections.Pick(_visible, prev, now, force: false, FailedAt, staleBefore: _staleBefore), false);
        }
    }

    /// <summary>
    /// Load the sections the user is looking at (<see cref="Show"/>) that are stale, or what a button asked for (<see cref="Want"/>).
    /// One section group per run, calls spaced like a person browsing (sources.mybk.gapMs plus jitter, in MybkBrowser). Student id and
    /// term are re-read only when older than 12 hours. Everything not fetched keeps the saved copy. Nothing wanted: no request at all.
    /// The hub's <paramref name="force"/> does not widen the set: after sign-in only the stale sections in view load.
    /// </summary>
    public async Task SyncAsync(Action<string> log, bool force, CancellationToken ct)
    {
        var browser = runner() ?? throw new InvalidOperationException("Cần mở app để đồng bộ MyBK.");
        var api = Config.Node("sources.mybk.api") as JsonObject ?? throw new InvalidOperationException("Thiếu sources.mybk.api trong cấu hình.");
        // Bản đã lưu: phần nào lần này không đọc được thì giữ phần của lần trước, không ghi đè bằng danh sách rỗng.
        var prev = MybkStore.Latest() is { Count: > 0 } latest ? latest : null;
        var special = Config.Map("sources.mybk.specialScores");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var refresh = Interlocked.Exchange(ref _refreshNext, 0) == 1;
        if (refresh) Want([MybkSections.Curriculum, MybkSections.Grades], force: true);
        var (sections, _) = TakeWanted(prev, now);
        if (sections.Count == 0)
        {
            log("MyBK: không có mục nào cần tải lại");
            return;
        }
        try { await SyncSectionsAsync(browser, api, prev, special, now, refresh, force, sections, log, ct); }
        catch
        {
            // Session expired, network down: these sections wait 10 minutes (or a button press) instead of restarting at once.
            lock (WantGate) foreach (var s in sections) FailedAt[s] = now;
            throw;
        }
    }

    private async Task SyncSectionsAsync(IBrowserRunner browser, JsonObject api, JsonObject? prev, IReadOnlyDictionary<string, string> special,
        long now, bool refresh, bool force, List<string> sections, Action<string> log, CancellationToken ct)
    {
        var needBasis = !MybkSections.BasisFreshIn(prev, now);
        var calls = sections.SelectMany(s => MybkSections.All[s].Apis).Where(a => a != MybkSections.RegistrationPage).Distinct().ToList();
        var page = sections.Contains(MybkSections.Registration);
        Log.Info($"MyBK: tải {string.Join(", ", sections)}{(needBasis ? " (kèm thông tin sinh viên, học kỳ)" : "")}");

        // Progress: one step per API, the registration page counts 2. Until the first API answers (opening MyBK may go through SSO) the
        // bar is indeterminate.
        var all = (needBasis ? MybkSections.BasisApis : []).Concat(calls).ToList();
        var plan = new SyncPlan().Add("sync.mybk.open", weight: null);
        foreach (var n in all) plan.Add("mybk:" + n);
        if (page) plan.Add("sync.mybk.registration", 2);
        void Report(SyncProgress p) => log(SyncSignal.Progress(p));
        var doneCalls = 0;
        void OnEach(string name)
        {
            doneCalls++;
            plan.SetWeight("sync.mybk.open", 0);
            var next = all.IndexOf(name) + 1;
            if (next > 0 && next < all.Count) Report(plan.Enter("mybk:" + all[next], "sync.mybk.api", Math.Min(doneCalls + 1, all.Count), all.Count));
            else Report(plan.Advance(1, doneCalls, all.Count));
        }
        Report(plan.Enter("sync.mybk.open"));
        var missing = new Dictionary<string, string>();   // API MyBK báo không có dữ liệu (code 404): tên, câu của MyBK

        JsonObject student, term;
        long basisAt;
        if (needBasis)
        {
            var first = await RunAsync(browser, api, MybkSections.BasisApis, new(), missing, log, OnEach, ct);
            var info = first.GetValueOrDefault("info") as JsonObject;
            if (info?["id"] is null) throw new SyncException(SyncErrorKind.Data, "Không đọc được thông tin sinh viên từ MyBK.");
            var sems = first.GetValueOrDefault("semesters") as JsonArray ?? [];
            var cur = sems.OfType<JsonObject>().FirstOrDefault(s => s["isCurrent"] is JsonValue v && v.TryGetValue<bool>(out var b) && b)
                      ?? sems.OfType<JsonObject>().MaxBy(s => MybkNormalize.Long(s["code"]) ?? 0);
            student = new JsonObject
            {
                ["name"] = $"{MybkNormalize.Str(info["lastName"])} {MybkNormalize.Str(info["firstName"])}".Trim(),
                ["mssv"] = MybkNormalize.Str(info["code"]),
                ["class"] = MybkNormalize.Str(info["classCode"]),
                ["id"] = info["id"]!.DeepClone(),   // MyBK internal id, needed by the schedule API (new in mybk.json 06/11/2026)
            };
            term = new JsonObject { ["code"] = cur?["code"]?.ToString() ?? "", ["name"] = MybkNormalize.Str(cur?["nameVi"]) ?? "" };
            basisAt = now;
        }
        else
        {
            student = (JsonObject)prev!["student"]!.DeepClone();
            term = (JsonObject)prev["term"]!.DeepClone();
            basisAt = MybkNormalize.Long(prev["basisAt"]) ?? now;
        }
        var sem = term["code"]?.ToString() ?? "";
        var args = new Dictionary<string, string>
        {
            ["id"] = student["id"]?.ToString() ?? "",
            ["mssv"] = student["mssv"]?.ToString() ?? "",
            ["sem"] = sem,
            ["year"] = sem.Length >= 4 ? sem[..4] : "",
            ["term"] = sem.Length > 4 ? sem[4..] : "",
        };
        // Nút "Cập nhật CTĐT trên MyBK" (hay sources.mybk.refreshOnForce cho đồng bộ bấm tay) nhờ MyBK tính lại điểm tích lũy và tiến độ CTĐT,
        // giống nút Cập nhật trên hai trang của MyBK. Đây là POST ghi lên MyBK nên chỉ chạy khi người dùng chủ động, không bao giờ ở lượt nền.
        if (refresh || (force && Config.Bool("sources.mybk.refreshOnForce", false) && sections.Contains(MybkSections.Curriculum)))
            await RefreshServerAsync(browser, api, args, log, ct);
        var got = calls.Count == 0 ? new Dictionary<string, JsonNode?>()
            : await RunAsync(browser, api, [.. calls], args, missing, log, OnEach, ct, TimeSpan.FromSeconds(Math.Max(5, Config.Int("sources.mybk.extraTimeoutSeconds", 15))));

        var output = Build(student, term, basisAt, got, missing, prev, special);
        var sameTerm = prev?["term"]?["code"]?.ToString() == sem;
        output["registration"] = sameTerm ? prev?["registration"]?.DeepClone() ?? new JsonArray() : new JsonArray();
        output["registrationAt"] = sameTerm ? prev?["registrationAt"]?.DeepClone() ?? 0 : 0;
        var pageOk = false;
        if (page)
        {
            try
            {
                Report(plan.Enter("sync.mybk.registration"));
                var html = await browser.PageAsync(Config.Str("sources.mybk.registration"), ct, "Đợt Đăng ký");
                var rounds = ParseRegistration(html);
                // Trang không có bảng đợt đăng ký (cột "Đợt Đăng ký") là đã mở nhầm trang: báo, giữ bản cũ, không lưu danh sách rỗng.
                if (rounds.Count == 0 && !html.Contains("Đợt Đăng ký", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Trang đăng ký môn không có bảng đợt đăng ký (có thể chưa vào được hệ thống đăng ký).");
                output["registration"] = rounds;
                output["registrationAt"] = now;
                pageOk = true;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.Warn($"MyBK đăng ký môn: {e.Message}");
                log(SyncSignal.Warn("đợt đăng ký môn", e.Message));
            }
        }

        // Fetch time per section: set only when every call of it answered (data, or MyBK said "no data"). A new term starts fresh.
        var at = sameTerm && prev?["sectionsAt"] is JsonObject old ? (JsonObject)old.DeepClone() : new JsonObject();
        lock (WantGate)
            foreach (var s in sections)
            {
                var ok = MybkSections.All[s].Apis.All(a => a == MybkSections.RegistrationPage ? pageOk : got.ContainsKey(a) || missing.ContainsKey(a));
                if (ok) { at[s] = now; FailedAt.Remove(s); }
                else FailedAt[s] = now;
            }
        output["sectionsAt"] = at;
        MybkStore.Write(output);
        log($"Xong: {string.Join(", ", sections)}");
        Report(plan.Finish());
    }

    /// <summary>
    /// Dựng mybk.json: phần của các API vừa đọc (<paramref name="got"/>) thay mới, mọi phần khác giữ bản đã lưu (một API chập chờn
    /// hay một mục không được tải lần này không xóa mất dữ liệu đang có).
    /// </summary>
    private static JsonObject Build(JsonObject student, JsonObject term, long basisAt, Dictionary<string, JsonNode?> got,
        IReadOnlyDictionary<string, string> missing, JsonObject? prev, IReadOnlyDictionary<string, string> special)
    {
        var sem = term["code"]?.ToString() ?? "";
        var output = new JsonObject
        {
            ["syncedAt"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["student"] = student,
            ["term"] = term,
            ["basisAt"] = basisAt,
        };
        // Phần theo kỳ (lịch học, lịch thi, môn đã đăng ký) chỉ giữ bản cũ khi vẫn cùng kỳ; phần cộng dồn (điểm, CTĐT…) thì luôn giữ.
        var sameTerm = prev?["term"]?["code"]?.ToString() == sem;
        void Put(string key, bool ok, Func<JsonNode> norm, bool perTerm = false) =>
            output[key] = ok || prev?[key] is null || (perTerm && !sameTerm) ? norm() : prev[key]!.DeepClone();
        Put("schedule", got.ContainsKey("schedule"), () => MybkNormalize.Schedule(got.GetValueOrDefault("schedule") as JsonArray), perTerm: true);
        if (output["schedule"] is JsonArray fresh && MybkNormalize.KeepOldSchedule(fresh, prev?["schedule"], sameTerm))
        {
            Log.Warn($"MyBK: lịch học trả về rỗng giữa kỳ {sem}, giữ bản đã lưu ({prev!["schedule"]!.AsArray().Count} buổi)");
            output["schedule"] = prev["schedule"]!.DeepClone();
        }
        Put("exams", got.ContainsKey("exams"), () => MybkNormalize.Exams(got.GetValueOrDefault("exams")), perTerm: true);
        Put("gradeTerms", got.ContainsKey("gradesTerms"), () => MybkNormalize.GradeTerms(got.GetValueOrDefault("gradesTerms") as JsonArray));
        Put("grades", got.ContainsKey("gradesCourses"), () => MybkNormalize.Grades(got.GetValueOrDefault("gradesCourses") as JsonArray, special));
        // MyBK báo chưa có CTĐT (sinh viên năm nhất): ghi câu đó thay cho danh sách cũ, như trang MyBK; trang MyBK của app hiện rõ lý do.
        if (got.TryGetValue("curriculum", out var rows) && !MybkNormalize.CurriculumReady(rows as JsonArray))
        {
            Log.Info("MyBK: danh sách CTĐT chưa sẵn sàng (không có msg \"done\"), giữ bản đã lưu");
            got.Remove("curriculum");
        }
        var noProgram = missing.GetValueOrDefault("curriculum");
        Put("curriculum", got.ContainsKey("curriculum") || noProgram is not null, () =>
        {
            var c = MybkNormalize.Curriculum(got.GetValueOrDefault("curriculumInfo") as JsonObject, got.GetValueOrDefault("curriculum") as JsonArray, special,
                prev?["curriculum"] as JsonObject);
            if (noProgram is not null) c["missing"] = noProgram;
            return c;
        });
        Put("components", got.ContainsKey("components"), () => MybkNormalize.Components(got.GetValueOrDefault("components") as JsonArray, special));
        Put("decisions", got.ContainsKey("decisions"), () => MybkNormalize.Decisions(got.GetValueOrDefault("decisions") as JsonArray));
        Put("socialWork", got.ContainsKey("socialWork"), () => MybkNormalize.SocialWork(got.GetValueOrDefault("socialWork") as JsonObject));
        Put("fees", got.ContainsKey("fees"), () => MybkNormalize.Fees(got.GetValueOrDefault("fees") as JsonArray));
        Put("registered", got.ContainsKey("registered"), () => MybkNormalize.Registered(got.GetValueOrDefault("registered") as JsonArray, sem), perTerm: true);
        return output;
    }

    /// <summary>Gọi các API theo tên trong config, điền sẵn {id} {mssv} {sem} {year} {term}. Request POST gửi MSSV làm body.</summary>
    private static async Task<Dictionary<string, JsonNode?>> RunAsync(IBrowserRunner browser, JsonObject api, string[] names,
        Dictionary<string, string> args, Dictionary<string, string> missing, Action<string> log, Action<string> onEach, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var baseUrl = Config.Str("sources.mybk.site").TrimEnd('/') + "/api/";
        string Fill(string s) => args.Aggregate(s, (acc, kv) => acc.Replace("{" + kv.Key + "}", kv.Value));
        var reqs = names.Select(n => api[n] switch
        {
            JsonValue v => new FetchRequest(n, baseUrl + Fill(v.GetValue<string>()), Timeout: timeout),
            JsonObject o => new FetchRequest(n, baseUrl + Fill(o["post"]!.GetValue<string>()), "POST",
                JsonSerializer.Serialize(Fill(o["body"]?.GetValue<string>() ?? "{mssv}"), CoreJson.Default.String), timeout),
            _ => throw new InvalidOperationException($"Cấu hình API MyBK sai: {n}"),
        }).ToList();
        var (res, retried) = await MybkReply.FetchAsync(browser, reqs, TimeSpan.FromSeconds(1.5), ct, onEach);
        if (retried.Count > 0) Log.Info($"MyBK: gọi lại một lần {string.Join(", ", retried)} sau lỗi");
        var output = new Dictionary<string, JsonNode?>();
        foreach (var n in names)
        {
            var reply = res.TryGetValue(n, out var r) ? MybkReply.Read(r) : new(null, "không gửi được (MyBK đang giới hạn, đã tạm dừng)", false);
            if (reply.Missing is { } none)
            {
                // MyBK trả lời được nhưng không có dữ liệu này: không phải lỗi, không báo người dùng; Build ghi lại câu của MyBK nếu cần hiện.
                Log.Info($"MyBK {n}: MyBK báo không có dữ liệu ({none})");
                missing[n] = none;
                continue;
            }
            if (reply.Why is not { } why) { output[n] = reply.Data; continue; }
            // Ghi kèm tóm tắt body (field báo lỗi, title trang), không chép cả body: có thể chứa dữ liệu cá nhân.
            Log.Warn($"MyBK {n}: {why}{(r is null ? "" : ", " + MybkNormalize.ErrorSummary(r.Body))}");
            if (!MybkReply.Silent(n)) log(SyncSignal.Warn(Label(n), $"{n}: {why}"));
        }
        return output;
    }

    private static int _refreshNext;
    private static readonly string[] RefreshApis = ["refreshGrades", "refreshCurriculum"];

    /// <summary>Đánh dấu lần đồng bộ MyBK kế tiếp gọi hai API cập nhật của MyBK trước khi đọc (người dùng bấm nút Cập nhật).</summary>
    public static void RequestRefresh() => Interlocked.Exchange(ref _refreshNext, 1);

    /// <summary>
    /// Gọi lần lượt refreshGrades rồi refreshCurriculum (thứ tự như người dùng bấm tay). Gọi một lần, không gọi lại tự động vì là POST ghi;
    /// lỗi chỉ báo cảnh báo, phần đọc phía sau vẫn chạy như thường. Hết phiên thì để RunAsync ở bước đọc báo.
    /// </summary>
    private static async Task RefreshServerAsync(IBrowserRunner browser, JsonObject api, Dictionary<string, string> args, Action<string> log, CancellationToken ct)
    {
        var baseUrl = Config.Str("sources.mybk.site").TrimEnd('/') + "/api/";
        string Fill(string s) => args.Aggregate(s, (acc, kv) => acc.Replace("{" + kv.Key + "}", kv.Value));
        var reqs = RefreshApis.Where(n => api[n] is JsonObject).Select(n =>
        {
            var o = (JsonObject)api[n]!;
            return new FetchRequest(n, baseUrl + Fill(o["post"]!.GetValue<string>()), "POST",
                JsonSerializer.Serialize(Fill(o["body"]?.GetValue<string>() ?? "{mssv}"), CoreJson.Default.String));
        }).ToList();
        if (reqs.Count == 0) return;
        var res = await browser.FetchAsync(reqs, ct);
        foreach (var r in reqs)
        {
            var status = res.TryGetValue(r.Name, out var x) ? x.Status : 0;
            Log.Info($"MyBK {r.Name}: HTTP {status} (nút Cập nhật)");
            if (status != 200) log(SyncSignal.Warn("cập nhật trên MyBK", $"{r.Name}: HTTP {status}"));
        }
    }

    /// <summary>Tên dễ hiểu của từng API MyBK để báo lỗi.</summary>
    private static string Label(string api) => api switch
    {
        "info" => "thông tin sinh viên",
        "semesters" => "danh sách học kỳ",
        "schedule" => "thời khóa biểu",
        "exams" => "lịch thi",
        "gradesCourses" or "gradesTerms" => "bảng điểm",
        "curriculumInfo" or "curriculum" => "chương trình đào tạo",
        "components" => "điểm thành phần",
        "decisions" => "quyết định học vụ",
        "socialWork" => "ngày công tác xã hội",
        "fees" => "học phí",
        "registered" => "môn đã đăng ký",
        _ => api,
    };

    /// <summary>Bảng các đợt đăng ký (xem <see cref="MybkNormalize.ParseRegistration"/>).</summary>
    public static JsonArray ParseRegistration(string html) => MybkNormalize.ParseRegistration(html);
}
