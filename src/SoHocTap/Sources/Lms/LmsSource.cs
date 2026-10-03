using System.Text.Json;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using SoHocTap.Files;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

/// <summary>
/// Source LMS, sync kiểu incremental (DESIGN.md mục 7):
///  1. Lấy danh sách lớp (1 request). Lớp mới thì đọc hết nội dung.
///  2. Lớp đã biết thì gọi core_course_get_updates_since; không đổi thì bỏ qua, có đổi thì chỉ xử lý mấy mục đã đổi.
///  3. Lớp kỳ trước chỉ check mỗi pastSyncDays ngày. State được ghi ngay sau từng lớp.
/// Output: data/lms.json, data/lms-files.json, data/lms-courses.json, data/lms-course/*.json, data/lms-quiz/*.json.
/// </summary>
public sealed partial class LmsSource : ISource
{
    public string Name => "lms";
    public TimeSpan Interval => TimeSpan.FromHours(Config.Int("sources.lms.syncHours", 3));

    private static string LmsFile => Paths.DataFile("lms.json");
    private static string FilesIndex => Paths.DataFile("lms-files.json");
    private static string CourseState => Paths.DataFile("lms-courses.json");
    private static string GroupsFile => Paths.DataFile("lms-groups.json");
    public static string CourseDir => Paths.DataFile("lms-course");
    public static string QuizDir => Paths.DataFile("lms-quiz");

    private static readonly HashSet<string> ContentUpdates = ["contentfiles", "configuration", "introfiles"];

    public SourceStatus Status()
    {
        var d = JsonStore.Read(LmsFile);
        return new SourceStatus(Token is not null, d?["syncedAt"]?.GetValue<long>(), d?["user"]?.GetValue<string>(), d?["term"]?.GetValue<string>());
    }

    public JsonNode? Data() => JsonStore.Read(LmsFile);

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
    /// vd. sổ điểm) là bình thường, chỉ ghi log.
    /// </summary>
    private static void Warn(string what, Exception e)
    {
        if (e is LmsException { Code: "nopermissions" or "nopermission" }) { Log.Debug($"LMS {what}: {e.Message}"); return; }
        Log.Warn($"LMS {what}: {e.Message}");
        SyncLog.Value?.Invoke(SyncSignal.Warn(what, e.Message));
    }

