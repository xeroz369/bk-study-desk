using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using SoHocTap.Files;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

public sealed partial class LmsSource
{
    private static async Task SaveAssignmentsAsync(List<CourseInfo> courses, JsonArray events, JsonObject index,
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

    private static IEnumerable<(JsonObject Sec, JsonObject Mod, JsonObject File)> Downloadable(JsonArray contents)
    {
        var skip = Config.List("sources.lms.skipFolders");
        var max = Config.Int("sources.lms.maxFileMB", 200) * 1024L * 1024;
        var maxVideo = Config.Int("sources.lms.maxVideoMB", 2048) * 1024L * 1024;
        foreach (var sec in contents.OfType<JsonObject>())
            foreach (var mod in (sec["modules"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if (mod["modname"]?.GetValue<string>() is not ("resource" or "folder")) continue;
                if (skip.Any(s => (mod["name"]?.GetValue<string>() ?? "").Contains(s, StringComparison.OrdinalIgnoreCase))) continue;
                foreach (var f in (mod["contents"] as JsonArray ?? []).OfType<JsonObject>())
                    if (f["type"]?.GetValue<string>() == "file"
                        && (f["filesize"]?.GetValue<long>() ?? 0) <= (KindOf(f["filename"]?.GetValue<string>()) == FileKind.Video ? maxVideo : max))
                        yield return (sec, mod, f);
            }
    }

    private static FileKind KindOf(string? fileName) => FileKinds.Of(fileName);

    public sealed record FileStat(FileKind Kind, long Bytes, bool Have);

    public sealed record SectionInfo(int Index, string Name, IReadOnlyList<FileStat> Items)
    {
        public IEnumerable<FileStat> Of(IReadOnlySet<FileKind> kinds) => Items.Where(f => kinds.Contains(f.Kind));
    }

    private static CourseInfo? Course(long courseId) =>
        LmsStore.Read()?.Courses.Where(c => c.Id == courseId && c.Subject is not null)
            .Select(c => new CourseInfo(c.Id, c.Name ?? "", c.Subject, c.Code ?? "", c.Part, c.Term ?? "", c.Teacher ?? "", c.Url ?? "")).FirstOrDefault();

    public static async Task<List<SectionInfo>> SectionsAsync(long courseId, CancellationToken ct)
    {
        var contents = (JsonArray)await CallAsync("core_course_get_contents", [Arg("courseid", courseId)], ct);
        var index = JsonStore.ReadObject(FilesIndex);
        var files = Downloadable(contents).ToList();
        var list = new List<SectionInfo>();
        var i = 0;
        foreach (var sec in contents.OfType<JsonObject>())
        {
            var mine = files.Where(x => ReferenceEquals(x.Sec, sec)).Select(x => x.File).ToList();
            if (mine.Count > 0)
                list.Add(new SectionInfo(i, WebUtility.HtmlDecode(sec["name"]?.GetValue<string>() ?? "Mục " + i),
                    [.. mine.Select(f => new FileStat(KindOf(f["filename"]?.GetValue<string>()), f["filesize"]?.GetValue<long>() ?? 0, UpToDate(index, f)))]));
            i++;
        }
        return list;
    }

    public static async Task<int> ReadStructuresAsync(IReadOnlyCollection<long> courseIds, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var index = JsonStore.ReadObject(FilesIndex);
            var n = 0;
            foreach (var id in courseIds)
            {
                if (Course(id) is not { } m) continue;
                try
                {
                    var contents = (JsonArray)await CallAsync("core_course_get_contents", [Arg("courseid", id)], ct);
                    SaveStructure(m, contents, index);
                    n++;
                }
                catch (Exception e) when (Recoverable(e, ct)) { Log.Warn($"Đọc cấu trúc lớp {m.Subject} ({id}): {e.Message}"); }
            }
            return n;
        }
        finally { Gate.Release(); }
    }

    private static bool UpToDate(JsonObject index, JsonObject f) =>
        index[f["fileurl"]?.GetValue<string>() ?? ""] is JsonObject e
        && e["timemodified"]?.GetValue<long>() == f["timemodified"]?.GetValue<long>()
        && (File.Exists(LocalCopy(e)) || Directory.Exists(e["extractedTo"]?.GetValue<string>() ?? ""));

    public readonly record struct DownloadResult(int Got, int Failed);

    public static async Task<DownloadResult> DownloadSectionsAsync(long courseId, IReadOnlyCollection<int> sections, bool extract, Action<string> log,
        CancellationToken ct, IReadOnlySet<FileKind>? kinds = null, Action<string>? step = null)
    {
        var m = Course(courseId) ?? throw new InvalidOperationException("Chưa có khóa này, hãy đồng bộ LMS trước.");
        await Gate.WaitAsync(ct);
        try
        {
            var contents = (JsonArray)await CallAsync("core_course_get_contents", [Arg("courseid", courseId)], ct);
            var chosen = contents.OfType<JsonObject>().Where((_, i) => sections.Contains(i)).ToHashSet();
            var index = JsonStore.ReadObject(FilesIndex);
            var hashes = new Dictionary<string, Dictionary<string, string>>();
            int n = 0, failed = 0;
            foreach (var (sec, mod, f) in Downloadable(contents).Where(x => chosen.Contains(x.Sec)
                         && (kinds is null || kinds.Contains(KindOf(x.File["filename"]?.GetValue<string>())))))
            {
                ct.ThrowIfCancellationRequested();
                if (UpToDate(index, f)) continue;
                step?.Invoke(f["filename"]?.GetValue<string>() ?? "");
                try
                {
                    if (await SyncFileAsync(m, sec, mod, f, index, hashes, log, ct, extract) is not null) n++;
                    else if (!UpToDate(index, f)) { failed++; log($"  lỗi: {f["filename"]}: không tải được"); }
                }
                catch (Exception e) when (Recoverable(e, ct))
                {
                    failed++;
                    Log.Warn($"Tải {f["filename"]}: {e.Message}");
                    log($"  lỗi: {f["filename"]}: {e.Message}");
                }
                JsonStore.Write(FilesIndex, index);
            }
            Organizer.SaveHashCache();
            return new(n, failed);
        }
        finally { Gate.Release(); }
    }

    private static async Task<JsonObject?> SyncFileAsync(CourseInfo m, JsonObject sec, JsonObject mod, JsonObject f, JsonObject index,
        Dictionary<string, Dictionary<string, string>> hashesBySubject, Action<string> log, CancellationToken ct, bool? extract = null)
    {
        var url = f["fileurl"]!.GetValue<string>();
        var known = index[url] as JsonObject;
        var modified = f["timemodified"]?.GetValue<long>();
        if (UpToDate(index, f)) return null;

        var sub = new List<string> { SafeName(WebUtility.HtmlDecode(sec["name"]?.GetValue<string>() ?? "Chung")) };
        if (mod["modname"]!.GetValue<string>() == "folder")
        {
            sub.Add(SafeName(mod["name"]!.GetValue<string>()));
            sub.AddRange((f["filepath"]?.GetValue<string>() ?? "/").Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Select(SafeName));
        }
        var dest = Path.Combine([m.LmsFolder, .. sub, SafeName(f["filename"]!.GetValue<string>())]);
        var tmp = Path.Combine(Paths.Data, "tmp", Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url))));
        try { await DownloadAsync(url, tmp, ct); }
        catch (HttpRequestException e) { Warn($"tải {f["filename"]}", e); return null; }

