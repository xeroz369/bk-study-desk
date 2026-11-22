using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Files;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

public sealed partial class LmsSource
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<bool> SyncMessagesAsync(CancellationToken ct)
    {
        if (!HasToken || !await Gate.WaitAsync(0, ct)) return false;
        try
        {
            var info = await CallAsync("core_webservice_get_site_info", [], ct);
            if (info["userid"]?.GetValue<long>() is not { } uid) return false;
            if (await OptionalAsync("tin nhắn LMS", () => CollectMessagesAsync(uid, LmsMessagesStore.Raw(), ct), ct) is not { } messages) return false;
            if (!HasToken) return false;
            LmsMessagesStore.Write(messages);
            await LmsImages.SaveAsync(LmsImages.HtmlOf(null, messages, null), ct);
            return true;
        }
        catch (Exception e) when (Recoverable(e, ct) || e is SessionExpiredException) { Log.Debug($"Tin nhắn LMS (lượt nhanh): {e.Message}"); return false; }
        finally { Gate.Release(); }
    }

    public static bool CourseCheckDue(JsonObject state, long courseId, long now, long freshSeconds) =>
        now - (state[courseId.ToString(System.Globalization.CultureInfo.InvariantCulture)]?["checked"]?.GetValue<long>() ?? 0) >= freshSeconds;

    public static async Task<bool> CheckCourseOnOpenAsync(long courseId, CancellationToken ct)
    {
        if (!HasToken) return false;
        if (!CourseCheckDue(ReadCourseState(), courseId, Now(), Math.Max(1, Config.Int("sources.lms.courseFreshMinutes", 20)) * 60L)) return false;
        var saved = LmsStore.Latest();
        if ((saved["courses"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(c => c["id"]?.GetValue<long>() == courseId) is not { } c) return false;
        if (!await Gate.WaitAsync(0, ct)) return false;
        try
        {
            var state = ReadCourseState();
            var index = JsonStore.ReadObject(FilesIndex);
            var check = await CheckCourseAsync(CourseInfo.FromJson(c), saved["term"]?.GetValue<string>() ?? "", state, index, [], force: false,
                Config.Bool("sources.lms.autoDownload", false), Config.Int("sources.lms.pastSyncDays", 7), _ => { }, ct);
            Log.Info($"LMS: mở lớp {c["subject"]}, kiểm cập nhật: {check?.Kind}");
            return check?.Kind is CourseCheckKind.New or CourseCheckKind.Changed or CourseCheckKind.ChangedAfterCheck;
        }
        catch (Exception e) when (Recoverable(e, ct) || e is SessionExpiredException) { Log.Debug($"LMS kiểm lớp khi mở: {e.Message}"); return false; }
        finally { Gate.Release(); }
    }

    private static int _lightNext;

    public static void LightNext() => Interlocked.Exchange(ref _lightNext, 1);

    public static void CancelLight() => Interlocked.Exchange(ref _lightNext, 0);

    public async Task SyncAsync(Action<string> log, bool force, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        var light = Interlocked.Exchange(ref _lightNext, 0) == 1 && !force;
        try { await SyncCoreAsync(log, force, ct, light); }
        finally { Gate.Release(); }
    }

    private static readonly AsyncLocal<Action<string>?> SyncLog = new();

    private static void Warn(string what, Exception e)
    {
        if (e is LmsException { Code: "nopermissions" or "nopermission" }) { Log.Debug($"LMS {what}: {e.Message}"); return; }
        if (e is LmsException or HttpRequestException or TimeoutException or IOException or UnauthorizedAccessException) Log.Warn($"LMS {what}: {e.Message}");
        else Log.Error($"LMS {what}", e);
        SyncLog.Value?.Invoke(SyncSignal.Warn(what, e.Message));
    }

    private static bool Recoverable(Exception e, CancellationToken ct) =>
        e is not (SessionExpiredException or OutOfMemoryException) && !(e is OperationCanceledException && ct.IsCancellationRequested);

    private static async Task<T> PartAsync<T>(string what, Func<Task<T>> work, T fallback, CancellationToken ct)
    {
        try { return await work(); }
        catch (Exception e) when (Recoverable(e, ct)) { Warn(what, e); return fallback; }
    }

    private static async Task<JsonObject?> OptionalAsync(string what, Func<Task<JsonObject>> work, CancellationToken ct)
    {
        try { return await work(); }
        catch (SessionExpiredException e) { Warn(what, e); return null; }
        catch (Exception e) when (Recoverable(e, ct)) { Warn(what, e); return null; }
    }

    private static bool Part(string what, Action work, CancellationToken ct)
    {
        try { work(); return true; }
        catch (Exception e) when (Recoverable(e, ct)) { Warn(what, e); return false; }
    }

    private static async Task SyncCoreAsync(Action<string> log, bool force, CancellationToken ct, bool light = false)
    {
        SyncLog.Value = log;
        var autoDownload = Config.Bool("sources.lms.autoDownload", false);
        var callsBefore = Calls;
        var plan = new SyncPlan()
            .Add("sync.lms.check", weight: null)
            .Add("sync.lms.groups")
            .Add("sync.lms.events", 2)
            .Add("sync.lms.quizzes", 2)
            .Add("sync.lms.news", weight: null)
            .Add("sync.lms.messages", 2)
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
        metas = [.. metas.Select(m => SharedTerm(m, metas, term))];
        var state = ReadCourseState();
        var index = JsonStore.ReadObject(FilesIndex);
        var hashes = new Dictionary<string, Dictionary<string, string>>();
        var newFiles = new List<JsonObject>();
        var pending = new List<(CourseInfo M, List<(JsonObject Sec, JsonObject Mod, JsonObject F)> Files, bool Fresh, string Key, JsonObject Done)>();
        var changedModules = new HashSet<long>();
        int nNew = 0, nChanged = 0, nChecked = 0, nSkipped = 0;
        var pastDays = Config.Int("sources.lms.pastSyncDays", 7);

        var pastTerms = Config.Bool("sources.lms.pastTerms", false);
        var toCheck = light ? [] : metas.Where(m => pastTerms || m.Term == term).OrderByDescending(m => m.Term).ToList();
        var current = metas.Where(m => m.Term == term).ToList();
        plan.SetWeight("sync.lms.check", Math.Max(1, toCheck.Count));
        plan.SetWeight("sync.lms.news", Math.Max(1, current.Count));
        plan.SetWeight("sync.lms.grades", Math.Max(1, current.Count(c => !Files.Organizer.IsShared(c.Part))));
        var nth = 0;
        foreach (var m in toCheck)
        {
            Report(plan.Advance(nth, nth + 1, toCheck.Count));
            nth++;
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

        var prev = LmsStore.Latest();
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
            ["events"] = events ?? prev["events"]?.DeepClone() ?? new JsonArray(),
            ["quizzes"] = quizzes ?? prev["quizzes"]?.DeepClone() ?? new JsonArray(),
            ["newFiles"] = new JsonArray(newFiles.Cast<JsonNode>().Concat(recent.Select(x => x!.DeepClone())).Take(300).ToArray()),
            ["announcements"] = announcements ?? prev["announcements"]?.DeepClone() ?? new JsonArray(),
            ["grades"] = grades ?? prev["grades"]?.DeepClone() ?? new JsonArray(),
            ["lastRun"] = new JsonObject { ["new"] = nNew, ["changed"] = nChanged, ["checked"] = nChecked, ["skipped"] = nSkipped, ["files"] = newFiles.Count },
            ["forumsSeen"] = prev["forumsSeen"]?.DeepClone(),
            ["lightAt"] = light ? Now() : prev["lightAt"]?.DeepClone(),
        };
        try
        {
            Report(plan.Enter("sync.lms.groups"));
            var classes = current.Where(c => !Files.Organizer.IsShared(c.Part)).ToList();
            var groups = await PartAsync("nhóm lớp LMS", () => MyGroupsAsync(classes, uid, ct), new Dictionary<long, HashSet<string>>(), ct);
            Report(plan.Enter("sync.lms.events"));
            events = await PartAsync("lịch và bài tập LMS", () => CollectEventsAsync(current, groups, uid, ct), prev["events"]?.DeepClone() as JsonArray ?? [], ct);
            Report(plan.Enter("sync.lms.quizzes"));
            quizzes = await PartAsync("quiz LMS", () => CollectQuizzesAsync(current, prev["quizzes"] as JsonArray, prev["syncedAt"]?.GetValue<long>() ?? 0, log, ct),
                prev["quizzes"]?.DeepClone() as JsonArray ?? [], ct);
            if (light) goto Saved;
            Report(plan.Enter("sync.lms.news"));
            announcements = await PartAsync("thông báo LMS", () => CollectAnnouncementsAsync(raw, current, uid, prev, changedModules, ct),
                prev["announcements"]?.DeepClone() as JsonArray ?? [], ct);
            await PartAsync("file thông báo LMS", async () => { await SaveAnnouncementFilesAsync(announcements, ct); return true; }, false, ct);
            Report(plan.Enter("sync.lms.messages"));
            if (await OptionalAsync("tin nhắn LMS", () => CollectMessagesAsync(uid, LmsMessagesStore.Raw(), ct), ct) is { } messages)
                Part("lưu tin nhắn LMS", () => LmsMessagesStore.Write(messages), ct);
            if (current.Count > 0 && await OptionalAsync("trang LMS", () => CollectPagesAsync(current, LmsPagesStore.Raw(), ct), ct) is { } pages)
                Part("lưu trang LMS", () => LmsPagesStore.Write(pages), ct);
            await PartAsync("ảnh LMS", () => LmsImages.SaveAsync(LmsImages.HtmlOf(announcements, LmsMessagesStore.Raw(), LmsPagesStore.Raw()), ct), 0, ct);
            Report(plan.Enter("sync.lms.grades"));
            grades = await PartAsync("sổ điểm LMS", () => CollectGradesAsync(classes, uid, ct), prev["grades"]?.DeepClone() as JsonArray ?? [], ct);
        Saved:;
        }
        catch (SessionExpiredException) when (events is not null)
        {
            LmsStore.Write(Output(prev["syncedAt"]?.GetValue<long>() ?? 0));
            Log.Info("LMS: hết phiên giữa chừng, đã lưu phần đọc được");
            throw;
        }
        var output = Output(light ? prev["syncedAt"]?.GetValue<long>() ?? 0 : Now());
        LmsStore.Write(output);
        log(SyncSignal.DataReady);
        if (light)
        {
            Log.Info($"Sync LMS (nhẹ, giữ dữ liệu cho nhắc hạn): {Calls - callsBefore} request");
            Report(plan.Finish());
            return;
        }

        var total = pending.Sum(p => p.Files.Count);
        if (total > 0) log($"Đang xem {total} tài liệu của {pending.Count} lớp...");
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
                var got = await PartAsync($"tài liệu {f["filename"]}", async () => (Ok: true, File: await SyncFileAsync(p.M, sec, mod, f, index, hashes, log, ct)), (Ok: false, File: (JsonObject?)null), ct);
                allOk &= got.Ok;
                if (got.File is not null && !p.Fresh) newFiles.Add(got.File);
                if (got.File is not null) JsonStore.Write(FilesIndex, index);
                doneWeight += Weight(f);
                if (++seen % 20 == 0) log($"Tài liệu: {seen}/{total}");
            }
            if (!allOk) continue;
            state[p.Key] = p.Done;
            SaveCourseState(state);
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

    public void Flush()
    {
        LmsStore.Flush();
        JsonObject? pending;
        lock (CourseGate) { pending = _courseStatePending; _courseStatePending = null; }
        if (pending is not null) JsonStore.Write(CourseState, pending);
    }

    public static CourseInfo SharedTerm(CourseInfo m, IReadOnlyList<CourseInfo> all, string term) =>
        Files.Organizer.IsShared(m.Part) && m.Term.Length == 0 && all.Any(x => x.Term == term && x.Code == m.Code) ? m with { Term = term } : m;

    private enum CourseCheckKind { New, Changed, Unchanged, ChangedAfterCheck, Skipped }

    private sealed record CourseCheck(CourseCheckKind Kind,
        (CourseInfo M, List<(JsonObject Sec, JsonObject Mod, JsonObject F)> Files, bool Fresh, string Key, JsonObject Done)? Pending = null);

    private static async Task<CourseCheck?> CheckCourseAsync(CourseInfo m, string term, JsonObject state, JsonObject index, HashSet<long> changedModules,
        bool force, bool autoDownload, int pastDays, Action<string> log, CancellationToken ct)
    {
        var key = m.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var st = state[key] as JsonObject;
        var started = Now();
        HashSet<long>? only = null;
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
        var files = Downloadable(contents).Where(x => autoDownload && (only is null || only.Contains(x.Mod["id"]!.GetValue<long>()))
                                                      && KindOf(x.File["filename"]?.GetValue<string>()) != FileKind.Video).ToList();
        if (files.Count == 0)
        {
            state[key] = done;
            SaveCourseState(state);
            return new CourseCheck(kind);
        }
        return new CourseCheck(kind, (m, files, st is null, key, done));
    }
}
