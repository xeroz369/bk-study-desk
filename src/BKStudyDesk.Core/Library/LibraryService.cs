using SoHocTap.Core;
using SoHocTap.Files;

namespace SoHocTap.Library;

internal sealed class LibraryService : IDisposable
{
    private readonly HttpClient _http = new(new SocketsHttpHandler { UseCookies = false, AutomaticDecompression = System.Net.DecompressionMethods.All })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };
    private readonly object _gate = new();
    private LibraryClient? _client;
    private string? _clientKey;

    public LibraryIndex? Index { get; private set; }

    public string? LastError { get; private set; }

    public event Action? Changed;

    public static string BaseUrl => Settings.Library.BaseUrl.Trim();

    public bool Active => Client.Root is not null;

    public DateTimeOffset? LastChecked => Client.LastChecked;

    private LibraryClient Client
    {
        get
        {
            var o = new LibraryOptions(BaseUrl, Paths.DataFile("library-cache"), Paths.DataFile("library-downloads.json"))
            {
                RefreshEvery = TimeSpan.FromHours(Math.Max(1, Config.Int("library.refreshHours", 24))),
                TabRefresh = TimeSpan.FromMinutes(Math.Max(1, Config.Int("library.tabRefreshMinutes", 10))),
                Timeout = TimeSpan.FromSeconds(Math.Clamp(Config.Int("library.timeoutSeconds", 30), 5, 300)),
                Gap = TimeSpan.FromMilliseconds(Math.Max(0, Config.Int("library.gapMs", 300))),
                MaxFileBytes = Math.Max(1, Config.Int("library.maxFileMB", 200)) * 1024L * 1024,
            };
            lock (_gate)
            {
                var key = o.ToString();
                if (_client is null || key != _clientKey)
                {
                    _client = new LibraryClient(_http, o, log: Log.Info);
                    _clientKey = key;
                    Index = null;
                }
                return _client;
            }
        }
    }

    public async Task<LibraryIndex?> RefreshAsync(LibraryRefresh mode, CancellationToken ct = default)
    {
        var client = Client;
        if (client.Root is null) return null;
        LibraryResult<LibraryIndex> r;
        try { r = await Task.Run(() => client.GetIndexAsync(mode, ct), ct).ConfigureAwait(false); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Thư viện: đọc cache lỗi: {e.Message}");
            return Index;
        }
        var before = Index;
        if (r.Value is not null && (before is null || r.Origin == LibraryOrigin.Network)) Index = r.Value;
        LastError = r.Error;
        if (!ReferenceEquals(before, Index) || r.Error is not null) Changed?.Invoke();
        return Index;
    }

    public IReadOnlyList<CourseMatch> Match(IEnumerable<string?> codes) =>
        Index?.Courses is { } courses ? LibraryMatch.FindAll(codes, courses) : [];

    public async Task<(LibraryResult<CourseDetail> Result, IReadOnlyList<string> Cleaned)> CourseAsync(CourseRef course, LibraryRefresh mode, CancellationToken ct = default)
    {
        var client = Client;
        var active = Active;
        var r = await Task.Run(() => client.GetCourseAsync(course, active ? mode : LibraryRefresh.CacheOnly, ct), ct).ConfigureAwait(false);
        IReadOnlyList<string> cleaned = [];
        if (r.Value is { } d)
        {
            try { cleaned = client.ApplyRemovals(d); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Warn($"Thư viện: dọn mục đã gỡ lỗi: {e.Message}"); }
        }
        return (r, cleaned);
    }

    public static string TargetDir(string subject) =>
        Path.Combine(Organizer.SubjectsRoot, subject, Config.Str("folders.librarySubfolder", "Thư viện"));

    public Task<DownloadResult> DownloadAsync(CourseRef course, LibraryItem item, string subject, CancellationToken ct = default) =>
        Client.DownloadAsync(course, item, TargetDir(subject), ct);

    public ItemLocal LocalState(CourseRef course, LibraryItem item) => Client.LocalState(course.Id, item);

    public IReadOnlyList<string> DownloadedFiles(CourseRef course, LibraryItem item) => Client.DownloadedFiles(course.Id, item.Id);

    public void SetBaseUrl(string url)
    {
        Config.Set("library.baseUrl", url.Trim());
        Index = null;
        Changed?.Invoke();
        _ = RefreshAsync(LibraryRefresh.Force);
    }

    public void ClearCache()
    {
        Client.ClearCache();
        Index = null;
        LastError = null;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _http.Dispose();
        lock (_gate) _client?.Dispose();
    }
}
