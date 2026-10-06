using SoHocTap.Core;
using SoHocTap.Files;

namespace SoHocTap.Library;

/// <summary>
/// Nối LibraryClient với config và thư mục của app (phần không test được bằng unit test vì đọc Config/Paths).
/// Thư viện luôn bật (tab Thư viện luôn có); chưa có địa chỉ thì không gọi mạng gì cả.
/// Mọi việc chạy nền; <see cref="Changed"/> bắn từ thread pool, UI tự chuyển về UI thread.
/// </summary>
internal sealed class LibraryService : IDisposable
{
    // Không cookie, không header riêng: chỉ GET thường tới trang thư viện.
    private readonly HttpClient _http = new(new SocketsHttpHandler { UseCookies = false, AutomaticDecompression = System.Net.DecompressionMethods.All })
    {
        Timeout = Timeout.InfiniteTimeSpan,   // timeout theo từng request trong LibraryClient
    };
    private readonly object _gate = new();
    private LibraryClient? _client;
    private string? _clientKey;

    /// <summary>Index đã đọc (cache hoặc mạng) gần nhất; null nếu chưa có.</summary>
    public LibraryIndex? Index { get; private set; }

    /// <summary>Lỗi của lần làm mới index gần nhất (vẫn có thể có Index từ cache).</summary>
    public string? LastError { get; private set; }

    /// <summary>Index vừa đổi (đọc xong từ cache hay mạng).</summary>
    public event Action? Changed;

    public static string BaseUrl => Settings.Library.BaseUrl.Trim();

    /// <summary>Có địa chỉ hợp lệ: mới được gọi mạng.</summary>
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
                    // Đổi địa chỉ/cài đặt: client mới (cache của địa chỉ khác tự bị bỏ qua). Client cũ không Dispose ngay vì có thể
                    // còn request đang chạy; nó chỉ giữ một semaphore.
                    _client = new LibraryClient(_http, o, log: Log.Info);
                    _clientKey = key;
                    Index = null;
                }
                return _client;
            }
        }
    }

    /// <summary>Làm mới index (theo <paramref name="mode"/>). Không bật thì không làm gì. Không bao giờ throw lỗi mạng.</summary>
    public async Task<LibraryIndex?> RefreshAsync(LibraryRefresh mode, CancellationToken ct = default)
    {
        var client = Client;
        if (client.Root is null) return null;   // chưa có địa chỉ: không có gì để đọc, kể cả cache
        LibraryResult<LibraryIndex> r;
        // Task.Run: đọc cache (đĩa) cũng không chạy trên UI thread.
        try { r = await Task.Run(() => client.GetIndexAsync(mode, ct), ct).ConfigureAwait(false); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Thư viện: đọc cache lỗi: {e.Message}");
            return Index;
        }
        // Bản trong RAM chỉ thay khi có bản mới thật (200) hoặc chưa có gì: 304 hay đọc lại cache thì giữ object cũ, UI khỏi vẽ lại.
        var before = Index;
        if (r.Value is not null && (before is null || r.Origin == LibraryOrigin.Network)) Index = r.Value;
        LastError = r.Error;
        if (!ReferenceEquals(before, Index) || r.Error is not null) Changed?.Invoke();
        return Index;
    }

    /// <summary>Môn thư viện cho các mã của một môn (LMS + MyBK). Index chưa có thì rỗng.</summary>
    public IReadOnlyList<CourseMatch> Match(IEnumerable<string?> codes) =>
        Index?.Courses is { } courses ? LibraryMatch.FindAll(codes, courses) : [];

    /// <summary>Chi tiết môn; đọc xong thì dọn bản đã tải của mục bị gỡ.</summary>
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

    /// <summary>Môn học\&lt;môn&gt;\Thư viện\: thư mục app quản lý, file người dùng trong đó không bao giờ bị ghi đè.</summary>
    public static string TargetDir(string subject) =>
        Path.Combine(Organizer.SubjectsRoot, subject, Config.Str("folders.librarySubfolder", "Thư viện"));

    public Task<DownloadResult> DownloadAsync(CourseRef course, LibraryItem item, string subject, CancellationToken ct = default) =>
        Client.DownloadAsync(course, item, TargetDir(subject), ct);

    public ItemLocal LocalState(CourseRef course, LibraryItem item) => Client.LocalState(course.Id, item);

    public IReadOnlyList<string> DownloadedFiles(CourseRef course, LibraryItem item) => Client.DownloadedFiles(course.Id, item.Id);

    /// <summary>Đổi địa chỉ (chỉ khi bật log chẩn đoán): client mới, đọc lại index.</summary>
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