        var subjDir = Path.Combine(Organizer.SubjectsRoot, m.Subject);
        if (!hashesBySubject.TryGetValue(subjDir, out var hashes)) hashesBySubject[subjDir] = hashes = Organizer.HashesIn(subjDir);
        var h = Organizer.Sha256(tmp);
        var knownPath = known?["path"]?.GetValue<string>();
        var lmsSub = Config.Str("folders.lmsSubfolder");
        var place = LmsPlacement.Decide(knownPath, hashes.GetValueOrDefault(h), dest, Organizer.SubjectsRoot, lmsSub, File.Exists);
        var final = LmsPlacement.Apply(tmp, place, old => Path.Combine(Config.Folder("archiveOldVersions"), Paths.RelativeToStudy(old)));
        var (path, dedupOf) = LmsPlacement.IndexFields(place, knownPath, final, Organizer.SubjectsRoot, lmsSub);
        index[url] = IndexEntry(path, dedupOf, modified, m.Id, h);
        if (final is null) return null;
        if (knownPath is not null && place.Kind == PlacementKind.New && File.Exists(knownPath))
            Log.Info($"LMS: bản mới của {f["filename"]} đặt vào Tài liệu LMS, không thay file ngoài cây của app");
        dest = final;
        MarkFromInternet(dest, url);
        hashes[h] = dest;
        var entry = (JsonObject)index[url]!;
        log($"  {(known is not null ? "cập nhật" : "mới")}: {Paths.RelativeToStudy(dest)}");
        if ((extract ?? Settings.Archives.Extract) && Organizer.IsArchive(dest) && Organizer.ExtractArchive(dest, hashes, log) is { } x)
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

