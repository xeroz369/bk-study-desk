using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Files;

namespace SoHocTap.Library;

public sealed record LibraryOptions(string BaseUrl, string CacheDir, string ManifestPath)
{
    public TimeSpan RefreshEvery { get; init; } = TimeSpan.FromDays(1);
    public TimeSpan TabRefresh { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan Gap { get; init; } = TimeSpan.FromMilliseconds(300);
    public long MaxFileBytes { get; init; } = 200L * 1024 * 1024;
}

public enum LibraryRefresh { CacheOnly, IfDue, Tab, Force }

public enum LibraryOrigin { Network, NotModified, Cache, None }

public sealed record LibraryResult<T>(T? Value, LibraryOrigin Origin, string? Error, DateTimeOffset? CheckedAt) where T : class;

public enum DownloadError { None, Network, Mismatch, TooLarge, NoFiles, BadName }

public sealed record DownloadResult(IReadOnlyList<string> Paths, DownloadError Error, string? Detail)
{
    public bool Ok => Error == DownloadError.None;
}

public sealed record DownloadRecord(string Course, string Item, string File, string Path, string Sha256, long Size, long At, string? Updated);

public enum ItemLocal { None, Downloaded, Outdated }

public sealed class LibraryClient : IDisposable
{
    private const int MaxJsonBytes = 16 * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly LibraryOptions _o;
    private readonly TimeProvider _time;
    private readonly Action<string>? _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Pace _pace;
    private readonly object _manifestGate = new();
    private readonly Dictionary<string, DateTimeOffset> _attempts = [];

    public LibraryClient(HttpClient http, LibraryOptions options, TimeProvider? time = null, Action<string>? log = null)
    {
        _http = http;
        _o = options;
        _time = time ?? TimeProvider.System;
        _log = log;
        _pace = new Pace(() => _o.Gap, () => _time.GetUtcNow().UtcDateTime);
        Root = RootOf(options.BaseUrl);
    }

    public Uri? Root { get; }

    public static Uri? RootOf(string? baseUrl)
    {
        var s = (baseUrl ?? "").Trim();
        if (s.Length == 0 || !Uri.TryCreate(s, UriKind.Absolute, out var u)) return null;
        if (u.Scheme is not ("https" or "http" or "file") || (u.IsFile && u.IsUnc)) return null;
        var text = u.AbsoluteUri.Split('?', '#')[0];
        if (!text.EndsWith('/')) text += "/";
        if (!text.EndsWith("/v1/", StringComparison.OrdinalIgnoreCase)) text += "v1/";
        return new Uri(text);
    }

    public Task<LibraryResult<LibraryIndex>> GetIndexAsync(LibraryRefresh mode, CancellationToken ct) =>
        GetAsync("index", Root is null ? null : new Uri(Root, "index.json"), LibraryJson.ParseIndex, mode, ct);

    public Task<LibraryResult<CourseDetail>> GetCourseAsync(CourseRef course, LibraryRefresh mode, CancellationToken ct)
    {
        if (!LibraryJson.CourseId().IsMatch(course.Id)) return Task.FromResult(new LibraryResult<CourseDetail>(null, LibraryOrigin.None, "id môn không hợp lệ", null));
        return GetAsync("course-" + course.Id, DetailUri(course), json =>
        {
            var d = LibraryJson.ParseCourse(json);
            return d.Id == course.Id ? d : throw new DataReadException($"file chi tiết là của môn {d.Id}, không phải {course.Id}");
        }, mode, ct);
    }

    private Uri? DetailUri(CourseRef course)
    {
        if (Root is null) return null;
        var rel = course.Detail is { Length: > 0 } d ? d : $"courses/{course.Id}.json";
        if (rel.Contains("..", StringComparison.Ordinal) || rel.Contains('\\') || Uri.TryCreate(rel, UriKind.Absolute, out _)) return null;
        var u = new Uri(Root, rel);
        return u.AbsoluteUri.StartsWith(Root.AbsoluteUri, StringComparison.Ordinal) ? u : null;
    }

    public DateTimeOffset? LastChecked => ReadMeta("index") is { } m && m.Base == Root?.AbsoluteUri ? m.CheckedAt : null;

