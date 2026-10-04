using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Files;

namespace SoHocTap.Api;

/// <summary>Những việc khung HTML nhờ app làm hộ (Shell implement).</summary>
public interface IShellActions
{
    /// <summary>Web của trường thì mở trong window của app (dùng chung session), trang khác thì mở ra browser.</summary>
    bool OpenWeb(string url, string title);

    /// <summary>Sync LMS now (saves finished quiz reviews). False if already running or not signed in.</summary>
    bool SyncLms();
}

public sealed record ApiRequest(string Method, string Path, IReadOnlyDictionary<string, string> Query, string? Body);

public sealed record ApiResponse(int Status, string ContentType, byte[] Body)
{
    public static ApiResponse Json(JsonNode? node, int status = 200) =>
        new(status, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(node?.ToJsonString(JsonStore.Options) ?? "null"));

    public static ApiResponse Error(int status, string message) => Json(new JsonObject { ["error"] = message }, status);
}

/// <summary>
/// Router cho https://sohoc.example/api/* của khung Luyện tập (HTML nhúng trong trang Luyện tập, xem Shell/WebUi.cs).
/// Chỉ có: kết quả luyện tập, gói luyện tập, mở tài liệu, mở web. Không chứa business logic, không mở port mạng nào.
/// </summary>
public sealed partial class ApiRouter(IShellActions shell)
{
    private static string StateFile => Paths.DataFile("ket-qua.json");
    private static string PacksDir => Paths.Packs;

    public Task<ApiResponse> HandleAsync(ApiRequest r, CancellationToken ct)
    {
        try
        {
            var seg = r.Path.Trim('/').Split('/');   // ["api", ...]
            return Task.FromResult((r.Method, seg.Length > 1 ? seg[1] : "") switch
            {
                ("GET", "state") => ApiResponse.Json(JsonStore.Read(StateFile) ?? EmptyState()),
                ("PUT", "state") => PutState(r.Body),
                // Nguồn của bài học (môn + tên file như trong content/), mở đúng trang nếu là PDF.
                ("POST", "source") => Ok(Documents.Open(
                    Documents.FindSource(r.Query.GetValueOrDefault("subject"), r.Query.GetValueOrDefault("file")),
                    int.TryParse(r.Query.GetValueOrDefault("page"), out var page) ? page : 0)),
                ("GET", "packs") => ListPacks(),
                ("GET", "subjects") => Subjects(),
                ("GET", "quizzes") => Quizzes(),
                // Công tắc tự lưu quiz LMS (cùng giá trị với ô trong Cài đặt).
                ("GET", "prefs") => ApiResponse.Json(new JsonObject { ["saveQuizzes"] = Config.Bool("sources.lms.saveQuizzes", true) }),
                ("POST", "prefs") when r.Query.TryGetValue("saveQuizzes", out var on) => SetSaveQuizzes(on == "true"),
                ("POST", "lms") when seg.ElementAtOrDefault(2) == "sync" => Ok(shell.SyncLms()),
                ("POST", "packs") when seg.ElementAtOrDefault(2) == "delete" => DeletePack(r.Query.GetValueOrDefault("id", "")),
                ("POST", "packs") => ImportPack(r.Body),
                ("POST", "app") when seg.ElementAtOrDefault(2) == "open-web" =>
                    Ok(shell.OpenWeb(r.Query.GetValueOrDefault("url", ""), r.Query.GetValueOrDefault("title", ""))),
                _ => ApiResponse.Error(404, "not found"),
            });
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Chi tiết lỗi chỉ ghi log, không trả về cho trang (ASVS: không lộ thông tin nội bộ qua thông báo lỗi).
            Log.Error($"API {r.Method} {r.Path}", e);
            return Task.FromResult(ApiResponse.Error(500, "lỗi trong app, xem app.log"));
        }
    }