    private static JsonObject IndexEntry(string? path, string? dedupOf, long? modified, long course, string sha)
    {
        var o = new JsonObject();
        if (path is not null) o["path"] = path;
        if (dedupOf is not null) o["dedupOf"] = dedupOf;
        o["timemodified"] = modified;
        o["course"] = course;
        o["sha256"] = sha;
        return o;
    }

    private static string? LocalCopy(JsonNode? entry) => entry?["path"]?.GetValue<string>() ?? entry?["dedupOf"]?.GetValue<string>();

    private static void MarkFromInternet(string path, string url)
    {
        if (!OperatingSystem.IsWindows()) return;
        try { File.WriteAllText(path + ":Zone.Identifier", $"[ZoneTransfer]\r\nZoneId=3\r\nHostUrl={url.Split('?')[0]}\r\n"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException) { }
    }

    [GeneratedRegex("<[^>]+>")] private static partial Regex TagRx();

    private static void SaveStructure(CourseInfo m, JsonArray contents, JsonObject index)
    {
        var local = index.ToDictionary(kv => kv.Key, kv => LocalCopy(kv.Value));
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
                        files.Add((JsonNode)new JsonObject { ["name"] = f["filename"]?.GetValue<string>(), ["size"] = f["filesize"]?.GetValue<long>(), ["path"] = p is not null && File.Exists(p) ? Paths.RelativeToStudy(p) : null });
                    }
                    else if (f["type"]?.GetValue<string>() == "url")
                        files.Add((JsonNode)new JsonObject { ["name"] = f["filename"]?.GetValue<string>(), ["url"] = f["fileurl"]?.GetValue<string>() });
                }
                var html = mod["description"]?.GetValue<string>() ?? "";
                var text = type == "label" ? TagRx().Replace(WebUtility.HtmlDecode(html), " ").Trim() : "";
                mods.Add((JsonNode)new JsonObject
                {
                    ["id"] = mod["id"]?.GetValue<long>(),
                    ["name"] = WebUtility.HtmlDecode(mod["name"]?.GetValue<string>() ?? ""),
                    ["type"] = type,
                    ["url"] = mod["url"]?.GetValue<string>(),
                    ["files"] = files,
                    ["text"] = text.Length > 300 ? text[..300] : text,
                    ["links"] = type == "label" ? new JsonArray([.. LmsMedia.LinksIn(html).Select(l => (JsonNode)l)]) : null,
                });
            }
            if (mods.Count > 0) sections.Add((JsonNode)new JsonObject { ["name"] = WebUtility.HtmlDecode(sec["name"]?.GetValue<string>() ?? ""), ["modules"] = mods });
        }
        var d = m.ToJson();
        d["savedAt"] = Now();
        d["sections"] = sections;
        JsonStore.Write(Path.Combine(CourseDir, $"{m.Id}.json"), d);
    }
}