    private async Task SyncCoreAsync(Action<string> log, bool force, CancellationToken ct)
    {
        SyncLog.Value = log;
        // sources.lms.autoDownload = false thì chỉ đọc lịch, quiz, thông báo, cấu trúc khóa; tài liệu thì tải tay theo mục (DownloadSectionsAsync).
        var autoDownload = Config.Bool("sources.lms.autoDownload", false);
        var callsBefore = Calls;
        log(SyncSignal.Step("sync.lms.courses"));
        var info = await CallAsync("core_webservice_get_site_info", [], ct);
        var uid = info["userid"]!.GetValue<long>();
        var raw = (JsonArray)await CallAsync("core_enrol_get_users_courses", [Arg("userid", uid)], ct);
        var skip = Config.List("sources.lms.skipCourses");
        var metas = raw.OfType<JsonObject>()
            .Where(c => c["fullname"]?.GetValue<string>() is { } n && Organizer.SubjectOf(n) is not null && !skip.Any(n.Contains))
            .Select(CourseMeta).ToList();
        var term = CurrentTerm(metas);
        var state = JsonStore.ReadObject(CourseState);
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
        var nth = 0;
        foreach (var m in toCheck)
        {
            log(SyncSignal.Step("sync.lms.check", nth++, toCheck.Count));
            var key = m.Id.ToString();
            var st = state[key] as JsonObject;
            var started = Now();
            HashSet<long>? only = null;                             // null = mọi mục
            if (force || st is null)
            {
                if (st is null) { nNew++; log($"  lớp mới: {m.Subject} {m.Part} {m.Term}"); } else nChanged++;
            }
            else
            {
                if (m.Term != term && started - (st["checked"]?.GetValue<long>() ?? 0) < pastDays * 86400) { nSkipped++; continue; }
                nChecked++;
                var upd = await CallAsync("core_course_get_updates_since",
                    [Arg("courseid", m.Id), Arg("since", Math.Max(0, (st["synced"]?.GetValue<long>() ?? 0) - 300))], ct);
                var lastCheck = (st["checked"]?.GetValue<long>() ?? 0) - 300;
                foreach (var i in (upd["instances"] as JsonArray ?? []).OfType<JsonObject>())
                    if (i["contextlevel"]?.GetValue<string>() == "module"
                        && (i["updates"] as JsonArray ?? []).Any(u => (u?["timeupdated"]?.GetValue<long?>() ?? long.MaxValue) > lastCheck))
                        changedModules.Add(i["id"]!.GetValue<long>());
                only = upd["instances"]?.AsArray().OfType<JsonObject>()
                    .Where(i => i["contextlevel"]?.GetValue<string>() == "module"
                                && i["updates"]!.AsArray().Any(u => ContentUpdates.Contains(u?["name"]?.GetValue<string>() ?? "")))
                    .Select(i => i["id"]!.GetValue<long>()).ToHashSet() ?? [];
                if (only.Count == 0)
                {
                    st["checked"] = started;
                    JsonStore.Write(CourseState, state);
                    continue;
                }
                nChanged++;
            }
            var contents = (JsonArray)await CallAsync("core_course_get_contents", [Arg("courseid", m.Id)], ct);
            SaveStructure(m, contents, index);
            var done = new JsonObject { ["synced"] = started, ["checked"] = started, ["term"] = m.Term };
            var files = Downloadable(contents).Where(x => autoDownload && (only is null || only.Contains(x.Mod["id"]!.GetValue<long>()))).ToList();
            if (files.Count == 0)
            {
                state[key] = done;
                JsonStore.Write(CourseState, state);
            }
            else pending.Add((m, files, st is null, key, done));
        }

        // Lịch, quiz, thông báo, điểm lưu trước rồi mới tải tài liệu: lần đầu đăng nhập có thể phải tải hàng trăm file (vài phút),
        // không để các trang trống trong lúc đó. Tài liệu tải sau, giãn cách theo sources.lms.fileGapMs.
        var current = metas.Where(m => m.Term == term).ToList();
        var prev = JsonStore.ReadObject(LmsFile);
        var recent = (prev["newFiles"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(x => (x["seenAt"]?.GetValue<long>() ?? 0) > Now() - 14 * 86400).Select(x => x.DeepClone());
        foreach (var x in newFiles) x["seenAt"] = Now();
        log(SyncSignal.Step("sync.lms.events"));
        var events = await CollectEventsAsync(current, uid, ct);
        log(SyncSignal.Step("sync.lms.quizzes"));
        var quizzes = await CollectQuizzesAsync(current, prev["quizzes"] as JsonArray, prev["syncedAt"]?.GetValue<long>() ?? 0, log, ct);
        log(SyncSignal.Step("sync.lms.news"));
        var announcements = await CollectAnnouncementsAsync(raw, current, uid, prev, changedModules, ct);
        log(SyncSignal.Step("sync.lms.grades"));
        var grades = await CollectGradesAsync(current, uid, ct);
        var output = new JsonObject
        {
            ["syncedAt"] = Now(),
            ["user"] = info["fullname"]?.GetValue<string>(),
            ["term"] = term,
            ["courses"] = new JsonArray(metas.Select(m => (JsonNode)m.ToJson()).ToArray()),
            ["events"] = events,
            ["quizzes"] = quizzes,
            ["newFiles"] = new JsonArray(newFiles.Cast<JsonNode>().Concat(recent).Take(300).ToArray()),
            ["announcements"] = announcements,
            ["grades"] = grades,
            ["lastRun"] = new JsonObject { ["new"] = nNew, ["changed"] = nChanged, ["checked"] = nChecked, ["skipped"] = nSkipped, ["files"] = newFiles.Count },
        };
        output["forumsSeen"] = prev["forumsSeen"]?.DeepClone();
        JsonStore.Write(LmsFile, output);
        log(SyncSignal.DataReady);   // giao diện đọc lại ngay: đã có lịch, quiz, điểm

        var total = pending.Sum(p => p.Files.Count);
        if (total > 0) log($"Đang xem {total} tài liệu của {pending.Count} lớp…");
        var seen = 0;
        foreach (var p in pending)
        {
            foreach (var (sec, mod, f) in p.Files)
            {
                log(SyncSignal.Step("sync.lms.files", seen, total));
                var got = await SyncFileAsync(p.M, sec, mod, f, index, hashes, log, ct);
                if (got is not null && !p.Fresh) newFiles.Add(got);    // lần đầu đọc lớp thì không tính là "mới"
                if (got is not null) JsonStore.Write(FilesIndex, index);
                if (++seen % 20 == 0) log($"Tài liệu: {seen}/{total}");
            }
            state[p.Key] = p.Done;
            JsonStore.Write(CourseState, state);                    // ghi ngay, lỡ tắt app giữa chừng thì lần sau làm tiếp
        }
        JsonStore.Write(FilesIndex, index);
        Organizer.SaveHashCache();
        if (autoDownload)
        {
            log(SyncSignal.Step("sync.lms.assign"));
            await SaveAssignmentsAsync(current, events, index, hashes, log, ct);
        }
        foreach (var x in newFiles) x["seenAt"] = Now();
        output["newFiles"] = new JsonArray(newFiles.Cast<JsonNode>().Concat(recent).Take(300).ToArray());
        output["lastRun"]!["files"] = newFiles.Count;
        JsonStore.Write(LmsFile, output);
        log($"Xong: {nNew} lớp mới, {nChanged} lớp có thay đổi, {nChecked - Math.Min(nChecked, nChanged)} lớp không đổi, {nSkipped} lớp kỳ trước chưa tới hạn, {newFiles.Count} tài liệu mới, {Calls - callsBefore} request API");
        Log.Info($"Sync LMS: {Calls - callsBefore} request, check {nChecked} lớp, {nChanged} lớp có đổi");
    }

    /// <summary>
    /// Bài tập (assignment): lưu đề (Đề bài.html) và file đính kèm vào Tài liệu LMS\Bài tập\&lt;tên bài&gt;, để xem được khi LMS lag.
    /// Dữ liệu lấy từ mod_assign_get_assignments đã gọi sẵn; file nào đã có (cùng url, cùng timemodified) thì không tải lại.
    /// Chỉ đọc: không mở trang nộp bài, không gọi API ghi.
    /// </summary>
    private async Task SaveAssignmentsAsync(List<CourseInfo> courses, JsonArray events, JsonObject index,
        Dictionary<string, Dictionary<string, string>> hashes, Action<string> log, CancellationToken ct)
    {
        var byId = courses.ToDictionary(c => c.Id);
        var max = Config.Int("sources.lms.maxFileMB", 200) * 1024L * 1024;
        var sec = new JsonObject { ["name"] = "Bài tập" };
        foreach (var e in events.OfType<JsonObject>().Where(e => e["kind"]?.GetValue<string>() == "assign"))
        {
            if (e["course"]?.GetValue<long>() is not { } cid || !byId.TryGetValue(cid, out var m)) continue;
            var name = e["name"]?.GetValue<string>() ?? "Bài tập";
            if (e["intro"]?.GetValue<string>() is { Length: > 0 } intro)
            {
                var dir = Path.Combine(m.LmsFolder, "Bài tập", SafeName(name));
                var file = Path.Combine(dir, "Đề bài.html");
                var due = DateTimeOffset.FromUnixTimeSeconds(e["time"]?.GetValue<long>() ?? 0).LocalDateTime;
                // CSP: mở file trên máy thì không chạy script / event handler, không gửi form, không tải ảnh ngoài (theo dõi người mở).
                // Bỏ thẻ <meta> trong nội dung LMS để không tự chuyển trang (refresh) hay đổi CSP.
                var html = $"<!doctype html><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; form-action 'none'; base-uri 'none'\">" +
                           $"<title>{WebUtility.HtmlEncode(name)}</title>" +
                           $"<h1>{WebUtility.HtmlEncode(name)}</h1><p>Hạn nộp: {due:dd/MM/yyyy HH:mm}</p>" +
                           MetaRx().Replace(ScriptRx().Replace(intro, ""), "");
                if (!File.Exists(file) || File.ReadAllText(file) != html)
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(file, html);
                }
            }
            var mod = new JsonObject { ["modname"] = "folder", ["name"] = name };
            foreach (var f in (e["files"] as JsonArray ?? []).OfType<JsonObject>())
                if (f["fileurl"] is not null && (f["filesize"]?.GetValue<long>() ?? 0) <= max)
                    await SyncFileAsync(m, sec, mod, f, index, hashes, log, ct);
        }
        JsonStore.Write(FilesIndex, index);
        Organizer.SaveHashCache();
    }

    [GeneratedRegex(@"<script\b[\s\S]*?</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptRx();

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaRx();

    /// <summary>Các file tải được của một khóa: file trong module "resource" và "folder", bỏ folder nằm trong skipFolders và file lớn hơn maxFileMB.</summary>
    private static IEnumerable<(JsonObject Sec, JsonObject Mod, JsonObject File)> Downloadable(JsonArray contents)
    {
        var skip = Config.List("sources.lms.skipFolders");
        var max = Config.Int("sources.lms.maxFileMB", 200) * 1024L * 1024;
        foreach (var sec in contents.OfType<JsonObject>())
            foreach (var mod in (sec["modules"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if (mod["modname"]?.GetValue<string>() is not ("resource" or "folder")) continue;
                if (skip.Any(s => (mod["name"]?.GetValue<string>() ?? "").Contains(s, StringComparison.OrdinalIgnoreCase))) continue;
                foreach (var f in (mod["contents"] as JsonArray ?? []).OfType<JsonObject>())
                    if (f["type"]?.GetValue<string>() == "file" && (f["filesize"]?.GetValue<long>() ?? 0) <= max) yield return (sec, mod, f);
            }
    }

    /// <summary>Loại file để người dùng chọn tải: PDF, slide (thường nặng nhất), còn lại.</summary>
    public enum FileKind { Pdf, Slide, Other }

    public static FileKind KindOf(string? fileName) => Path.GetExtension(fileName ?? "").ToLowerInvariant() switch
    {
        ".pdf" => FileKind.Pdf,
        ".ppt" or ".pptx" or ".pps" or ".ppsx" or ".odp" or ".key" => FileKind.Slide,
        _ => FileKind.Other,
    };

    /// <summary>Một file tải được trong mục: loại, dung lượng, đã có trên máy chưa.</summary>
    public sealed record FileStat(FileKind Kind, long Bytes, bool Have);

    /// <summary>Một mục (section) của khóa trên LMS và các file tải được trong đó.</summary>
    public sealed record SectionInfo(int Index, string Name, IReadOnlyList<FileStat> Items)
    {
        public IEnumerable<FileStat> Of(IReadOnlySet<FileKind> kinds) => Items.Where(f => kinds.Contains(f.Kind));
    }

    private static CourseInfo? Course(long courseId) =>
        (JsonStore.ReadObject(LmsFile)["courses"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(c => c["id"]?.GetValue<long>() == courseId).Select(CourseInfo.FromJson).FirstOrDefault();

    /// <summary>Các mục của khóa (đọc thẳng từ LMS) để người dùng chọn tải.</summary>
    public static async Task<List<SectionInfo>> SectionsAsync(long courseId, CancellationToken ct)
    {
        var contents = (JsonArray)await CallAsync("core_course_get_contents", [Arg("courseid", courseId)], ct);
        var index = JsonStore.ReadObject(FilesIndex);
        bool Have(JsonObject f) => index[f["fileurl"]?.GetValue<string>() ?? ""] is JsonObject e
                                   && e["path"]?.GetValue<string>() is { } p && (File.Exists(p) || Directory.Exists(e["extractedTo"]?.GetValue<string>() ?? ""));
        var files = Downloadable(contents).ToList();
        var list = new List<SectionInfo>();
        var i = 0;
        foreach (var sec in contents.OfType<JsonObject>())
        {
            var mine = files.Where(x => ReferenceEquals(x.Sec, sec)).Select(x => x.File).ToList();
            if (mine.Count > 0)
                list.Add(new SectionInfo(i, WebUtility.HtmlDecode(sec["name"]?.GetValue<string>() ?? "Mục " + i),
                    [.. mine.Select(f => new FileStat(KindOf(f["filename"]?.GetValue<string>()), f["filesize"]?.GetValue<long>() ?? 0, Have(f)))]));
            i++;
        }
        return list;
    }

    /// <summary>
    /// Tải các mục đã chọn của một khóa vào thư mục môn (giống lúc sync: chống trùng, giữ bản cũ). extract = true thì tự giải nén file zip.
    /// <paramref name="kinds"/>: chỉ tải các loại file này (null = mọi loại).
    /// </summary>
    public async Task<int> DownloadSectionsAsync(long courseId, IReadOnlyCollection<int> sections, bool extract, Action<string> log, CancellationToken ct,
        IReadOnlySet<FileKind>? kinds = null)
    {
        var m = Course(courseId) ?? throw new InvalidOperationException("Chưa có khóa này, hãy đồng bộ LMS trước.");
        await Gate.WaitAsync(ct);
        try
        {
            var contents = (JsonArray)await CallAsync("core_course_get_contents", [Arg("courseid", courseId)], ct);
            var chosen = contents.OfType<JsonObject>().Where((_, i) => sections.Contains(i)).ToHashSet();
            var index = JsonStore.ReadObject(FilesIndex);
            var hashes = new Dictionary<string, Dictionary<string, string>>();
            var n = 0;
            foreach (var (sec, mod, f) in Downloadable(contents).Where(x => chosen.Contains(x.Sec)
                         && (kinds is null || kinds.Contains(KindOf(x.File["filename"]?.GetValue<string>())))))
            {
                ct.ThrowIfCancellationRequested();
                if (await SyncFileAsync(m, sec, mod, f, index, hashes, log, ct, extract) is not null) n++;
                JsonStore.Write(FilesIndex, index);
            }
            Organizer.SaveHashCache();
            return n;
        }
        finally { Gate.Release(); }
    }

    private async Task<JsonObject?> SyncFileAsync(CourseInfo m, JsonObject sec, JsonObject mod, JsonObject f, JsonObject index,
        Dictionary<string, Dictionary<string, string>> hashesBySubject, Action<string> log, CancellationToken ct, bool? extract = null)
    {
        var url = f["fileurl"]!.GetValue<string>();
        var known = index[url] as JsonObject;
        var modified = f["timemodified"]?.GetValue<long>();
        if (known is not null && known["timemodified"]?.GetValue<long>() == modified && File.Exists(known["path"]?.GetValue<string>())) return null;

        var sub = new List<string> { SafeName(WebUtility.HtmlDecode(sec["name"]?.GetValue<string>() ?? "Chung")) };
        if (mod["modname"]!.GetValue<string>() == "folder")
        {
            sub.Add(SafeName(mod["name"]!.GetValue<string>()));
            sub.AddRange((f["filepath"]?.GetValue<string>() ?? "/").Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Select(SafeName));
        }
        var dest = Path.Combine([m.LmsFolder, .. sub, SafeName(f["filename"]!.GetValue<string>())]);
        var tmp = Path.Combine(Paths.Data, "tmp", Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(url))));
        try { await DownloadAsync(url, tmp, ct); }
        catch (HttpRequestException e) { Warn($"tải {f["filename"]}", e); return null; }

        var subjDir = Path.Combine(Organizer.SubjectsRoot, m.Subject);
        if (!hashesBySubject.TryGetValue(subjDir, out var hashes)) hashesBySubject[subjDir] = hashes = Organizer.HashesIn(subjDir);
        var h = Organizer.Sha256(tmp);
        if (hashes.TryGetValue(h, out var existing) && File.Exists(existing))
        {
            File.Delete(tmp);                                       // nội dung này môn đã có rồi
            index[url] = IndexEntry(existing, modified, m.Id, h);
            return null;
        }
        var oldPath = known?["path"]?.GetValue<string>();
        if (oldPath is not null && File.Exists(oldPath))
        {
            Organizer.MoveFile(oldPath, Organizer.UniquePath(Path.Combine(Config.Folder("archiveOldVersions"), Paths.RelativeToStudy(oldPath))));
            dest = oldPath;                                         // LMS có bản mới thì đặt đúng chỗ bản cũ
        }
        else dest = Organizer.UniquePath(dest);
        Organizer.MoveFile(tmp, dest);
        MarkFromInternet(dest, url);
        hashes[h] = dest;
        var entry = IndexEntry(dest, modified, m.Id, h);
        index[url] = entry;
        log($"  {(known is not null ? "cập nhật" : "mới")}: {Paths.RelativeToStudy(dest)}");
        if ((extract ?? Config.Bool("archives.extract", true)) && Organizer.IsArchive(dest) && Organizer.ExtractArchive(dest, hashes, log) is { } x)
        {
            entry["path"] = x.Archived;
            entry["extractedTo"] = x.Folder;
        }
        return new JsonObject
        {
            ["course"] = m.Id,
            ["subject"] = m.Subject,
            ["name"] = f["filename"]?.GetValue<string>(),
            ["path"] = Paths.RelativeToStudy(dest),
            ["section"] = sec["name"]?.GetValue<string>(),
            ["module"] = mod["name"]?.GetValue<string>(),
            ["at"] = modified,
            ["updated"] = known is not null,
        };
    }

    private static JsonObject IndexEntry(string path, long? modified, long course, string sha) =>
        new() { ["path"] = path, ["timemodified"] = modified, ["course"] = course, ["sha256"] = sha };

    // ------------------------------------------------------------------ mốc thời gian, quiz

    private async Task<JsonArray> CollectEventsAsync(List<CourseInfo> courses, long uid, CancellationToken ct)
    {
        var subj = courses.ToDictionary(c => c.Id, c => c.Subject + (c.Part is null ? "" : $" ({c.Part})"));
        var groups = await MyGroupsAsync(courses, uid, ct);   // CollectGradesAsync cũng dùng cái này để lọc mục điểm của nhóm khác
        var outMap = new Dictionary<string, JsonObject>();
        // Bài tập có mốc trên lịch hành động: instance của mod_assign → mục "ev" tương ứng. Lịch đọc đủ (không lỗi, không bị cắt
        // ở 4 trang) thì biết chắc bài nào không còn mốc, tức là đã nộp (xem dưới).
        var assignEvents = new Dictionary<long, JsonObject>();
        var calendarComplete = false;
        try
        {
            // Moodle chỉ cho tối đa 50 mục mỗi lần (limitnum 1..50): đọc theo trang bằng aftereventid, tối đa 4 trang.
            var events = new List<JsonObject>();
            long after = 0;
            for (var page = 0; page < 4; page++)
            {
                var res = await CallAsync("core_calendar_get_action_events_by_timesort",
                    [Arg("timesortfrom", Now() - 7 * 86400), Arg("timesortto", Now() + 120 * 86400), Arg("limitnum", 50), Arg("aftereventid", after),
                     Arg("limittononsuspendedevents", 1)], ct);
                var batch = res["events"]!.AsArray().OfType<JsonObject>().ToList();
                events.AddRange(batch);
                if (batch.Count < 50) break;
                after = batch[^1]["id"]?.GetValue<long>() ?? 0;
                if (after == 0) break;
            }
            calendarComplete = events.Count < 200;
            foreach (var e in events)
            {
                var cid = e["course"]?["id"]?.GetValue<long>() ?? -1;
                var name = e["name"]?.GetValue<string>() ?? "";
                if (!subj.ContainsKey(cid) || !ForMyGroup(name, cid, groups)) continue;
                var mod = e["modulename"]?.GetValue<string>();
                outMap["ev" + e["id"]] = new JsonObject
                {
                    ["id"] = "ev" + e["id"],
                    ["course"] = cid,
                    ["subject"] = subj[cid],
                    ["name"] = WebUtility.HtmlDecode(e["activityname"]?.GetValue<string>() ?? name),
                    ["kind"] = mod is "assign" or "quiz" ? mod : "event",
                    ["time"] = e["timesort"]?.GetValue<long>() ?? e["timestart"]?.GetValue<long>(),
                    ["label"] = WebUtility.HtmlDecode(name),
                    // Loại mốc của Moodle: quiz có "open"/"close", bài nộp có "due". Mốc mở không phải hạn nộp.
                    ["phase"] = e["eventtype"]?.GetValue<string>(),
                    ["url"] = e["url"]?.GetValue<string>(),
                };
                if (mod == "assign")
                {
                    if (e["instance"]?.GetValue<long>() is { } inst) assignEvents[inst] = outMap["ev" + e["id"]];
                    else calendarComplete = false;   // thiếu instance thì không ghép được, cũng không suy ra "đã nộp"
                }
            }
        }
        catch (LmsException ex) { Warn("lịch LMS", ex); }
        try
        {
            var res = await CallAsync("mod_assign_get_assignments", Arr("courseids", courses.Select(c => (object)c.Id)), ct);
            foreach (var c in res["courses"]!.AsArray().OfType<JsonObject>())
                foreach (var a in c["assignments"]!.AsArray().OfType<JsonObject>())
                {
                    var cid = c["id"]!.GetValue<long>();
                    var due = a["duedate"]?.GetValue<long>() ?? 0;
                    if (due == 0 || !ForMyGroup(a["name"]!.GetValue<string>(), cid, groups)) continue;
                    var aid = a["id"]!.GetValue<long>();
                    // Cùng một bài đã có mốc "ev" trên lịch: không thêm mục thứ hai (trước đây mỗi bài hiện và được nhắc 2 lần).
                    // Chỉ chép đề và file đính kèm sang mục "ev" để vẫn lưu về máy.
                    if (assignEvents.TryGetValue(aid, out var ev))
                    {
                        ev["intro"] = a["intro"]?.GetValue<string>();
                        ev["files"] = AttachmentFiles(a);
                        continue;
                    }
                    // Hạn nằm trong khung lịch đã đọc mà Moodle không có mốc: Moodle gỡ mốc khi bạn đã nộp (hoặc không phải nộp),
                    // xem mod_assign_core_calendar_provide_event_action. Giữ để lưu đề, nhưng đánh dấu done: không đếm, không nhắc.
                    var done = calendarComplete && due >= Now() - 7 * 86400 && due <= Now() + 120 * 86400;
                    outMap.TryAdd("as" + aid, new JsonObject
                    {
                        ["id"] = "as" + a["id"],
                        ["course"] = cid,
                        ["subject"] = subj.GetValueOrDefault(cid, ""),
                        ["name"] = WebUtility.HtmlDecode(a["name"]!.GetValue<string>()),
                        ["kind"] = "assign",
                        ["time"] = due,
                        ["label"] = "Hạn nộp",
                        ["url"] = $"{Site}/mod/assign/view.php?id={a["cmid"]}",
                        // Đề và file đính kèm có sẵn trong kết quả này (không tốn thêm request): để lưu về máy.
                        ["intro"] = a["intro"]?.GetValue<string>(),
                        ["files"] = AttachmentFiles(a),
                        ["done"] = done ? true : null,
                    });
                }
        }
        catch (LmsException ex) { Warn("bài tập LMS", ex); }
        foreach (var e in outMap.Values.Where(e => e["done"] is null).ToList()) e.Remove("done");
        return new JsonArray(outMap.Values.OrderBy(e => e["time"]?.GetValue<long>() ?? 0).Cast<JsonNode>().ToArray());
    }

    private static JsonArray AttachmentFiles(JsonObject a) => new((a["introattachments"] as JsonArray ?? []).OfType<JsonObject>()
        .Select(f => (JsonNode)new JsonObject
        {
            ["filename"] = f["filename"]?.DeepClone(),
            ["fileurl"] = f["fileurl"]?.DeepClone(),
            ["filesize"] = f["filesize"]?.DeepClone(),
            ["timemodified"] = f["timemodified"]?.DeepClone(),
            ["filepath"] = "/",
            ["type"] = "file",
        }).ToArray());

    /// <summary>
    /// Thông báo: bài đăng diễn đàn (Các thông báo, diễn đàn thường) của các môn kỳ này, kể cả khóa phụ cùng môn bị ẩn khỏi
    /// danh sách lớp (vd. "…_Video" có diễn đàn hỏi đáp đề thi), cộng thêm notification hệ thống của Moodle. Chỉ giữ announcementDays ngày.
    /// Mỗi lần chạy: 1 request lấy danh sách diễn đàn + 1 request notification + 1 request cho mỗi diễn đàn có thể có bài mới.
    /// Diễn đàn chỉ đọc lại khi numdiscussions đổi, khi core_course_get_updates_since báo module có update,
    /// hoặc đã quá 24 giờ (để bắt reply mới); còn lại thì dùng cache bài cũ. Mốc lưu ở lms.json → forumsSeen {id: {n, at}}.
    /// </summary>
    private static async Task<JsonArray> CollectAnnouncementsAsync(JsonArray enrolled, List<CourseInfo> current, long uid,
        JsonObject prev, HashSet<long> changedModules, CancellationToken ct)
    {
        var seen = prev["forumsSeen"] as JsonObject ?? new JsonObject();
        var prevItems = (prev["announcements"] as JsonArray ?? []).OfType<JsonObject>().Where(x => x["forumId"] is not null)
            .GroupBy(x => x["forumId"]!.GetValue<long>()).ToDictionary(g => g.Key, g => g.ToList());
        var nowSeen = new JsonObject();
        int asked = 0, reused = 0;
        var subjects = current.Select(c => c.Subject).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Chỉ lớp của học kỳ hiện tại (kể cả lớp bị bỏ khi đồng bộ tài liệu như lớp video của môn): lớp kỳ trước cùng tên môn
        // (học lại, học cải thiện) không đọc diễn đàn nữa.
        var term = current.FirstOrDefault()?.Term;
        var courseSubject = enrolled.OfType<JsonObject>()
            .Where(c => Organizer.SubjectOf(c["fullname"]?.GetValue<string>() ?? "") is not null)
            .Select(CourseMeta)
            .Where(m => m.Term == term && subjects.Contains(m.Subject))
            .ToDictionary(m => m.Id, m => m.Subject);
        var since = Now() - Config.Int("sources.lms.announcementDays", 60) * 86400L;
        var perForum = Config.Int("sources.lms.announcementsPerForum", 10);
        var items = new List<JsonObject>();
        try
        {
            var forums = (JsonArray)await CallAsync("mod_forum_get_forums_by_courses", [], ct);
            foreach (var f in forums.OfType<JsonObject>())
            {
                var cid = f["course"]?.GetValue<long>() ?? -1;
                if (!courseSubject.TryGetValue(cid, out var subject)) continue;
                var fid = f["id"]!.GetValue<long>();
                var count = f["numdiscussions"]?.GetValue<long?>();
                if (seen[fid.ToString()] is JsonObject last && count is not null && last["n"]?.GetValue<long?>() == count
                    && Now() - (last["at"]?.GetValue<long>() ?? 0) < 86400 && !changedModules.Contains(f["cmid"]?.GetValue<long>() ?? -1))
                {
                    reused++;
                    nowSeen[fid.ToString()] = last.DeepClone();
                    items.AddRange(prevItems.GetValueOrDefault(fid, []).Where(x => (x["time"]?.GetValue<long>() ?? 0) >= since).Select(x => (JsonObject)x.DeepClone()));
                    continue;
                }
                asked++;
                var d = await CallAsync("mod_forum_get_forum_discussions", [Arg("forumid", fid), Arg("perpage", perForum)], ct);
                nowSeen[fid.ToString()] = new JsonObject { ["n"] = count, ["at"] = Now() };
                foreach (var x in (d["discussions"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    var time = x["timemodified"]?.GetValue<long>() ?? 0;
                    if (time < since) continue;
                    items.Add(new JsonObject
                    {
                        ["id"] = "fd" + x["discussion"],
                        ["forumId"] = fid,
                        ["kind"] = f["type"]?.GetValue<string>() == "news" ? "news" : "forum",
                        ["course"] = cid,
                        ["subject"] = subject,
                        ["forum"] = WebUtility.HtmlDecode(f["name"]?.GetValue<string>() ?? ""),
                        ["title"] = WebUtility.HtmlDecode(x["name"]?.GetValue<string>() ?? ""),
                        ["author"] = x["userfullname"]?.GetValue<string>(),
                        ["time"] = time,
                        ["created"] = x["created"]?.GetValue<long>(),
                        ["replies"] = x["numreplies"]?.GetValue<int>() ?? 0,
                        ["pinned"] = x["pinned"]?.GetValue<bool>() ?? false,
                        ["message"] = x["message"]?.GetValue<string>() ?? "",
                        ["attachments"] = new JsonArray((x["attachments"] as JsonArray ?? []).OfType<JsonObject>()
                            .Select(a => (JsonNode)new JsonObject { ["name"] = a["filename"]?.GetValue<string>(), ["url"] = a["fileurl"]?.GetValue<string>() }).ToArray()),
                        ["url"] = $"{Site}/mod/forum/discuss.php?d={x["discussion"]}",
                    });
                }
            }
        }
        catch (LmsException e) { Warn("thông báo diễn đàn", e); nowSeen.Clear(); }   // lần sau đọc lại hết diễn đàn
        Log.Info($"Thông báo LMS: {courseSubject.Count} khóa của môn đang học, {items.Count} bài trong {Config.Int("sources.lms.announcementDays", 60)} ngày; gọi {asked} diễn đàn, dùng cache {reused}");
        prev["forumsSeen"] = nowSeen;   // ghi chung với lms.json (xem SyncCoreAsync)
        try
        {
            var n = await CallAsync("message_popup_get_popup_notifications", [Arg("useridto", uid), Arg("limit", 20), Arg("newestfirst", 1)], ct);
            foreach (var x in (n["notifications"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var time = x["timecreated"]?.GetValue<long>() ?? 0;
                if (time < since) continue;
                items.Add(new JsonObject
                {
                    ["id"] = "nt" + x["id"],
                    ["kind"] = "system",
                    ["subject"] = "",
                    ["forum"] = "Thông báo LMS",
                    ["title"] = x["subject"]?.GetValue<string>() ?? "",
                    ["author"] = "",
                    ["time"] = time,
                    ["replies"] = 0,
                    ["pinned"] = false,
                    ["message"] = x["fullmessagehtml"]?.GetValue<string>() is { Length: > 0 } h ? h : x["smallmessage"]?.GetValue<string>() ?? "",
                    ["attachments"] = new JsonArray(),
                    ["url"] = x["contexturl"]?.GetValue<string>(),
                });
            }
        }
        catch (LmsException e) { Warn("thông báo LMS", e); }
        return new JsonArray(items.OrderByDescending(i => i["time"]!.GetValue<long>()).Cast<JsonNode>().ToArray());
    }

    /// <summary>
    /// Sổ điểm LMS của từng lớp kỳ này (điểm thành phần: quiz, BTL, giữa kỳ… do giảng viên nhập). Dùng API
    /// gradereport_user_get_grade_items của app mobile, mỗi lớp một request. Lớp nào chưa có mục điểm thì bỏ qua.
    /// </summary>
    private static async Task<JsonArray> CollectGradesAsync(List<CourseInfo> current, long uid, CancellationToken ct)
    {
        // Nhóm của mình (đã ghi lúc lấy mốc, ngay trước bước này). Lớp thí nghiệm có mục điểm của mọi nhóm nên chỉ giữ nhóm mình.
        var groupsJson = JsonStore.ReadObject(GroupsFile);
        var groups = groupsJson.ToDictionary(p => long.Parse(p.Key), p => (p.Value as JsonArray ?? []).Select(x => x!.GetValue<string>()).ToHashSet());
        var list = new JsonArray();
        foreach (var c in current)
        {
            try
            {
                var r = await CallAsync("gradereport_user_get_grade_items", [Arg("courseid", c.Id), Arg("userid", uid)], ct);
                var items = (r["usergrades"]?[0]?["gradeitems"] as JsonArray ?? []).OfType<JsonObject>()
                    .Where(i => ForMyGroup(i["itemname"]?.GetValue<string>() ?? "", c.Id, groups))
                    .Select(i => (JsonNode)new JsonObject
                    {
                        ["name"] = i["itemname"]?.GetValue<string>() is { Length: > 0 } n ? WebUtility.HtmlDecode(n) : "Tổng khóa học",
                        ["kind"] = i["itemtype"]?.GetValue<string>() == "mod" ? i["itemmodule"]?.GetValue<string>() : i["itemtype"]?.GetValue<string>(),
                        ["grade"] = i["graderaw"]?.GetValue<double?>(),
                        ["text"] = i["gradeformatted"]?.GetValue<string>(),
                        ["max"] = i["grademax"]?.GetValue<double?>(),
                        ["percent"] = i["percentageformatted"]?.GetValue<string>(),
                        ["feedback"] = i["feedback"]?.GetValue<string>(),
                    }).ToArray();
                // Chỉ có row tổng (giảng viên chưa tạo mục điểm nào) thì bỏ.
                if (!items.Any(i => i!["kind"]?.GetValue<string>() is not ("course" or "category"))) continue;
                list.Add(new JsonObject { ["course"] = c.Id, ["subject"] = c.Subject, ["part"] = c.Part, ["items"] = new JsonArray(items) });
            }
            catch (LmsException e) { Warn($"sổ điểm {c.Subject}", e); }
        }
        return list;
    }

    /// <summary>
    /// Tên nhóm của mình trong từng lớp (lớp thí nghiệm dùng chung cho nhiều nhóm). Nhóm gần như không đổi trong kỳ nên
    /// cache vào lms-groups.json, chỉ gọi API cho lớp chưa biết, 7 ngày thì hỏi lại hết.
    /// </summary>
    private static async Task<Dictionary<long, HashSet<string>>> MyGroupsAsync(List<CourseInfo> courses, long uid, CancellationToken ct)
    {
        var fresh = File.Exists(GroupsFile) && DateTime.UtcNow - File.GetLastWriteTimeUtc(GroupsFile) < TimeSpan.FromDays(7);
        var cached = fresh ? JsonStore.ReadObject(GroupsFile) : new JsonObject();
        var map = new Dictionary<long, HashSet<string>>();
        var asked = false;
        foreach (var c in courses)
        {
            if (cached[c.Id.ToString()] is JsonArray known) { map[c.Id] = known.Select(x => x!.GetValue<string>()).ToHashSet(); continue; }
            asked = true;
            try
            {
                var r = await CallAsync("core_group_get_course_user_groups", [Arg("courseid", c.Id), Arg("userid", uid)], ct);
                map[c.Id] = r["groups"]!.AsArray().Select(g => g?["name"]?.GetValue<string>() ?? "").ToHashSet();
            }
            catch (LmsException e) { map[c.Id] = []; Warn($"nhóm lớp {c.Subject}", e); }
        }
        if (asked) JsonStore.Write(GroupsFile, map);
        return map;
    }

    /// <summary>Mốc có mã nhóm trong tên (theo groupPattern) mà không phải nhóm của mình thì bỏ.</summary>
    private static bool ForMyGroup(string name, long cid, Dictionary<long, HashSet<string>> groups)
    {
        var pattern = Config.Str("sources.lms.groupPattern");
        if (pattern.Length == 0) return true;
        var codes = Regex.Matches(name, pattern).Select(x => x.Value).ToHashSet();
        return codes.Count == 0 || codes.Overlaps(groups.GetValueOrDefault(cid, []));
    }

    /// <summary>Chỉ gọi lại attempt của quiz đang mở hoặc vừa đóng; quiz khác thì dùng lại data cũ.</summary>
    private async Task<JsonArray> CollectQuizzesAsync(List<CourseInfo> courses, JsonArray? previous, long lastSync, Action<string> log, CancellationToken ct)
    {
        var subj = courses.ToDictionary(c => c.Id, c => c.Subject);
        var old = (previous ?? []).OfType<JsonObject>().ToDictionary(q => q["id"]!.GetValue<long>());
        JsonNode res;
        try { res = await CallAsync("mod_quiz_get_quizzes_by_courses", Arr("courseids", courses.Select(c => (object)c.Id)), ct); }
        catch (LmsException e) { Warn("danh sách quiz", e); return previous?.DeepClone().AsArray() ?? []; }
        Directory.CreateDirectory(QuizDir);
        var t = Now();
        var list = new JsonArray();
        foreach (var q in res["quizzes"]!.AsArray().OfType<JsonObject>())
        {
            var id = q["id"]!.GetValue<long>();
            long? open = q["timeopen"]?.GetValue<long>() is > 0 and var o ? o : null;
            long? close = q["timeclose"]?.GetValue<long>() is > 0 and var c ? c : null;
            var item = new JsonObject
            {
                ["id"] = id,
                ["course"] = q["course"]?.GetValue<long>(),
                ["subject"] = subj.GetValueOrDefault(q["course"]!.GetValue<long>(), ""),
                ["name"] = WebUtility.HtmlDecode(q["name"]!.GetValue<string>()),
                ["open"] = open,
                ["close"] = close,
                ["timelimit"] = q["timelimit"]?.GetValue<long>(),
                ["attempts"] = new JsonArray(),
                ["url"] = $"{Site}/mod/quiz/view.php?id={q["coursemodule"]}",
            };
            bool opened = open is null || open <= t;
            bool active = opened && (close is null || close >= lastSync - 86400);
            // Quiz cũ đã đóng: dùng lại data, trừ khi còn lượt đã nộp chưa lưu (vừa bật tự lưu, hoặc file bị xóa).
            if (old.TryGetValue(id, out var prev) && !active && !Unsaved(id, prev)) { item["attempts"] = prev["attempts"]?.DeepClone(); list.Add(item); continue; }
            if (!opened) { list.Add(item); continue; }
            // Quiz không có hạn đóng thì không biết bao giờ hết làm được: hỏi lượt làm tối đa ngày một lần, không hỏi ở mọi lần đồng bộ.
            if (close is null && old.TryGetValue(id, out var last) && (last["checked"]?.GetValue<long>() ?? 0) > t - 86400 && !Unsaved(id, last))
            {
                item["attempts"] = last["attempts"]?.DeepClone();
                item["checked"] = last["checked"]?.DeepClone();
                list.Add(item);
                continue;
            }
            item["checked"] = t;
            JsonArray attempts;
            try { attempts = (await CallAsync("mod_quiz_get_user_attempts", [Arg("quizid", id), Arg("status", "finished")], ct))["attempts"]!.AsArray(); }
            catch (LmsException e) { attempts = []; Warn($"lượt làm quiz {item["name"]}", e); }
            foreach (var a in attempts.OfType<JsonObject>())
            {
                var aid = a["id"]!.GetValue<long>();
                item["attempts"]!.AsArray().Add(new JsonObject { ["id"] = aid, ["finished"] = a["timefinish"]?.GetValue<long>(), ["grade"] = a["sumgrades"]?.DeepClone() });
                if (!SaveQuizzes) continue;
                // Only finished attempts, read-only review API. Never touch an attempt that is in progress.
                var path = Path.Combine(QuizDir, $"{id}-{aid}.json");
                if (JsonStore.Read(path) is JsonObject saved && !NeedsRefetch(saved, close, t)) continue;
                var head = new JsonObject
                {
                    ["quiz"] = item["name"]?.DeepClone(),
                    ["subject"] = item["subject"]?.DeepClone(),
                    ["course"] = item["course"]?.DeepClone(),
                    ["attempt"] = aid,
                    ["finished"] = a["timefinish"]?.DeepClone(),
                    ["closesAt"] = close,
                    ["savedAt"] = t,
                };
                try
                {
                    var rev = await CallAsync("mod_quiz_get_attempt_review", [Arg("attemptid", aid), Arg("page", -1)], ct);
                    var questions = new JsonArray();
                    foreach (var x in rev["questions"]!.AsArray().OfType<JsonObject>())
                        questions.Add(new JsonObject
                        {
                            ["slot"] = x["slot"]?.DeepClone(),
                            ["type"] = x["type"]?.DeepClone(),
                            ["html"] = await CleanReviewAsync(x["html"]?.GetValue<string>() ?? "", ct),
                            ["mark"] = x["mark"]?.DeepClone(),
                            ["maxmark"] = x["maxmark"]?.DeepClone(),
                            ["status"] = x["status"]?.DeepClone(),
                        });
                    head["grade"] = rev["grade"]?.DeepClone();
                    // LMS may hide the right answers until the quiz closes: remember it so we fetch again after close.
                    head["answers"] = questions.Any(q => q?["html"]?.GetValue<string>().Contains("rightanswer", StringComparison.Ordinal) == true);
                    head["questions"] = questions;
                    JsonStore.Write(path, head);
                    log($"  lưu quiz: {item["name"]}{(head["answers"]!.GetValue<bool>() ? "" : " (chưa có đáp án, sẽ lấy lại sau khi quiz đóng)")}");
                }
                catch (LmsException e) when (e.Code is not ("noreview" or "noreviewattempt" or "noreviewavailable"))
                {
                    // Lỗi thoáng qua (mạng, hết phiên…): không ghi file, lần đồng bộ sau thử lại (một request).
                    Warn($"lưu quiz {item["name"]}", e);
                }
                catch (LmsException e)
                {
                    // Quiz does not allow review: keep a stub (name, grade) so the app can offer "ghi nhanh câu còn nhớ".
                    head["grade"] = a["sumgrades"]?.DeepClone();
                    head["noReview"] = true;
                    head["reason"] = e.Message;
                    JsonStore.Write(path, head);
                    log($"  quiz không cho xem lại: {item["name"]}");
                }
            }
            list.Add(item);
        }
        return list;
    }

    /// <summary>Tự lưu bản xem lại quiz đã nộp (sources.lms.saveQuizzes, chỉnh trong Cài đặt hoặc Kho quiz).</summary>
    private static bool SaveQuizzes => Config.Bool("sources.lms.saveQuizzes", true);

    /// <summary>Quiz có lượt đã nộp mà chưa có file lưu, hoặc file kiểu cũ (chưa lọc sesskey, thiếu savedAt).</summary>
    private static bool Unsaved(long quizId, JsonObject prev) =>
        SaveQuizzes && (prev["attempts"] as JsonArray ?? []).OfType<JsonObject>()
            .Any(a => JsonStore.Read(Path.Combine(QuizDir, $"{quizId}-{a["id"]}.json")) is not JsonObject f || f["savedAt"] is null);

    /// <summary>Đánh dấu file tải từ mạng (Zone.Identifier), để Office mở ở Protected View và SmartScreen kiểm file chạy được. URL không kèm token.</summary>
    private static void MarkFromInternet(string path, string url)
    {
        if (!OperatingSystem.IsWindows()) return;
        try { File.WriteAllText(path + ":Zone.Identifier", $"[ZoneTransfer]\r\nZoneId=3\r\nHostUrl={url.Split('?')[0]}\r\n"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException) { }   // ổ không phải NTFS
    }

    /// <summary>Saved without answers (or without review) and the quiz has closed since: try once more.</summary>
    private static bool NeedsRefetch(JsonObject saved, long? close, long now)
    {
        if (saved["savedAt"] is null) return true;   // file kiểu cũ: lưu lại bằng bản đã lọc
        var incomplete = saved["noReview"]?.GetValue<bool>() == true || saved["answers"]?.GetValue<bool>() == false;
        var savedAt = saved["savedAt"]?.GetValue<long>() ?? 0;
        return incomplete && close is { } c && c <= now && savedAt < c;
    }

    [GeneratedRegex(@"<script\b[\s\S]*?</script>|<input type=""hidden""[^>]*>|<div class=""questionflag[\s\S]*?</label>\s*</div>", RegexOptions.IgnoreCase)]
    private static partial Regex ReviewNoiseRx();
    [GeneratedRegex(@"src=""(https?://[^""]+/pluginfile\.php/[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PluginFileRx();

    /// <summary>
    /// Review HTML from LMS: drop scripts and hidden inputs (they carry the session key), embed question images
    /// (pluginfile URLs need the token) as data URIs so the saved quiz works offline and can be shared.
    /// </summary>
    private static async Task<string> CleanReviewAsync(string html, CancellationToken ct)
    {
        html = ReviewNoiseRx().Replace(html, "");
        foreach (var url in PluginFileRx().Matches(html).Select(m => m.Groups[1].Value).Distinct().ToList())
            if (await DataUriAsync(WebUtility.HtmlDecode(url), ct) is { } data) html = html.Replace(url, data);
        return html;
    }

    // ------------------------------------------------------------------ cấu trúc khóa (trang Khóa học)

    [GeneratedRegex("<[^>]+>")] private static partial Regex TagRx();

    private static void SaveStructure(CourseInfo m, JsonArray contents, JsonObject index)
    {
        var local = index.ToDictionary(kv => kv.Key, kv => kv.Value?["path"]?.GetValue<string>());
        var sections = new JsonArray();
        foreach (var sec in contents.OfType<JsonObject>())
        {
            var mods = new JsonArray();
            foreach (var mod in sec["modules"]!.AsArray().OfType<JsonObject>())
            {
                var type = mod["modname"]?.GetValue<string>();
                if (type == "label" && string.IsNullOrWhiteSpace(mod["description"]?.GetValue<string>())) continue;
                var files = new JsonArray();
                foreach (var f in (mod["contents"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    if (f["type"]?.GetValue<string>() == "file")
                    {
                        var p = local.GetValueOrDefault(f["fileurl"]?.GetValue<string>() ?? "");
                        files.Add(new JsonObject { ["name"] = f["filename"]?.GetValue<string>(), ["size"] = f["filesize"]?.GetValue<long>(), ["path"] = p is not null && File.Exists(p) ? Paths.RelativeToStudy(p) : null });
                    }
                    else if (f["type"]?.GetValue<string>() == "url")
                        files.Add(new JsonObject { ["name"] = f["filename"]?.GetValue<string>(), ["url"] = f["fileurl"]?.GetValue<string>() });
                }
                var text = type == "label" ? TagRx().Replace(WebUtility.HtmlDecode(mod["description"]?.GetValue<string>() ?? ""), " ").Trim() : "";
                mods.Add(new JsonObject
                {
                    ["id"] = mod["id"]?.GetValue<long>(),
                    ["name"] = WebUtility.HtmlDecode(mod["name"]?.GetValue<string>() ?? ""),
                    ["type"] = type,
                    ["url"] = mod["url"]?.GetValue<string>(),
                    ["files"] = files,
                    ["text"] = text.Length > 300 ? text[..300] : text,
                });
            }
            if (mods.Count > 0) sections.Add(new JsonObject { ["name"] = WebUtility.HtmlDecode(sec["name"]?.GetValue<string>() ?? ""), ["modules"] = mods });
        }
        var d = m.ToJson();
        d["savedAt"] = Now();
        d["sections"] = sections;
        JsonStore.Write(Path.Combine(CourseDir, $"{m.Id}.json"), d);
    }


    // ------------------------------------------------------------------ helper

    [GeneratedRegex(@"\)_(.+?)\s*\(")] private static partial Regex TeacherRx();
    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1f]")] private static partial Regex BadCharsRx();

    private static CourseInfo CourseMeta(JsonObject c)
    {
        var full = c["fullname"]!.GetValue<string>();
        var s = Organizer.SubjectOf(full)!;
        var t = TeacherRx().Match(full);
        return new CourseInfo(c["id"]!.GetValue<long>(), full, s.Subject, s.Code, s.Part, Organizer.TermOf(full),
            t.Success ? t.Groups[1].Value.Trim() : "", $"{Site}/course/view.php?id={c["id"]}");
    }

    private static string CurrentTerm(List<CourseInfo> metas)
    {
        var t = Config.Str("sources.lms.term");
        return t.Length > 0 && t != "auto" ? t : metas.Select(m => m.Term).Where(x => x.Length > 0).DefaultIfEmpty("").Max()!;
    }

    private static string SafeName(string name)
    {
        var s = BadCharsRx().Replace(name, "_").Trim().TrimEnd('.');
        return s.Length == 0 ? "_" : s.Length > 150 ? s[..150] : s;
    }
}

/// <summary>Một lớp trên LMS, đã tách tên môn / phần / học kỳ / giảng viên.</summary>
public sealed record CourseInfo(long Id, string Name, string Subject, string Code, string? Part, string Term, string Teacher, string Url)
{
    public string LmsFolder => Path.Combine(
        Part is null ? Path.Combine(Organizer.SubjectsRoot, Subject) : Path.Combine(Organizer.SubjectsRoot, Subject, Part),
        Config.Str("folders.lmsSubfolder"));

    public JsonObject ToJson() => new()
    {
        ["id"] = Id,
        ["name"] = Name,
        ["subject"] = Subject,
        ["code"] = Code,
        ["part"] = Part,
        ["term"] = Term,
        ["teacher"] = Teacher,
        ["url"] = Url,
        ["folder"] = Paths.RelativeToStudy(LmsFolder),
    };

    public static CourseInfo FromJson(JsonObject o) => new(
        o["id"]!.GetValue<long>(), o["name"]?.GetValue<string>() ?? "", o["subject"]!.GetValue<string>(), o["code"]?.GetValue<string>() ?? "",
        o["part"]?.GetValue<string>(), o["term"]?.GetValue<string>() ?? "", o["teacher"]?.GetValue<string>() ?? "", o["url"]?.GetValue<string>() ?? "");
}