    public void ClearCache()
    {
        if (!Directory.Exists(_o.CacheDir)) return;
        foreach (var f in Directory.GetFiles(_o.CacheDir, "*.json")) File.Delete(f);
        lock (_attempts) _attempts.Clear();
    }

    private sealed record Meta(string? Base, string? Url, string? ETag, DateTimeOffset? CheckedAt);

    private string BodyPath(string key) => Path.Combine(_o.CacheDir, key + ".json");
    private string MetaPath(string key) => Path.Combine(_o.CacheDir, key + ".meta.json");

    private Meta? ReadMeta(string key)
    {
        try
        {
            if (!File.Exists(MetaPath(key))) return null;
            var o = JsonNode.Parse(File.ReadAllText(MetaPath(key))) as JsonObject;
            long? at = o?["checkedAt"] is JsonValue v && v.TryGetValue<long>(out var n) ? n : null;
            return new Meta(o?["base"]?.GetValue<string>(), o?["url"]?.GetValue<string>(), o?["etag"]?.GetValue<string>(),
                at is { } t ? DateTimeOffset.FromUnixTimeSeconds(t) : null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { return null; }
    }

    private void WriteMeta(string key, Meta m) =>
        AtomicFile.WriteIfChanged(MetaPath(key), new JsonObject
        {
            ["base"] = m.Base,
            ["url"] = m.Url,
            ["etag"] = m.ETag,
            ["checkedAt"] = m.CheckedAt?.ToUnixTimeSeconds(),
        }.ToJsonString());

    private string? ReadBody(string key)
    {
        try { return File.Exists(BodyPath(key)) ? File.ReadAllText(BodyPath(key)) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    private async Task<LibraryResult<T>> GetAsync<T>(string key, Uri? uri, Func<string, T> parse, LibraryRefresh mode, CancellationToken ct) where T : class
    {
        var meta = ReadMeta(key);
        if (meta is not null && (Root is null || meta.Base != Root.AbsoluteUri)) meta = null;
        T? cached = null;
        if (meta is not null && ReadBody(key) is { } text)
        {
            try { cached = parse(text); }
            catch (DataReadException e) { _log?.Invoke($"Thư viện: cache {key} hỏng, bỏ qua: {e.Message}"); meta = null; }
        }
        LibraryResult<T> FromCache(string? error) => new(cached, cached is null ? LibraryOrigin.None : LibraryOrigin.Cache, error, meta?.CheckedAt);
        if (uri is null) return FromCache(Root is null ? "chưa đặt địa chỉ thư viện" : "đường dẫn chi tiết môn không hợp lệ");
        if (!Due(key, meta, mode)) return FromCache(null);

        lock (_attempts) _attempts[key] = _time.GetUtcNow();
        var f = await FetchTextAsync(uri, cached is null ? null : meta?.ETag, ct).ConfigureAwait(false);
        var now = _time.GetUtcNow();
        if (f.NotModified && cached is not null)
        {
            WriteMeta(key, meta! with { CheckedAt = now });
            return new(cached, LibraryOrigin.NotModified, null, now);
        }
        if (f.Error is not null || f.Text is null)
        {
            _log?.Invoke($"Thư viện: không tải được {uri.AbsolutePath}: {f.Error}");
            return FromCache(f.Error ?? "không có nội dung");
        }
        T fresh;
        try { fresh = parse(f.Text); }
        catch (DataReadException e)
        {
            _log?.Invoke($"Thư viện: {uri.AbsolutePath} không đọc được: {e.Message}");
            return FromCache(e.Message);
        }
        AtomicFile.WriteIfChanged(BodyPath(key), f.Text);
        WriteMeta(key, new Meta(Root!.AbsoluteUri, uri.AbsoluteUri, f.ETag, now));
        return new(fresh, LibraryOrigin.Network, null, now);
    }

    private bool Due(string key, Meta? meta, LibraryRefresh mode)
    {
        var now = _time.GetUtcNow();
        DateTimeOffset? tried;
        lock (_attempts) tried = _attempts.TryGetValue(key, out var t) ? t : null;
        return mode switch
        {
            LibraryRefresh.CacheOnly => false,
            LibraryRefresh.Force => true,
            LibraryRefresh.Tab => Latest(meta?.CheckedAt, tried) is not { } last || now - last >= _o.TabRefresh,
            _ => (meta?.CheckedAt is not { } ok || now - ok >= _o.RefreshEvery) && (tried is null || now - tried >= _o.TabRefresh),
        };
    }

    private static DateTimeOffset? Latest(DateTimeOffset? a, DateTimeOffset? b) => a is null ? b : b is null ? a : a > b ? a : b;

    private sealed record Fetched(string? Text, string? ETag, bool NotModified, string? Error);

    private async Task<Fetched> FetchTextAsync(Uri uri, string? etag, CancellationToken ct)
    {
        if (uri.IsFile)
        {
            try
            {
                var info = new FileInfo(uri.LocalPath);
                if (!info.Exists) return new(null, null, false, "không có file " + info.Name);
                if (info.Length > MaxJsonBytes) return new(null, null, false, "file quá lớn");
                return new(await File.ReadAllTextAsync(info.FullName, ct).ConfigureAwait(false), null, false, null);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new(null, null, false, e.Message); }
        }
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _pace.WaitAsync(ct).ConfigureAwait(false);
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            if (etag is not null && EntityTagHeaderValue.TryParse(etag, out var tag)) req.Headers.IfNoneMatch.Add(tag);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_o.Timeout);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotModified) return new(null, etag, true, null);
            if (Pace.IsThrottle((int)resp.StatusCode))
                _pace.PauseFor(Pace.Backoff(Pace.RetryAfter(resp.Headers.RetryAfter, resp.Headers.Date, _time.GetUtcNow())));
            if (!resp.IsSuccessStatusCode) return new(null, null, false, $"HTTP {(int)resp.StatusCode}");
            if (resp.Content.Headers.ContentLength > MaxJsonBytes) return new(null, null, false, "file quá lớn");
            var bytes = await resp.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
            if (bytes.Length > MaxJsonBytes) return new(null, null, false, "file quá lớn");
            return new(Encoding.UTF8.GetString(bytes), resp.Headers.ETag?.ToString(), false, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(null, null, false, "hết thời gian chờ"); }
        catch (HttpRequestException e) { return new(null, null, false, e.Message); }
        catch (IOException e) { return new(null, null, false, e.Message); }
        finally { _gate.Release(); }
    }

    public static Uri? SiteOf(LibraryIndex? index) =>
        index?.Site is { } s && Uri.TryCreate(s.EndsWith('/') ? s : s + "/", UriKind.Absolute, out var u) && u.Scheme is "https" or "http" ? u : null;

    public static Uri? ContributeUrl(LibraryIndex? index, string path = "gui-tai-lieu/") =>
        SiteOf(index) is { } site && path.Length > 0 && !path.Contains("..", StringComparison.Ordinal) && !path.Contains(':')
            ? new Uri(site, path.TrimStart('/')) : null;

    public static Uri? CourseUrl(LibraryIndex? index, CourseRef course)
    {
        if (course.Url is { } s && Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme is "https" or "http") return u;
        return SiteOf(index) is { } site && LibraryJson.CourseId().IsMatch(course.Id) ? new Uri(site, $"course/{course.Id}/") : null;
    }

    public static Uri? ItemUrl(LibraryIndex? index, CourseRef course, LibraryItem item)
    {
        if (item.IsLink)
            return item.Url is { } s && Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme is "https" or "http" ? u : null;
        return CourseUrl(index, course) is { } c ? new Uri(c, "#" + item.Id) : null;
    }

    public async Task<DownloadResult> DownloadAsync(CourseRef course, LibraryItem item, string targetDir, CancellationToken ct)
    {
        if (item.Removed || item.Files is not { Count: > 0 } files) return new([], DownloadError.NoFiles, null);
        var paths = new List<string>();
        foreach (var file in files)
        {
            if (SafeFileName(file.Name) is not { } name) return new(paths, DownloadError.BadName, file.Name);
            if (file.Size > _o.MaxFileBytes) return new(paths, DownloadError.TooLarge, name);
            var dest = Path.Combine(targetDir, name);
            var mine = Record(course.Id, item.Id, name);
            var present = new[] { mine?.Path, dest }.FirstOrDefault(p => p is not null && File.Exists(p) && Same(HashOf(p), file.Sha256));
            if (present is not null)
            {
                if (mine is not null && SamePath(mine.Path, present) && mine.Updated != item.Updated) SaveRecord(mine with { Updated = item.Updated });
                paths.Add(present);
                continue;
            }
            Directory.CreateDirectory(targetDir);
            string? lastError = null;
            var mismatch = false;
            string? placed = null;
            foreach (var raw in file.Urls ?? [])
            {
                if (FileUri(raw) is not { } url) { lastError = "url không hợp lệ"; continue; }
                var tmp = Path.Combine(targetDir, $".{name}.{Guid.NewGuid():N}.part");
                try
                {
                    var (hash, size) = await DownloadToAsync(url, tmp, file.Size, ct).ConfigureAwait(false);
                    if (size != file.Size || !Same(hash, file.Sha256))
                    {
                        mismatch = true;
                        lastError = $"sha256 không khớp ({url.Host})";
                        _log?.Invoke($"Thư viện: {name} từ {url.Host} sai sha256 hoặc cỡ ({size} byte), thử url khác");
                        continue;
                    }
                    placed = Place(tmp, dest, mine);
                    MarkFromWeb(placed, url);
                    break;
                }
                catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException
                                              || (e is OperationCanceledException && !ct.IsCancellationRequested))
                {
                    lastError = e is OperationCanceledException ? "hết thời gian chờ" : e.Message;
                    _log?.Invoke($"Thư viện: tải {name} từ {url.Host} lỗi: {lastError}");
                }
                finally
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                }
            }
            if (placed is null) return new(paths, mismatch ? DownloadError.Mismatch : DownloadError.Network, lastError);
            SaveRecord(new DownloadRecord(course.Id, item.Id, name, placed, file.Sha256.ToLowerInvariant(), file.Size, _time.GetUtcNow().ToUnixTimeSeconds(), item.Updated));
            paths.Add(placed);
        }
        return new(paths, DownloadError.None, null);
    }

