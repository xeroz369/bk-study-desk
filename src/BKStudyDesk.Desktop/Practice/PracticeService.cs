using System.Text;
using System.Text.Json.Nodes;
using SoHocTap.Api;
using SoHocTap.Core;
using SoHocTap.Presentation.Practice;
using SoHocTap.Presentation.Practice.Pages;

namespace BKStudyDesk.Desktop.Practice;

/// <summary>
/// Dữ liệu của trang Luyện tập native: nạp nội dung có sẵn (content/manifest.json và .md), gói đã cài (content/packs hay data/packs),
/// quiz LMS đã lưu (qua ApiRouter của 1.x) vào một StudyRegistry, và kết quả luyện tập (ket-qua.json) vào PracticeProgress.
/// Lỗi từng file chỉ thành cảnh báo, không làm hỏng phần còn lại.
/// </summary>
public sealed class PracticeService(ApiRouter router)
{
    private static readonly IReadOnlyDictionary<string, string> NoQuery = new Dictionary<string, string>();

    public StudyRegistry Study { get; private set; } = new(s => Html.Sanitize(s));
    public PracticeProgress Progress { get; private set; } = null!;
    public List<InstalledPack> Packs { get; } = [];
    public List<QuizInfo> Quizzes { get; private set; } = [];
    public List<string> Warnings { get; } = [];
    public bool Loaded { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Study = new StudyRegistry(s => Html.Sanitize(s));
        Warnings.Clear();
        Packs.Clear();
        if (File.Exists(Path.Combine(Paths.Content, "manifest.json"))) Warnings.AddRange(ContentLibrary.Load(Paths.Content, Study));
        LoadPacks();
        await LoadQuizzesAsync(ct);
        var old = Progress;
        Progress = new PracticeProgress(Study, new ApiProgressStorage(router));
        await Progress.LoadAsync(ct);
        if (old != null) Progress.Absorb(old);   // câu làm lúc ket-qua.json đọc lỗi
        Progress.SeedSchedule();
        Loaded = true;
    }

