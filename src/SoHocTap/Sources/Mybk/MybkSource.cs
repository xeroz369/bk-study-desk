using System.Text.Json;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Data;

namespace SoHocTap.Sources.Mybk;

/// <summary>
/// Source MyBK, phần Thông tin sinh viên (DESIGN.md mục 8b).
/// Flow: SSO → /my/homeSSO.action → /app/login?type=cas → /app/ (Bearer token nằm trong #hid_Token).
/// Mọi request đều chạy trong trang MyBK qua <see cref="IBrowserRunner"/>. Chỉ gọi API đọc (sources.mybk.api).
/// Không lưu CCCD, địa chỉ, điện thoại, ngày sinh, email cá nhân.
/// </summary>
public sealed class MybkSource(Func<IBrowserRunner?> runner) : ISource
{
    public string Name => "mybk";
    public TimeSpan Interval => TimeSpan.FromHours(Config.Int("sources.mybk.syncHours", 12));

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
        return new SourceStatus(runner() is not null && signedIn, d is { SyncedAt: > 0 } ? d.SyncedAt : null, d?.Student.Name);
    }

    public JsonNode? Data() => JsonStore.Read(DataFile);

    public async Task SyncAsync(Action<string> log, bool force, CancellationToken ct)
    {
        var browser = runner() ?? throw new InvalidOperationException("Cần mở app để đồng bộ MyBK.");
        var api = Config.Node("sources.mybk.api") as JsonObject ?? throw new InvalidOperationException("Thiếu sources.mybk.api trong cấu hình.");
        // Bản đã lưu: phần nào lần này không đọc được thì giữ phần của lần trước, không ghi đè bằng danh sách rỗng.
        var prev = JsonStore.Read(DataFile) as JsonObject;
        var special = Config.Map("sources.mybk.specialScores");

        log(SyncSignal.Step("sync.mybk.open", 0, 4));
        var first = await RunAsync(browser, api, ["info", "semesters"], new(), log, ct);
        var info = first.GetValueOrDefault("info") as JsonObject;
        if (info?["id"] is null) throw new SyncException(SyncErrorKind.Data, "Không đọc được thông tin sinh viên từ MyBK.");
        var sems = first.GetValueOrDefault("semesters") as JsonArray ?? [];
        var cur = sems.OfType<JsonObject>().FirstOrDefault(s => s["isCurrent"] is JsonValue v && v.TryGetValue<bool>(out var b) && b)
                  ?? sems.OfType<JsonObject>().MaxBy(s => MybkNormalize.Long(s["code"]) ?? 0);
        var sem = cur?["code"]?.ToString() ?? "";
        var args = new Dictionary<string, string>
        {
            ["id"] = info["id"]!.ToString(),
            ["mssv"] = MybkNormalize.Str(info["code"]) ?? "",
            ["sem"] = sem,
            ["year"] = sem.Length >= 4 ? sem[..4] : "",
            ["term"] = sem.Length > 4 ? sem[4..] : "",
        };
        Dictionary<string, JsonNode?> got = [], extra = [];
        try
        {
            log(SyncSignal.Step("sync.mybk.study", 1, 4));
            got = await RunAsync(browser, api, ["schedule", "exams", "gradesCourses", "gradesTerms", "curriculumInfo", "curriculum"], args, log, ct);
            // Mấy API đọc dò ra từ source trang MyBK (01/10/2026): điểm thành phần, quyết định học vụ, ngày CTXH, khoản phí. Lỗi thì bỏ qua.
            log(SyncSignal.Step("sync.mybk.extra", 2, 4));
            extra = await RunAsync(browser, api, ["components", "decisions", "socialWork", "fees", "registered"], args, log, ct);
        }
        catch (Exception e) when (got.Count > 0 && !ct.IsCancellationRequested)
        {
            // Hết phiên hay lỗi mạng giữa chừng: phần đã đọc được vẫn lưu (phần còn lại giữ bản cũ), giữ syncedAt của lần trước
            // vì lần này chưa đồng bộ xong; lỗi vẫn báo lên như thường.
            var partial = Build(info, sem, cur, got, extra, prev, special);
            partial["syncedAt"] = prev?["syncedAt"]?.DeepClone() ?? partial["syncedAt"]!.DeepClone();
            partial["registration"] = prev?["registration"]?.DeepClone() ?? new JsonArray();
            partial["registrationAt"] = prev?["registrationAt"]?.DeepClone() ?? 0;
            MybkStore.Write(partial);
            Log.Info($"MyBK: lưu {got.Count + extra.Count} phần đã đọc trước khi lỗi ({e.GetType().Name})");
            throw;
        }

        var output = Build(info, sem, cur, got, extra, prev, special);
        // Đăng ký môn là trang HTML riêng nên để cuối cùng (WebView ẩn phải rời /app). Trang này nằm trên hệ thống đăng ký môn,
        // hay quá tải vào đợt đăng ký, nên chỉ tải lại mỗi ngày một lần; giữa chừng dùng bản đã lưu.
        var regAt = MybkNormalize.Long(prev?["registrationAt"]) ?? 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // Danh sách rỗng không giữ cả ngày: có thể là lần đọc trước bị lỗi (ví dụ rơi vào trang khác), thử lại ở lần đồng bộ sau.
        if (prev?["registration"] is JsonArray { Count: > 0 } cached && now - regAt < 86400)
        {
            output["registration"] = cached.DeepClone();
            output["registrationAt"] = regAt;
        }
        else
        {
            try
            {
                log(SyncSignal.Step("sync.mybk.registration", 3, 4));
                var html = await browser.PageAsync(Config.Str("sources.mybk.registration"), ct, "Đợt Đăng ký");
                var rounds = ParseRegistration(html);
                // Trang không có bảng đợt đăng ký (cột "Đợt Đăng ký") là đã mở nhầm trang: báo, giữ bản cũ, không lưu danh sách rỗng.
                if (rounds.Count == 0 && !html.Contains("Đợt Đăng ký", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Trang đăng ký môn không có bảng đợt đăng ký (có thể chưa vào được hệ thống đăng ký).");
                output["registration"] = rounds;
                output["registrationAt"] = now;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.Warn($"MyBK đăng ký môn: {e.Message}");
                log(SyncSignal.Warn("đợt đăng ký môn", e.Message));
                output["registration"] = prev?["registration"]?.DeepClone() ?? new JsonArray();
                output["registrationAt"] = regAt;
            }
        }
        MybkStore.Write(output);
        log($"Xong: {output["schedule"]!.AsArray().Count} buổi học, {output["exams"]!.AsArray().Count} lịch thi, {output["grades"]!.AsArray().Count} môn có điểm");
    }

    /// <summary>
    /// Dựng mybk.json từ các API đã đọc. API nào lần này lỗi (không có trong <paramref name="got"/>/<paramref name="extra"/>) thì
    /// giữ phần của lần trước: một API chập chờn không được xóa mất lịch thi hay bảng điểm đang có.
    /// </summary>
    private static JsonObject Build(JsonObject info, string sem, JsonObject? cur, Dictionary<string, JsonNode?> got, Dictionary<string, JsonNode?> extra,
        JsonObject? prev, IReadOnlyDictionary<string, string> special)
    {
        var output = new JsonObject
        {
            ["syncedAt"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            // Chỉ giữ thông tin cần hiển thị.
            ["student"] = new JsonObject
            {
                ["name"] = $"{MybkNormalize.Str(info["lastName"])} {MybkNormalize.Str(info["firstName"])}".Trim(),
                ["mssv"] = MybkNormalize.Str(info["code"]),
                ["class"] = MybkNormalize.Str(info["classCode"]),
            },
            ["term"] = new JsonObject { ["code"] = sem, ["name"] = MybkNormalize.Str(cur?["nameVi"]) ?? "" },
        };
        // Phần theo kỳ (lịch học, lịch thi, môn đã đăng ký) chỉ giữ bản cũ khi vẫn cùng kỳ; phần cộng dồn (điểm, CTĐT…) thì luôn giữ.
        var sameTerm = prev?["term"]?["code"]?.ToString() == sem;
        void Put(string key, bool ok, Func<JsonNode> norm, bool perTerm = false) =>
            output[key] = ok || prev?[key] is null || (perTerm && !sameTerm) ? norm() : prev[key]!.DeepClone();
        Put("schedule", got.ContainsKey("schedule"), () => MybkNormalize.Schedule(got.GetValueOrDefault("schedule") as JsonArray), perTerm: true);
        Put("exams", got.ContainsKey("exams"), () => MybkNormalize.Exams(got.GetValueOrDefault("exams")), perTerm: true);
        Put("gradeTerms", got.ContainsKey("gradesTerms"), () => MybkNormalize.GradeTerms(got.GetValueOrDefault("gradesTerms") as JsonArray));
        Put("grades", got.ContainsKey("gradesCourses"), () => MybkNormalize.Grades(got.GetValueOrDefault("gradesCourses") as JsonArray, special));
        Put("curriculum", got.ContainsKey("curriculumInfo") && got.ContainsKey("curriculum"),
            () => MybkNormalize.Curriculum(got.GetValueOrDefault("curriculumInfo") as JsonObject, got.GetValueOrDefault("curriculum") as JsonArray, special));
        Put("components", extra.ContainsKey("components"), () => MybkNormalize.Components(extra.GetValueOrDefault("components") as JsonArray, special));
        Put("decisions", extra.ContainsKey("decisions"), () => MybkNormalize.Decisions(extra.GetValueOrDefault("decisions") as JsonArray));
        Put("socialWork", extra.ContainsKey("socialWork"), () => MybkNormalize.SocialWork(extra.GetValueOrDefault("socialWork") as JsonObject));
        Put("fees", extra.ContainsKey("fees"), () => MybkNormalize.Fees(extra.GetValueOrDefault("fees") as JsonArray));
        Put("registered", extra.ContainsKey("registered"), () => MybkNormalize.Registered(extra.GetValueOrDefault("registered") as JsonArray, sem), perTerm: true);
        return output;
    }

    /// <summary>Gọi các API theo tên trong config, điền sẵn {id} {mssv} {sem} {year} {term}. Request POST gửi MSSV làm body.</summary>
    private static async Task<Dictionary<string, JsonNode?>> RunAsync(IBrowserRunner browser, JsonObject api, string[] names,
        Dictionary<string, string> args, Action<string> log, CancellationToken ct)
    {
        var baseUrl = Config.Str("sources.mybk.site").TrimEnd('/') + "/api/";
        string Fill(string s) => args.Aggregate(s, (acc, kv) => acc.Replace("{" + kv.Key + "}", kv.Value));
        var reqs = names.Select(n => api[n] switch
        {
            JsonValue v => new FetchRequest(n, baseUrl + Fill(v.GetValue<string>())),
            JsonObject o => new FetchRequest(n, baseUrl + Fill(o["post"]!.GetValue<string>()), "POST",
                JsonSerializer.Serialize(Fill(o["body"]?.GetValue<string>() ?? "{mssv}"))),
            _ => throw new InvalidOperationException($"Cấu hình API MyBK sai: {n}"),
        }).ToList();
        var res = await browser.FetchAsync(reqs, ct);
        var output = new Dictionary<string, JsonNode?>();
        foreach (var n in names)
        {
            if (!res.TryGetValue(n, out var r) || r.Status != 200)
            {
                if (r is { Status: 401 or 403 } || r is { HasToken: false }) throw new SessionExpiredException("Phiên MyBK hết hạn, cần đăng nhập HCMUT lại.");
                var why = r is null ? "không gửi được (MyBK đang giới hạn, đã tạm dừng)" : r.Status <= 0 ? "không kết nối được" : $"HTTP {r.Status}";
                // Ghi kèm tóm tắt body (field báo lỗi, title trang), không chép cả body: có thể chứa dữ liệu cá nhân.
                Log.Warn($"MyBK {n}: {why}{(r is null ? "" : ", " + MybkNormalize.ErrorSummary(r.Body))}");
                log(SyncSignal.Warn(Label(n), $"{n}: {why}"));
                continue;
            }
            JsonObject? body;
            try { body = JsonNode.Parse(r.Body) as JsonObject; }
            catch (JsonException) { body = null; }
            if (body?["code"]?.ToString() is not ("200" or "204"))
            {
                var why = body is null ? "trả về không phải JSON" : $"mã {body["code"]} {body["msg"]}".Trim();
                Log.Warn($"MyBK {n}: {why}");
                log(SyncSignal.Warn(Label(n), $"{n}: {why}"));
                continue;
            }
            output[n] = body["data"]?.DeepClone();
        }
        return output;
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