    private Uri? FileUri(string raw)
    {
        if (Uri.TryCreate(raw, UriKind.Absolute, out var u))
            return u.Scheme is "https" or "http" || (u.IsFile && !u.IsUnc && Root is { IsFile: true }) ? u : null;
        return Root is { IsFile: true } root && !raw.Contains("..", StringComparison.Ordinal) ? new Uri(root, raw) : null;
    }

    private async Task<(string Hash, long Size)> DownloadToAsync(Uri url, string tmp, long expected, CancellationToken ct)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long total = 0;
        var buffer = new byte[81920];
        await using (var output = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            if (url.IsFile)
            {
                await using var input = new FileStream(url.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                total = await CopyAsync(input, output, sha, expected, buffer, ct).ConfigureAwait(false);
            }
            else
            {
                await _gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await _pace.WaitAsync(ct).ConfigureAwait(false);
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(_o.Timeout + TimeSpan.FromMinutes(expected / (10.0 * 1024 * 1024)));
                    using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                    if (Pace.IsThrottle((int)resp.StatusCode))
                        _pace.PauseFor(Pace.Backoff(Pace.RetryAfter(resp.Headers.RetryAfter, resp.Headers.Date, _time.GetUtcNow())));
                    if (!resp.IsSuccessStatusCode) throw new HttpRequestException($"HTTP {(int)resp.StatusCode}");
                    await using var input = await resp.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
                    total = await CopyAsync(input, output, sha, expected, buffer, cts.Token).ConfigureAwait(false);
                }
                finally { _gate.Release(); }
            }
        }
        return (Convert.ToHexStringLower(sha.GetHashAndReset()), total);
    }

