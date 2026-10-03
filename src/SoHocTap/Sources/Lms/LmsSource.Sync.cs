using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Files;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

// Một lượt đồng bộ LMS: lớp, lịch, quiz, thông báo, điểm, rồi tới tài liệu.
public sealed partial class LmsSource
{
    // ------------------------------------------------------------------ sync

    /// <summary>Sync và tải theo mục cùng ghi vào index file (lms-files.json) nên không được chạy song song.</summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task SyncAsync(Action<string> log, bool force, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try { await SyncCoreAsync(log, force, ct); }
        finally { Gate.Release(); }
    }

    // Kênh báo lỗi từng phần của lần đồng bộ đang chạy (các hàm Collect* là static, không nhận log).
    private static readonly AsyncLocal<Action<string>?> SyncLog = new();

    /// <summary>
    /// Một phần không đọc được: ghi log và báo lên giao diện, đồng bộ vẫn chạy tiếp. Riêng "nopermissions" (giảng viên không cho xem,
    /// vd. sổ điểm) là bình thường, chỉ ghi log. Lỗi lạ (không phải lỗi mạng, lỗi LMS, file bận) ghi cả stack trace để sửa được.
    /// </summary>
    private static void Warn(string what, Exception e)
    {
        if (e is LmsException { Code: "nopermissions" or "nopermission" }) { Log.Debug($"LMS {what}: {e.Message}"); return; }
        if (e is LmsException or HttpRequestException or TimeoutException or IOException or UnauthorizedAccessException) Log.Warn($"LMS {what}: {e.Message}");
        else Log.Error($"LMS {what}", e);
        SyncLog.Value?.Invoke(SyncSignal.Warn(what, e.Message));
    }

    /// <summary>
    /// Lỗi này chỉ hỏng một phần, bỏ qua được (ghi cảnh báo, chạy tiếp)? Không bỏ qua: hết phiên (cả lượt phải dừng để báo đăng nhập
    /// lại), người dùng/app hủy, hết RAM. Timeout của HttpClient (TaskCanceledException khi ct chưa hủy) thì bỏ qua được.
    /// </summary>
    private static bool Recoverable(Exception e, CancellationToken ct) =>
        e is not (SessionExpiredException or OutOfMemoryException) && !(e is OperationCanceledException && ct.IsCancellationRequested);

    /// <summary>Đọc một phần của lượt đồng bộ; lỗi bỏ qua được thì báo cảnh báo và trả <paramref name="fallback"/>.</summary>
    private static async Task<T> PartAsync<T>(string what, Func<Task<T>> work, T fallback, CancellationToken ct)
    {
        try { return await work(); }
        catch (Exception e) when (Recoverable(e, ct)) { Warn(what, e); return fallback; }
    }

    /// <summary>Bản đồng bộ (không async) của <see cref="PartAsync"/>, cho việc ghi file trên máy.</summary>
    private static bool Part(string what, Action work, CancellationToken ct)
    {
        try { work(); return true; }
        catch (Exception e) when (Recoverable(e, ct)) { Warn(what, e); return false; }
    }

    private async Task SyncCoreAsync(Action<string> log, bool force, CancellationToken ct)
    {
        SyncLog.Value = log;
        // sources.lms.autoDownload = false thì chỉ đọc lịch, quiz, thông báo, cấu trúc khóa; tài liệu thì tải tay theo mục (DownloadSectionsAsync).
        var autoDownload = Config.Bool("sources.lms.autoDownload", false);
        var callsBefore = Calls;
        // Tiến độ theo trọng số (xấp xỉ số request): kiểm tra 1 mỗi khóa, mỗi phần 1 mỗi lần gọi API, tải tệp theo dung lượng.
        // Số khóa, số tệp chưa biết thì chưa tính được tổng: thanh chạy vô định tới khi có danh sách lớp.
        var plan = new SyncPlan()
            .Add("sync.lms.check", weight: null)
            .Add("sync.lms.groups")
            .Add("sync.lms.events", 2)
            .Add("sync.lms.quizzes", 2)
            .Add("sync.lms.news", weight: null)
            .Add("sync.lms.grades", weight: null);
        if (autoDownload) plan.Add("sync.lms.files", weight: null, reserved: true).Add("sync.lms.assign");
        plan.CapUntilDone("sync.lms.check", 0.33);
        void Report(SyncProgress p) => log(SyncSignal.Progress(p));
        Report(plan.Enter("sync.lms.check", "sync.lms.courses"));
        var info = await CallAsync("core_webservice_get_site_info", [], ct);
        var uid = info["userid"]?.GetValue<long>() ?? throw new SyncException(SyncErrorKind.Data, "LMS không trả về userid.");
        var raw = await CallAsync("core_enrol_get_users_courses", [Arg("userid", uid)], ct) as JsonArray
                  ?? throw new SyncException(SyncErrorKind.Data, "LMS trả danh sách lớp không đúng dạng.");
        var skip = Config.List("sources.lms.skipCourses");
        var metas = raw.OfType<JsonObject>()
            .Where(c => c["fullname"]?.GetValue<string>() is { } n && Organizer.SubjectOf(n) is not null && !skip.Any(n.Contains))
            .Select(CourseMeta).ToList();
        var term = CurrentTerm(metas);
        var state = ReadCourseState();
        var index = JsonStore.ReadObject(FilesIndex);
        var hashes = new Dictionary<string, Dictionary<string, string>>();
        var newFiles = new List<JsonObject>();
        // Lớp có tài liệu cần xem/tải: tải sau khi đã lưu lịch, quiz, điểm. Fresh = lần đầu đọc lớp (file không tính là "mới").
        var pending = new List<(CourseInfo M, List<(JsonObject Sec, JsonObject Mod, JsonObject F)> Files, bool Fresh, string Key, JsonObject Done)>();
        var changedModules = new HashSet<long>();   // module có update từ lần check trước (vd. diễn đàn có bài mới)
        int nNew = 0, nChanged = 0, nChecked = 0, nSkipped = 0;
        var pastDays = Config.Int("sources.lms.pastSyncDays", 7);

        // Mặc định chỉ đọc lớp của học kỳ hiện tại (sources.lms.pastTerms = false): lớp kỳ trước không gọi API nào.
        var pastTerms = Config.Bool("sources.lms.pastTerms", false);
        var toCheck = metas.Where(m => pastTerms || m.Term == term).OrderByDescending(m => m.Term).ToList();   // kỳ mới làm trước để bản mới giữ tên gốc
        var current = metas.Where(m => m.Term == term).ToList();
        plan.SetWeight("sync.lms.check", Math.Max(1, toCheck.Count));
        plan.SetWeight("sync.lms.news", Math.Max(1, current.Count));    // diễn đàn: khoảng một request mỗi lớp
        plan.SetWeight("sync.lms.grades", Math.Max(1, current.Count));  // sổ điểm: một request mỗi lớp
        var nth = 0;
        foreach (var m in toCheck)
        {
            Report(plan.Advance(nth, nth + 1, toCheck.Count));
            nth++;
            // Một lớp lỗi (khóa bị ẩn, LMS báo lỗi riêng lớp đó) không làm hỏng cả lượt: trạng thái lớp đó không đổi, lần sau đọc lại.
            var outcome = await PartAsync($"lớp {m.Subject}{(m.Part is null ? "" : $" ({m.Part})")}",
                () => CheckCourseAsync(m, term, state, index, changedModules, force, autoDownload, pastDays, log, ct), (CourseCheck?)null, ct);
            switch (outcome)
            {
                case { Kind: CourseCheckKind.New }: nNew++; break;
                case { Kind: CourseCheckKind.Changed }: nChanged++; break;
                case { Kind: CourseCheckKind.Unchanged }: nChecked++; break;
                case { Kind: CourseCheckKind.ChangedAfterCheck }: nChecked++; nChanged++; break;
                case { Kind: CourseCheckKind.Skipped }: nSkipped++; break;
            }
            if (outcome?.Pending is { } p) pending.Add(p);
        }

        // Lịch, quiz, thông báo, điểm lưu trước rồi mới tải tài liệu: lần đầu đăng nhập có thể phải tải hàng trăm file (vài phút),
        // không để các trang trống trong lúc đó. Tài liệu tải sau, giãn cách theo sources.lms.fileGapMs.
        var prev = LmsStore.Latest();   // kể cả lượt trước không có gì mới (chưa ghi ra đĩa)
        var recent = (prev["newFiles"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(x => (x["seenAt"]?.GetValue<long>() ?? 0) > Now() - 14 * 86400).Select(x => x.DeepClone()).ToList();
        foreach (var x in newFiles) x["seenAt"] = Now();
        JsonArray? events = null, quizzes = null, announcements = null, grades = null;
        JsonObject Output(long syncedAt) => new()
        {
            ["syncedAt"] = syncedAt,
            ["user"] = info["fullname"]?.GetValue<string>(),
            ["term"] = term,
            ["courses"] = new JsonArray(metas.Select(m => (JsonNode)m.ToJson()).ToArray()),
            // Phần chưa đọc tới (lượt bị dừng giữa chừng) thì giữ bản của lần trước.
            ["events"] = events ?? prev["events"]?.DeepClone() ?? new JsonArray(),
            ["quizzes"] = quizzes ?? prev["quizzes"]?.DeepClone() ?? new JsonArray(),
            ["newFiles"] = new JsonArray(newFiles.Cast<JsonNode>().Concat(recent.Select(x => x!.DeepClone())).Take(300).ToArray()),
            ["announcements"] = announcements ?? prev["announcements"]?.DeepClone() ?? new JsonArray(),
            ["grades"] = grades ?? prev["grades"]?.DeepClone() ?? new JsonArray(),
            ["lastRun"] = new JsonObject { ["new"] = nNew, ["changed"] = nChanged, ["checked"] = nChecked, ["skipped"] = nSkipped, ["files"] = newFiles.Count },
            ["forumsSeen"] = prev["forumsSeen"]?.DeepClone(),
        };
        try
        {
            // Mỗi bước lỗi thì giữ phần của lần trước (không xóa lịch, điểm đang có vì một API chập chờn).
            Report(plan.Enter("sync.lms.groups"));
            // Nhóm của mình: lọc mốc và mục điểm của nhóm khác (lớp thí nghiệm dùng chung cho nhiều nhóm).
            var groups = await PartAsync("nhóm lớp LMS", () => MyGroupsAsync(current, uid, ct), new Dictionary<long, HashSet<string>>(), ct);
            Report(plan.Enter("sync.lms.events"));
            events = await PartAsync("lịch và bài tập LMS", () => CollectEventsAsync(current, groups, ct), prev["events"]?.DeepClone() as JsonArray ?? [], ct);
            Report(plan.Enter("sync.lms.quizzes"));
            quizzes = await PartAsync("quiz LMS", () => CollectQuizzesAsync(current, prev["quizzes"] as JsonArray, prev["syncedAt"]?.GetValue<long>() ?? 0, log, ct),
                prev["quizzes"]?.DeepClone() as JsonArray ?? [], ct);
            Report(plan.Enter("sync.lms.news"));
            announcements = await PartAsync("thông báo LMS", () => CollectAnnouncementsAsync(raw, current, uid, prev, changedModules, ct),
                prev["announcements"]?.DeepClone() as JsonArray ?? [], ct);
            Report(plan.Enter("sync.lms.grades"));
            grades = await PartAsync("sổ điểm LMS", () => CollectGradesAsync(current, uid, ct), prev["grades"]?.DeepClone() as JsonArray ?? [], ct);
        }
        catch (SessionExpiredException) when (events is not null)
        {
            // Hết phiên giữa chừng: phần đã đọc vẫn lưu, giữ syncedAt của lần trước vì lượt này chưa xong; lỗi vẫn báo như thường.
            LmsStore.Write(Output(prev["syncedAt"]?.GetValue<long>() ?? 0));
            Log.Info("LMS: hết phiên giữa chừng, đã lưu phần đọc được");
            throw;
        }
        var output = Output(Now());
        LmsStore.Write(output);
        log(SyncSignal.DataReady);   // giao diện đọc lại ngay: đã có lịch, quiz, điểm

        var total = pending.Sum(p => p.Files.Count);
        if (total > 0) log($"Đang xem {total} tài liệu của {pending.Count} lớp...");
        // Tải tệp chiếm nửa thanh, chia theo dung lượng LMS báo; có tệp không rõ dung lượng thì chia đều theo số tệp.
        static long Size(JsonObject f) => f["filesize"]?.GetValue<long>() ?? 0;
        var bySize = pending.SelectMany(p => p.Files).All(x => Size(x.F) > 0);
        double Weight(JsonObject f) => bySize ? Size(f) : 1;
        plan.SetWeight("sync.lms.files", pending.Sum(p => p.Files.Sum(x => Weight(x.F))));
        double doneWeight = 0;
        var seen = 0;
        foreach (var p in pending)
        {
            var allOk = true;
            foreach (var (sec, mod, f) in p.Files)
            {
                Report(seen == 0 ? plan.Enter("sync.lms.files", count: 1, of: total, detail: p.M.Subject)
                                 : plan.Advance(doneWeight, seen + 1, total, p.M.Subject));
                // File đang mở ở chương trình khác (PDF đang đọc), ổ đầy, mạng chập: bỏ file này, đồng bộ chạy tiếp, lần sau thử lại.
                var got = await PartAsync($"tài liệu {f["filename"]}", async () => (Ok: true, File: await SyncFileAsync(p.M, sec, mod, f, index, hashes, log, ct)), (Ok: false, File: (JsonObject?)null), ct);
                allOk &= got.Ok;
                if (got.File is not null && !p.Fresh) newFiles.Add(got.File);    // lần đầu đọc lớp thì không tính là "mới"
                if (got.File is not null) JsonStore.Write(FilesIndex, index);
                doneWeight += Weight(f);
                if (++seen % 20 == 0) log($"Tài liệu: {seen}/{total}");
            }
            // Lớp có file chưa tải được thì không đánh dấu đã đồng bộ: lần sau còn thấy cập nhật mà tải lại.
            if (!allOk) continue;
            state[p.Key] = p.Done;
            SaveCourseState(state);                                 // ghi ngay, lỡ tắt app giữa chừng thì lần sau làm tiếp
        }
        JsonStore.Write(FilesIndex, index);
        Organizer.SaveHashCache();
        if (autoDownload)
        {
            Report(plan.Enter("sync.lms.assign"));
            await PartAsync("đề bài tập LMS", async () => { await SaveAssignmentsAsync(current, events, index, hashes, log, ct); return true; }, false, ct);
        }
        foreach (var x in newFiles) x["seenAt"] = Now();
        output["newFiles"] = new JsonArray(newFiles.Cast<JsonNode>().Concat(recent).Take(300).ToArray());
        output["lastRun"]!["files"] = newFiles.Count;
        LmsStore.Write(output);
        log($"Xong: {nNew} lớp mới, {nChanged} lớp có thay đổi, {nChecked - Math.Min(nChecked, nChanged)} lớp không đổi, {nSkipped} lớp kỳ trước chưa tới hạn, {newFiles.Count} tài liệu mới, {Calls - callsBefore} request API");
        Log.Info($"Sync LMS: {Calls - callsBefore} request, check {nChecked} lớp, {nChanged} lớp có đổi");
        Report(plan.Finish());
    }

    // ------------------------------------------------------------------ lms-courses.json: dấu "checked" giữ trong RAM

    private static readonly object CourseGate = new();
    private static JsonObject? _courseStatePending;

    private static JsonObject ReadCourseState()
    {
        lock (CourseGate) return (JsonObject?)_courseStatePending?.DeepClone() ?? JsonStore.ReadObject(CourseState);
    }

    private static void SaveCourseState(JsonObject state)
    {
        lock (CourseGate)
        {
            JsonStore.Write(CourseState, state);
            _courseStatePending = null;
        }
    }

    /// <summary>App thoát: ghi lms.json (lần kiểm tra cuối) và dấu "checked" của các lớp không đổi.</summary>
    public void Flush()
    {
        LmsStore.Flush();
        JsonObject? pending;
        lock (CourseGate) { pending = _courseStatePending; _courseStatePending = null; }
        if (pending is not null) JsonStore.Write(CourseState, pending);
    }

    private enum CourseCheckKind { New, Changed, Unchanged, ChangedAfterCheck, Skipped }

    private sealed record CourseCheck(CourseCheckKind Kind,
        (CourseInfo M, List<(JsonObject Sec, JsonObject Mod, JsonObject F)> Files, bool Fresh, string Key, JsonObject Done)? Pending = null);

    /// <summary>
    /// Kiểm một lớp: lớp mới (hoặc force) thì đọc hết nội dung; lớp đã biết thì hỏi core_course_get_updates_since, có đổi mới đọc lại.
    /// Lưu cấu trúc khóa, ghi trạng thái lớp nếu không còn file phải tải; còn file thì trả về để tải sau.
    /// </summary>
    private static async Task<CourseCheck?> CheckCourseAsync(CourseInfo m, string term, JsonObject state, JsonObject index, HashSet<long> changedModules,
        bool force, bool autoDownload, int pastDays, Action<string> log, CancellationToken ct)
    {
        var key = m.Id.ToString();
        var st = state[key] as JsonObject;
        var started = Now();
        HashSet<long>? only = null;                             // null = mọi mục
        CourseCheckKind kind;
        if (force || st is null)
        {
            if (st is null) { kind = CourseCheckKind.New; log($"  lớp mới: {m.Subject} {m.Part} {m.Term}"); } else kind = CourseCheckKind.Changed;
        }
        else
        {
            if (m.Term != term && started - (st["checked"]?.GetValue<long>() ?? 0) < pastDays * 86400) return new CourseCheck(CourseCheckKind.Skipped);
            var upd = await CallAsync("core_course_get_updates_since",
                [Arg("courseid", m.Id), Arg("since", Math.Max(0, (st["synced"]?.GetValue<long>() ?? 0) - 300))], ct);
            var lastCheck = (st["checked"]?.GetValue<long>() ?? 0) - 300;
            foreach (var i in (upd["instances"] as JsonArray ?? []).OfType<JsonObject>())
                if (i["contextlevel"]?.GetValue<string>() == "module"
                    && (i["updates"] as JsonArray ?? []).Any(u => (u?["timeupdated"]?.GetValue<long?>() ?? long.MaxValue) > lastCheck))
                    changedModules.Add(i["id"]!.GetValue<long>());
            only = (upd["instances"] as JsonArray ?? []).OfType<JsonObject>()
                .Where(i => i["contextlevel"]?.GetValue<string>() == "module"
                            && (i["updates"] as JsonArray ?? []).Any(u => ContentUpdates.Contains(u?["name"]?.GetValue<string>() ?? "")))
                .Select(i => i["id"]!.GetValue<long>()).ToHashSet();
            if (only.Count == 0)
            {
                // Lớp không đổi: chỉ dấu "checked" mới, giữ trong RAM, ghi khi có thay đổi khác hoặc khi app thoát (Flush).
                st["checked"] = started;
                lock (CourseGate) _courseStatePending = state;
                return new CourseCheck(CourseCheckKind.Unchanged);
            }
            kind = CourseCheckKind.ChangedAfterCheck;
        }
        var contents = await CallAsync("core_course_get_contents", [Arg("courseid", m.Id)], ct) as JsonArray
                       ?? throw new SyncException(SyncErrorKind.Data, "LMS trả nội dung khóa không đúng dạng.");
        Part($"cấu trúc khóa {m.Subject}", () => SaveStructure(m, contents, index), ct);
        var done = new JsonObject { ["synced"] = started, ["checked"] = started, ["term"] = m.Term };
        var files = Downloadable(contents).Where(x => autoDownload && (only is null || only.Contains(x.Mod["id"]!.GetValue<long>()))).ToList();
        if (files.Count == 0)
        {
            state[key] = done;
            SaveCourseState(state);
            return new CourseCheck(kind);
        }
        return new CourseCheck(kind, (m, files, st is null, key, done));
    }
}