    private void LoadPacks()
    {
        if (!Directory.Exists(Paths.Packs)) return;
        foreach (var file in Directory.EnumerateFiles(Paths.Packs, "*" + PackSuffix).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            try
            {
                var text = File.ReadAllText(file);
                var report = PackValidator.Validate(JsonNode.Parse(text), new FileInfo(file).Length);
                var pack = report.Ok ? StudyPack.Parse(text) : null;
                if (pack is null)
                {
                    Packs.Add(new InstalledPack(name, null, string.Join("; ", report.Errors.Take(3).Select(e => $"{e.Path} {e.Message}")), 0));
                    continue;
                }
                Study.AddPack(pack);
                Packs.Add(new InstalledPack(name, pack, null, PackValidator.CountQuestions(pack)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                Packs.Add(new InstalledPack(name, null, e.Message, 0));
            }
        }
    }

    private async Task LoadQuizzesAsync(CancellationToken ct)
    {
        var r = await router.HandleAsync(new ApiRequest("GET", "/api/quizzes", NoQuery, null), ct);
        if (r.Status != 200)
        {
            Warnings.Add($"quiz LMS: HTTP {r.Status}");
            return;
        }
        var (packs, info) = LmsQuiz.QuizPacks(SavedQuiz.ListFromJson(Encoding.UTF8.GetString(r.Body)), DateTimeOffset.Now, TimeZoneInfo.Local);
        foreach (var p in packs) Study.AddPack(p);
        Quizzes = info;
    }

    public PracticeHomeModel Home() => PracticeHome.Build(Study, Progress, Packs, Quizzes);

    /// <summary>Tạo, nhập, xuất gói trên sổ đang nạp.</summary>
    public Transfer Transfer() => new(Study, Packs, Quizzes, SoHocTap.Shell.AppInfo.Name);

    /// <summary>Cảnh báo nạp nội dung và lỗi đọc ket-qua.json, để hiện ở khung lỗi đầu trang.</summary>
    public List<string> Problems() => [.. Warnings, .. Progress.SaveError is { Length: > 0 } s ? [s] : Array.Empty<string>()];

    /// <summary>Gỡ một gói đã cài (file trong content/packs). Trả lỗi để hiện, null khi xong.</summary>
    public async Task<string?> RemovePackAsync(string file, CancellationToken ct = default)
    {
        var id = file.EndsWith(PackSuffix, StringComparison.OrdinalIgnoreCase) ? file[..^PackSuffix.Length] : file;
        var r = await router.HandleAsync(new ApiRequest("POST", "/api/packs/delete", new Dictionary<string, string> { ["id"] = id }, null), ct);
        return r.Status == 200 ? null : ErrorOf(r);
    }

    /// <summary>Mở tài liệu nguồn của bài đúng trang (như nút Mở slide của bản Svelte). False khi không thấy file.</summary>
    public async Task<bool> OpenSourceAsync(string subject, string file, int page, CancellationToken ct = default)
    {
        var q = new Dictionary<string, string> { ["subject"] = subject, ["file"] = file, ["page"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        return (await router.HandleAsync(new ApiRequest("POST", "/api/source", q, null), ct)).Status == 200;
    }

    /// <summary>
    /// Cài một gói (tạo câu, nhập, cập nhật gói đã có): chuyển kết quả theo fingerprint trước khi id câu đổi, ghi gói qua
    /// POST /api/packs, ghi kết quả. Trả lỗi để hiện, null khi xong. Người gọi nạp lại sổ sau đó.
    /// </summary>
    public async Task<string?> InstallAsync(StudyPack p, CancellationToken ct = default)
    {
        var (moves, gone) = Transfer().Migration(p);
        var r = await router.HandleAsync(new ApiRequest("POST", "/api/packs", NoQuery, p.ToJson()), ct);
        if (r.Status != 200) return ErrorOf(r);
        Progress.MigrateIds(moves, gone);
        await Progress.FlushAsync(ct);
        return null;
    }

    /// <summary>Môn đang học kỳ này (LMS, MyBK đã sync): chọn môn khi soạn. Lỗi hay chưa sync thì danh sách rỗng.</summary>
    public async Task<List<(string Code, string Name)>> SubjectsAsync(CancellationToken ct = default)
    {
        var r = await router.HandleAsync(new ApiRequest("GET", "/api/subjects", NoQuery, null), ct);
        if (r.Status != 200) return [];
        try
        {
            return [.. (JsonNode.Parse(r.Body) as JsonArray ?? []).OfType<JsonObject>()
                .Select(o => (o["code"]?.GetValue<string>() ?? "", o["name"]?.GetValue<string>() ?? ""))
                .Where(x => x.Item1.Length > 0)];
        }
        catch (System.Text.Json.JsonException) { return []; }
    }

    /// <summary>Công tắc tự lưu quiz LMS (cùng giá trị với ô trong Cài đặt). Null khi không đọc được.</summary>
    public async Task<bool?> SaveQuizzesAsync(bool? set = null, CancellationToken ct = default)
    {
        var r = set is { } on
            ? await router.HandleAsync(new ApiRequest("POST", "/api/prefs", new Dictionary<string, string> { ["saveQuizzes"] = on ? "true" : "false" }, null), ct)
            : await router.HandleAsync(new ApiRequest("GET", "/api/prefs", NoQuery, null), ct);
        if (r.Status != 200) return null;
        try { return JsonNode.Parse(r.Body)?["saveQuizzes"]?.GetValue<bool>(); }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException) { return null; }
    }

    /// <summary>Đồng bộ LMS ngay (lưu quiz vừa nộp). False khi app đang hay vừa đồng bộ, hoặc chưa đăng nhập.</summary>
    public async Task<bool> SyncLmsAsync(CancellationToken ct = default) =>
        (await router.HandleAsync(new ApiRequest("POST", "/api/lms/sync", NoQuery, null), ct)).Status == 200;

    /// <summary>Tên tác giả ghi vào gói, nhớ trên máy này.</summary>
    public static string Author
    {
        get => Config.Str("practice.author");
        set => Config.Set("practice.author", value);
    }

    private const string PackSuffix = ".studypack.json";

    /// <summary>Chữ <c>error</c> của response lỗi; không đọc được thì dùng mã HTTP.</summary>
    internal static string ErrorOf(ApiResponse r)
    {
        try { return JsonNode.Parse(r.Body)?["error"]?.GetValue<string>() ?? $"HTTP {r.Status}"; }
        catch (System.Text.Json.JsonException) { return $"HTTP {r.Status}"; }
    }
}