    private static async Task<long> CopyAsync(Stream input, Stream output, IncrementalHash sha, long expected, byte[] buffer, CancellationToken ct)
    {
        long total = 0;
        int n;
        while ((n = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            total += n;
            if (total > expected) return total;
            sha.AppendData(buffer, 0, n);
            await output.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
        }
        return total;
    }

    private static string Place(string tmp, string dest, DownloadRecord? mine)
    {
        if (File.Exists(dest))
        {
            var own = mine is not null && SamePath(mine.Path, dest) && Same(HashOf(dest), mine.Sha256);
            if (own)
            {
                File.Move(tmp, dest, overwrite: true);
                return dest;
            }
            dest = FileOps.UniquePath(dest);
        }
        File.Move(tmp, dest);
        return dest;
    }

    private static void MarkFromWeb(string path, Uri url)
    {
        if (!OperatingSystem.IsWindows() || url.IsFile) return;
        try { File.WriteAllText(path + ":Zone.Identifier", $"[ZoneTransfer]\r\nZoneId=3\r\nHostUrl={url.GetLeftPart(UriPartial.Path)}\r\n"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException) { }
    }

    public static string? SafeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var last = name.Replace('\\', '/').Split('/')[^1];
        var invalid = Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '|', '?', '*']).ToHashSet();
        var s = new string([.. NameMatch.Nfc(last).Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c)]).Trim().TrimEnd('.').TrimStart('.');
        return s.Length == 0 ? null : s;
    }

    private static bool Same(string? a, string? b) => a is not null && b is not null && a.Equals(b, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static string? HashOf(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexStringLower(SHA256.HashData(fs));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public IReadOnlyList<DownloadRecord> Records()
    {
        lock (_manifestGate) return ReadManifest();
    }

    public IReadOnlyList<string> DownloadedFiles(string courseId, string itemId) =>
        [.. Records().Where(r => r.Course == courseId && r.Item == itemId && File.Exists(r.Path)).Select(r => r.Path)];

    public ItemLocal LocalState(string courseId, LibraryItem item)
    {
        var recs = Records().Where(r => r.Course == courseId && r.Item == item.Id && File.Exists(r.Path)).ToList();
        if (recs.Count == 0) return ItemLocal.None;
        foreach (var f in item.Files ?? [])
        {
            var rec = recs.FirstOrDefault(r => r.File == SafeFileName(f.Name));
            if (rec is null || !Same(rec.Sha256, f.Sha256) || string.CompareOrdinal(rec.Updated ?? "", item.Updated ?? "") < 0) return ItemLocal.Outdated;
        }
        return ItemLocal.Downloaded;
    }

    private DownloadRecord? Record(string course, string item, string file) =>
        Records().FirstOrDefault(r => r.Course == course && r.Item == item && r.File == file);

    private void SaveRecord(DownloadRecord rec)
    {
        lock (_manifestGate)
        {
            var list = ReadManifest().Where(r => !(r.Course == rec.Course && r.Item == rec.Item && r.File == rec.File)).Append(rec).ToList();
            WriteManifest(list);
        }
    }

    public IReadOnlyList<string> ApplyRemovals(CourseDetail detail)
    {
        var gone = (detail.Items ?? []).Where(i => i.Removed).Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        if (gone.Count == 0) return [];
        var deleted = new List<string>();
        lock (_manifestGate)
        {
            var list = ReadManifest();
            var keep = new List<DownloadRecord>();
            foreach (var r in list)
            {
                if (r.Course != detail.Id || !gone.Contains(r.Item)) { keep.Add(r); continue; }
                try
                {
                    if (File.Exists(r.Path) && Same(HashOf(r.Path), r.Sha256))
                    {
                        File.Delete(r.Path);
                        deleted.Add(r.Path);
                        _log?.Invoke($"Thư viện: mục {r.Item} đã gỡ, xóa bản đã tải {Path.GetFileName(r.Path)}");
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    _log?.Invoke($"Thư viện: chưa xóa được {Path.GetFileName(r.Path)}: {e.Message}");
                    keep.Add(r);
                }
            }
            if (keep.Count != list.Count) WriteManifest(keep);
        }
        return deleted;
    }

    private List<DownloadRecord> ReadManifest()
    {
        try
        {
            if (!File.Exists(_o.ManifestPath)) return [];
            return DataJson.Parse<List<DownloadRecord>>(File.ReadAllText(_o.ManifestPath))?.Where(r => r?.Path is not null && r.Sha256 is not null).ToList() ?? [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DataReadException)
        {
            _log?.Invoke($"Thư viện: không đọc được manifest: {e.Message}");
            return [];
        }
    }

    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = CoreJson.Default,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
    };

    private void WriteManifest(List<DownloadRecord> list) => AtomicFile.WriteIfChanged(_o.ManifestPath, Js.Serialize(list, ManifestJson));

    public void Dispose()
    {
        _gate.Dispose();
        _pace.Dispose();
    }
}