    /// <summary>
    /// Môn đang học kỳ này (tên + mã) từ dữ liệu đã sync: LMS (khóa kỳ hiện tại, bỏ khóa thí nghiệm/bài tập phụ)
    /// cộng kết quả đăng ký môn trên MyBK. Trang Soạn dùng để chọn môn, khỏi gõ tay mã môn.
    /// </summary>
    private static ApiResponse Subjects()
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // mã → tên
        if (LmsStore.Read() is { } lms)
            foreach (var c in lms.Courses.Where(c => c.Term == lms.Term && c.Part is null))
                if (c.Code?.Split('_')[0] is { Length: > 0 } code && c.Subject is { } name)
                    seen.TryAdd(code, name);
        foreach (var r in MybkStore.Read()?.Registered ?? [])
            if (r.Code is { Length: > 0 } code && r.Name is { } name && !seen.Values.Contains(name, StringComparer.OrdinalIgnoreCase))
                seen.TryAdd(code, name);
        return ApiResponse.Json(new JsonArray(seen.OrderBy(kv => kv.Value, StringComparer.Create(new System.Globalization.CultureInfo("vi-VN"), true))
            .Select(kv => (JsonNode)new JsonObject { ["code"] = kv.Key, ["name"] = kv.Value }).ToArray()));
    }

    [GeneratedRegex(@"<script\b[\s\S]*?</script>|<input type=""hidden""[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex Noise();

    /// <summary>
    /// Saved LMS quiz reviews (data/lms-quiz/*.json) with the course code, for the "Kho quiz" page.
    /// Older files may still contain scripts / hidden inputs (session key): stripped here before they reach the page.
    /// </summary>
    private static ApiResponse Quizzes()
    {
        var codes = new Dictionary<long, string>();
        var lms = LmsStore.Read();
        foreach (var c in lms?.Courses ?? [])
            if (c.Code?.Split('_')[0] is { } code) codes[c.Id] = code;
        // Quiz info from the last sync (close time, attempt finish time): fills files saved by older versions.
        var quizzes = (lms?.Quizzes ?? []).GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First());
        var list = new JsonArray();
        var dir = Paths.DataFile("lms-quiz");
        if (Directory.Exists(dir))
            foreach (var f in Directory.GetFiles(dir, "*.json"))
            {
                if (JsonStore.Read(f) is not JsonObject q) continue;
                var name = Path.GetFileNameWithoutExtension(f);   // <quizId>-<attemptId>
                q["file"] = name;
                if (long.TryParse(name.Split('-')[0], out var qid) && quizzes.TryGetValue(qid, out var meta))
                {
                    q["course"] ??= meta.Course;
                    q["closesAt"] ??= meta.Close;
                    if (q["finished"] is null && long.TryParse(name.Split('-').ElementAtOrDefault(1), out var aid)
                        && meta.Attempts.FirstOrDefault(a => a.Id == aid) is { } attempt)
                        q["finished"] = attempt.Finished;
                }
                if (q["course"]?.GetValue<long>() is { } cid && codes.TryGetValue(cid, out var code)) q["code"] = code;
                foreach (var x in (q["questions"] as JsonArray ?? []).OfType<JsonObject>())
                    x["html"] = Noise().Replace(x["html"]?.GetValue<string>() ?? "", "");
                list.Add(q);
            }
        return ApiResponse.Json(list);
    }

    /// <summary>Gói đã cài: content/packs/*.studypack.json (UI tự đọc file qua đường dẫn tĩnh rồi kiểm tra).</summary>
    private static ApiResponse ListPacks()
    {
        var list = new JsonArray();
        if (Directory.Exists(PacksDir))
            foreach (var f in Directory.GetFiles(PacksDir, "*.studypack.json").Order(StringComparer.OrdinalIgnoreCase))
                list.Add(new JsonObject { ["file"] = Path.GetFileName(f), ["size"] = new FileInfo(f).Length });
        return ApiResponse.Json(list);
    }

    /// <summary>
    /// Cài gói: UI đã chạy validatePack đầy đủ; ở đây chỉ chặn phần nguy hiểm cho file system (id, cỡ, format).
    /// Nội dung vẫn được lọc HTML lúc hiển thị, nên gói lạ cũng không chạy được script.
    /// </summary>
    private static ApiResponse ImportPack(string? body)
    {
        if (PackImport.Check(body, out var pack) is { } error) return ApiResponse.Error(400, error);
        return ApiResponse.Json(new JsonObject { ["ok"] = true, ["file"] = InstallPack(pack!) });
    }

    /// <summary>Ghi gói đã qua <see cref="PackImport.Check"/> vào content/packs (hoặc data/packs); trả tên file.</summary>
    public static string InstallPack(JsonObject pack)
    {
        Directory.CreateDirectory(PacksDir);
        var file = PackImport.FileName(pack["id"]!.GetValue<string>());
        JsonStore.Write(Path.Combine(PacksDir, file), pack);
        Log.Info($"Cài gói luyện tập: {file}");
        return file;
    }

    /// <summary>Đã có gói cùng id chưa (cập nhật gói thì phải qua khung Luyện tập để giữ kết quả theo fingerprint).</summary>
    public static bool PackInstalled(string id) => PackImport.Id().IsMatch(id) && File.Exists(Path.Combine(PacksDir, PackImport.FileName(id)));

    private static ApiResponse DeletePack(string id)
    {
        if (!PackImport.Id().IsMatch(id)) return ApiResponse.Error(400, "id gói không hợp lệ");
        var path = Path.Combine(PacksDir, PackImport.FileName(id));
        if (!File.Exists(path)) return ApiResponse.Error(404, "không có gói này");
        File.Delete(path);   // gói do user tự cài vào content/packs, không phải tài liệu môn học
        Log.Info($"Gỡ gói luyện tập: {id}");
        return ApiResponse.Json(new JsonObject { ["ok"] = true });
    }

    private static ApiResponse SetSaveQuizzes(bool on)
    {
        Config.Set("sources.lms.saveQuizzes", on);
        return ApiResponse.Json(new JsonObject { ["saveQuizzes"] = on });
    }

    private static ApiResponse Ok(bool ok) => ok ? ApiResponse.Json(new JsonObject { ["ok"] = true }) : ApiResponse.Error(404, "not found");

    private static JsonObject EmptyState() => new() { ["version"] = 1, ["questions"] = new JsonObject(), ["lessons"] = new JsonObject(), ["exams"] = new JsonArray() };

    /// <summary>Lưu kết quả luyện tập; lần ghi đầu tiên mỗi ngày thì backup một bản vào data/backup.</summary>
    private static ApiResponse PutState(string? body)
    {
        if ((body?.Length ?? 0) > 10_000_000) return ApiResponse.Error(413, "kết quả luyện tập quá lớn");
        if (JsonNode.Parse(body ?? "") is not JsonObject state || state["questions"] is not JsonObject) return ApiResponse.Error(400, "sai định dạng");
        var backup = Paths.DataFile(Path.Combine("backup", $"ket-qua-{DateTime.Now:yyyy-MM-dd}.json"));
        if (File.Exists(StateFile) && !File.Exists(backup))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            File.Copy(StateFile, backup);
        }
        state["updatedAt"] = DateTime.Now.ToString("s");
        JsonStore.Write(StateFile, state);
        return ApiResponse.Json(new JsonObject { ["ok"] = true, ["updatedAt"] = state["updatedAt"]!.DeepClone() });
    }
}
