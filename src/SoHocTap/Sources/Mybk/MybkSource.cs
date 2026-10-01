using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;

namespace SoHocTap.Sources.Mybk;

/// <summary>
/// Source MyBK, phần Thông tin sinh viên (DESIGN.md mục 8b).
/// Flow: SSO → /my/homeSSO.action → /app/login?type=cas → /app/ (Bearer token nằm trong #hid_Token).
/// Mọi request đều chạy trong trang MyBK qua <see cref="IBrowserRunner"/>. Chỉ gọi API đọc (sources.mybk.api).
/// Không lưu CCCD, địa chỉ, điện thoại, ngày sinh, email cá nhân.
/// </summary>
public sealed partial class MybkSource(Func<IBrowserRunner?> runner) : ISource
{
    public string Name => "mybk";
    public TimeSpan Interval => TimeSpan.FromHours(Config.Int("sources.mybk.syncHours", 12));

    private static string DataFile => Paths.DataFile("mybk.json");
    private static string SessionFile => Paths.DataFile(Path.Combine("secrets", "mybk-session.json"));

    /// <summary>
    /// Login HCMUT xong (flow đã đi qua MyBK) thì true, logout thì false. Chưa login lần nào và chưa có data thì
    /// không coi là đã kết nối, scheduler sẽ không mở MyBK ngầm (đỡ spam request lên server trường).
    /// </summary>
    public static void SetSignedIn(bool on) =>
        JsonStore.Write(SessionFile, new JsonObject { ["signedIn"] = on, ["at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

    public SourceStatus Status()
    {
        var d = JsonStore.Read(DataFile);
        var signedIn = JsonStore.Read(SessionFile)?["signedIn"]?.GetValue<bool>() ?? d is not null;
        return new SourceStatus(runner() is not null && signedIn, d?["syncedAt"]?.GetValue<long>(), d?["student"]?["name"]?.GetValue<string>());
    }

    public JsonNode? Data() => JsonStore.Read(DataFile);

    public async Task SyncAsync(Action<string> log, bool force, CancellationToken ct)
    {
        var browser = runner() ?? throw new InvalidOperationException("Cần mở app để đồng bộ MyBK.");
        var api = Config.Node("sources.mybk.api") as JsonObject ?? throw new InvalidOperationException("Thiếu sources.mybk.api trong cấu hình.");

        var first = await RunAsync(browser, api, ["info", "semesters"], new(), log, ct);
        var info = first.GetValueOrDefault("info") as JsonObject;
        if (info?["id"] is null) throw new InvalidOperationException("Không đọc được thông tin sinh viên từ MyBK.");
        var sems = first.GetValueOrDefault("semesters") as JsonArray ?? [];
        var cur = sems.OfType<JsonObject>().FirstOrDefault(s => s["isCurrent"]?.GetValue<bool>() == true)
                  ?? sems.OfType<JsonObject>().MaxBy(s => s["code"]?.GetValue<long>() ?? 0);
        var sem = cur?["code"]?.ToString() ?? "";
        var args = new Dictionary<string, string>
        {
            ["id"] = info["id"]!.ToString(),
            ["mssv"] = info["code"]?.GetValue<string>() ?? "",
            ["sem"] = sem,
            ["year"] = sem.Length >= 4 ? sem[..4] : "",
            ["term"] = sem.Length > 4 ? sem[4..] : "",
        };
        var got = await RunAsync(browser, api, ["schedule", "exams", "gradesCourses", "gradesTerms", "curriculumInfo", "curriculum"], args, log, ct);
        // Mấy API đọc dò ra từ source trang MyBK (01/10/2026): điểm thành phần, quyết định học vụ, ngày CTXH, khoản phí. Lỗi thì bỏ qua.
        var extra = await RunAsync(browser, api, ["components", "decisions", "socialWork", "fees", "registered"], args, log, ct);

        var output = new JsonObject
        {
            ["syncedAt"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            // Chỉ giữ thông tin cần hiển thị.
            ["student"] = new JsonObject
            {
                ["name"] = $"{info["lastName"]} {info["firstName"]}".Trim(),
                ["mssv"] = info["code"]?.GetValue<string>(),
                ["class"] = info["classCode"]?.GetValue<string>(),
            },
            ["term"] = new JsonObject { ["code"] = sem, ["name"] = cur?["nameVi"]?.GetValue<string>() ?? "" },
            ["schedule"] = NormSchedule(got.GetValueOrDefault("schedule") as JsonArray),
            ["exams"] = NormExams(got.GetValueOrDefault("exams")),
            ["gradeTerms"] = NormGradeTerms(got.GetValueOrDefault("gradesTerms") as JsonArray),
            ["grades"] = NormGrades(got.GetValueOrDefault("gradesCourses") as JsonArray),
            ["curriculum"] = NormCurriculum(got.GetValueOrDefault("curriculumInfo") as JsonObject, got.GetValueOrDefault("curriculum") as JsonArray),
            ["components"] = NormComponents(extra.GetValueOrDefault("components") as JsonArray),
            ["decisions"] = NormDecisions(extra.GetValueOrDefault("decisions") as JsonArray),
            ["socialWork"] = NormSocialWork(extra.GetValueOrDefault("socialWork") as JsonObject),
            ["fees"] = NormFees(extra.GetValueOrDefault("fees") as JsonArray),
            ["registered"] = NormRegistered(extra.GetValueOrDefault("registered") as JsonArray, sem),
        };
        // Đăng ký môn là trang HTML riêng nên để cuối cùng (WebView ẩn phải rời /app).
        try { output["registration"] = ParseRegistration(await browser.PageAsync(Config.Str("sources.mybk.registration"), ct)); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log($"  đăng ký môn: {e.Message}");
            output["registration"] = JsonStore.Read(DataFile)?["registration"]?.DeepClone() ?? new JsonArray();
        }
        JsonStore.Write(DataFile, output);
        log($"Xong: {output["schedule"]!.AsArray().Count} buổi học, {output["exams"]!.AsArray().Count} lịch thi, {output["grades"]!.AsArray().Count} môn có điểm");
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
                log($"  {n}: lỗi {r?.Status}");
                if (r is { Status: 401 or 403 } || r is { HasToken: false }) throw new InvalidOperationException("Phiên MyBK hết hạn, cần đăng nhập HCMUT lại.");
                continue;
            }
            var body = JsonNode.Parse(r.Body) as JsonObject;
            if (body?["code"]?.ToString() is not ("200" or "204")) { log($"  {n}: mã {body?["code"]} {body?["msg"]}"); continue; }
            output[n] = body["data"]?.DeepClone();
        }
        return output;
    }

    // ------------------------------------------------------------------ normalize

    [GeneratedRegex("<[^>]+>")] private static partial Regex TagRx();
    private static string Clean(JsonNode? n) => WebUtility.HtmlDecode(TagRx().Replace(n?.GetValue<string>() ?? "", "")).Trim();
    private static JsonNode? V(JsonNode? n) => n?.DeepClone();

    private static JsonArray NormSchedule(JsonArray? rows) => new((rows ?? []).OfType<JsonObject>().Select(r =>
    {
        var subj = r["subject"] as JsonObject; var room = r["room"] as JsonObject; var emp = r["employee"] as JsonObject;
        var weeks = (r["weekSeriesDisplay"]?.GetValue<string>() ?? "").Split('|').Where(w => int.TryParse(w, out _)).Select(w => (JsonNode)int.Parse(w)).ToArray();
        return (JsonNode)new JsonObject
        {
            ["code"] = V(subj?["code"]),
            ["name"] = V(subj?["nameVi"]),
            ["credits"] = V(subj?["numOfCredits"]),
            ["day"] = V(r["dayOfWeek"]),
            ["start"] = V(r["startTime"]),
            ["end"] = V(r["endTime"]),
            ["lesson"] = V(r["startLesson"]),
            ["lessons"] = V(r["numOfLesson"]),
            ["room"] = V(room?["code"]),
            ["campus"] = V(room?["building"]?["campus"]?["code"]),
            ["teacher"] = $"{emp?["lastName"]} {emp?["firstName"]}".Trim(),
            ["year"] = V(r["calendarYear"]),
            ["weeks"] = new JsonArray(weeks),
        };
    }).OrderBy(x => x["day"]?.GetValue<int>() ?? 9).ThenBy(x => x["start"]?.GetValue<string>()).ToArray());

    private static JsonArray NormExams(JsonNode? d)
    {
        var rows = d is JsonObject o ? o["data"] as JsonArray : d as JsonArray;
        return new((rows ?? []).OfType<JsonObject>().Select(r => (JsonNode)new JsonObject
        {
            ["code"] = V(r["MAMONHOC"]),
            ["name"] = V(r["TENMONHOC"]),
            ["type"] = V(r["LOAITHI"]),
            ["date"] = V(r["NGAYTHI"]),
            ["time"] = V(r["GIOBD"]),
            ["minutes"] = int.TryParse(r["GIO_SOPHUT"]?.ToString(), out var min) ? min : null,
            ["room"] = V(r["MAPHONG"]),
            ["campus"] = V(r["MACOSO"]),
            ["group"] = V(r["NHOMLOP"]),
        }).OrderBy(x => x["date"]?.ToString()).ThenBy(x => x["time"]?.ToString()).ToArray());
    }

    private static JsonArray NormGradeTerms(JsonArray? rows) => new((rows ?? []).OfType<JsonObject>()
        .OrderByDescending(r => r["maNamHocHocKy"]?.ToString())
        .Select(r => (JsonNode)new JsonObject
        {
            ["code"] = V(r["maNamHocHocKy"]),
            ["name"] = V(r["namHocHocKyTen"]),
            ["gpaTerm"] = V(r["diemTBTLHocKy"]),
            ["gpaAll"] = V(r["diemTBTLChung"]),
            ["creditsTerm"] = V(r["soTCTLHocKy"]),
            ["creditsAll"] = V(r["soTCTLChung"]),
            ["updated"] = V(r["capNhatCuoi"]),
        }).ToArray());

    // diemDat / diemdat của MyBK: 1 đạt · 0 chưa đạt · -1 không tính (chứng chỉ, môn chuyển điểm).
    private static int Result(JsonNode? n) => int.TryParse(n?.ToString(), out var v) ? v : 0;

    /// <summary>Điểm số > 10 là mã đặc biệt (MT miễn thi, VT vắng thi, RT rút môn, DT đạt chứng chỉ…), bảng mã ở sources.mybk.specialScores.</summary>
    private static string? Special(JsonNode? score)
    {
        if (!double.TryParse(score?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var v) || v <= 10) return null;
        // Mã chưa rõ nghĩa (vd. 11) thì để nguyên mã, không đoán.
        var code = ((int)v).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Config.Map("sources.mybk.specialScores").GetValueOrDefault(code, "mã " + code);
    }

    private static JsonArray NormGrades(JsonArray? rows) => new((rows ?? []).OfType<JsonObject>()
        .OrderByDescending(r => r["maHocKy"]?.ToString())
        .Select(r => (JsonNode)new JsonObject
        {
            ["term"] = V(r["maHocKy"]),
            ["code"] = V(r["maMonHoc"]),
            ["name"] = V(r["tenMonHoc"]),
            ["credits"] = V(r["soTinChi"]),
            ["score"] = Special(r["diemSo"]) is null ? V(r["diemSo"]) : null,
            ["letter"] = V(r["diemChu"]),
            ["special"] = Special(r["diemSo"]),
            ["result"] = Result(r["diemDat"]),
            ["passed"] = Result(r["diemDat"]) == 1,
            ["group"] = V(r["nhomTo"]),
            ["note"] = Clean(r["ghiChu"]),
            ["termId"] = V(r["namHocHocKyId"]),
            ["courseId"] = V(r["monHocId"]),   // để ghép điểm thành phần
        }).ToArray());

    /// <summary>Điểm thành phần chính thức (MyBK): mỗi môn mỗi kỳ, các column điểm kèm tỉ lệ. Mã điểm đặc biệt (VT, MT…) đổi thành chữ.
    /// Bỏ column tổng kết (tket, tkethp) vì điểm tổng đã có trong bảng điểm.</summary>
    private static JsonArray NormComponents(JsonArray? rows) => new((rows ?? []).OfType<JsonObject>().Select(r =>
    {
        JsonArray cols = [];
        try { cols = JsonNode.Parse(r["diemThanhPhanJson"]?.GetValue<string>() ?? "[]") as JsonArray ?? []; } catch (JsonException) { }
        return (JsonNode)new JsonObject
        {
            ["termId"] = V(r["namHocHocKyId"]),
            ["courseId"] = V(r["monHocId"]),
            ["status"] = V(r["tenTinhTrang"]),
            ["items"] = new JsonArray(cols.OfType<JsonObject>().Where(c => c["ma"]?.GetValue<string>() is not ("tket" or "tkethp"))
                .Select(c => (JsonNode)new JsonObject
                {
                    ["code"] = V(c["ma"]),
                    ["name"] = V(c["ten"]),
                    ["weight"] = V(c["tyle"]),
                    ["score"] = Special(c["diem"]) is null ? V(c["diem"]) : null,
                    ["special"] = Special(c["diem"]),
                }).ToArray()),
        };
    }).ToArray());

    /// <summary>Quyết định học vụ (vào/ra, cảnh cáo, thôi học…): chỉ loại, kỳ, lý do, tình trạng, ngày.</summary>
    private static JsonArray NormDecisions(JsonArray? rows) => new((rows ?? []).OfType<JsonObject>().Select(r => (JsonNode)new JsonObject
    {
        ["type"] = V(r["type"]),
        ["term"] = V(r["yearSemesterName"]),
        ["reason"] = V(r["decisionReason"]),
        ["status"] = V(r["status"]),
        ["date"] = V(r["decisionDate"]),
    }).ToArray());

    /// <summary>Ngày công tác xã hội: tổng đã quy đổi và từng hoạt động (tên, ngày, số ngày).</summary>
    private static JsonObject NormSocialWork(JsonObject? o) => new()
    {
        ["days"] = V(o?["numOfSocialWorkDays"]),
        ["activities"] = new JsonArray((o?["participationActivities"] as JsonArray ?? []).OfType<JsonObject>().Select(a => (JsonNode)new JsonObject
        {
            ["name"] = V(a["activityName"]),
            ["date"] = V(a["activityStartDate"]),
            ["days"] = V(a["numberOfConversionDays"]),
            ["confirmed"] = V(a["isConfirmed"]),
            ["canceled"] = V(a["isCanceled"]),
        }).ToArray()),
    };

    /// <summary>
    /// Kết quả đăng ký môn của kỳ hiện tại. API trả về row của mọi đợt (HK261_D1, _D2, _KQ…) nên chỉ lấy đợt có mã bắt đầu bằng
    /// "HK" + 3 số cuối của mã kỳ (20261 → HK261), mỗi môn giữ row của đợt mới nhất (semesterId lớn nhất).
    /// </summary>
    private static JsonArray NormRegistered(JsonArray? rows, string sem)
    {
        if (sem.Length < 3) return [];
        var prefix = "HK" + sem[^3..];
        return new JsonArray((rows ?? []).OfType<JsonObject>()
            .Where(r => r["semesterCode"]?.GetValue<string>()?.StartsWith(prefix, StringComparison.Ordinal) == true)
            .GroupBy(r => r["subjectCode"]?.GetValue<string>() ?? "")
            .Select(g => g.MaxBy(r => r["semesterId"]?.GetValue<int>() ?? 0)!)
            .OrderBy(r => r["subjectCode"]?.GetValue<string>())
            .Select(r => (JsonNode)new JsonObject
            {
                ["code"] = V(r["subjectCode"]),
                ["name"] = V(r["subjectName"]),
                ["classGroup"] = V(r["subjClassGrpCode"]),
                ["theory"] = V(r["theoryGrpCode"]),
                ["lab"] = V(r["exerciseGrpCode"]),
                ["round"] = V(r["semesterCode"]),
                ["result"] = Clean(r["result"]),
                ["confirmed"] = V(r["isConfirmed"]),
            }).ToArray());
    }

    /// <summary>Khoản phí: chỉ giữ khoản còn phải đóng (chưa hủy, còn nợ), gồm nội dung, số tiền, hạn.</summary>
    private static JsonArray NormFees(JsonArray? rows) => new((rows ?? []).OfType<JsonObject>()
        .Where(r => (r["isCancelled"]?.GetValue<int>() ?? 0) == 0 && (r["amtRemaining"]?.GetValue<long>() ?? 0) > 0)
        .Select(r => (JsonNode)new JsonObject
        {
            ["content"] = V(r["shortContent"]),
            ["amount"] = V(r["amtToBePaid"]),
            ["remaining"] = V(r["amtRemaining"]),
            ["due"] = V(r["expectedExpirationDate"]),
        }).ToArray());

    /// <summary>
    /// Chương trình đào tạo. Mỗi row API là một môn nhưng kèm luôn số liệu của KHỐI (tcyeucau, tcdat, mhdat, hoanthanh),
    /// nên tách ra blocks[] và courses[]. Tiến độ khối tính theo tín chỉ (mhyeucau luôn là 0). Trạng thái môn do UI tự suy
    /// từ result/attempted, ghép với bảng điểm (CTĐT có thể cũ hơn) và thời khóa biểu (môn đang học).
    /// </summary>
    private static JsonObject NormCurriculum(JsonObject? info, JsonArray? rows)
    {
        var list = (rows ?? []).OfType<JsonObject>().OrderBy(r => r["stt"]?.GetValue<int>() ?? 0).ToList();
        var (done, need) = Fraction(info?["sotctl"]);
        var blocks = list.GroupBy(r => r["khoikienthucid"]?.ToString() ?? "").Select(g =>
        {
            var r = g.First();
            return (JsonNode)new JsonObject
            {
                ["id"] = g.Key,
                ["name"] = Clean(r["tenkhoikienthuc"]),
                ["group"] = V(r["loainhomkhoikienthuc"]),
                ["required"] = r["khoikienthucbatbuoc"]?.ToString() == "1",
                ["creditsNeed"] = V(r["tcyeucau"]),
                ["creditsDone"] = V(r["tcdat"]),
                ["passedCount"] = V(r["mhdat"]),
                ["complete"] = r["hoanthanh"]?.ToString() == "1",
                ["order"] = V(r["stt"]),
            };
        }).ToArray();
        return new JsonObject
        {
            ["program"] = V(info?["tenctdt"]),
            ["faculty"] = V(info?["tenkhoa"]),
            ["year"] = V(info?["namapdung"]),
            ["code"] = V(info?["mactdt"]),
            ["creditsDone"] = done,
            ["creditsNeed"] = need,
            ["gpa10"] = Number(info?["tbtlhe10"]),
            ["gpa4"] = Number(info?["tbtlhe4"]),
            ["updated"] = V(info?["capnhatcuoi"]),
            ["blocks"] = new JsonArray(blocks),
            ["courses"] = new JsonArray(list.Select(r => (JsonNode)new JsonObject
            {
                ["block"] = r["khoikienthucid"]?.ToString(),
                ["code"] = V(r["mamonhoc"]),
                ["name"] = V(r["tenmonhoc"]),
                ["credits"] = V(r["sotc"]),
                ["score"] = Special(r["diemso"]) is null && r["hieuluc"]?.ToString() == "1" ? V(r["diemso"]) : null,
                ["letter"] = r["diemchu"]?.ToString() is { } l && l != "--" ? l : null,
                ["special"] = Special(r["diemso"]),
                ["result"] = Result(r["diemdat"]),
                ["attempted"] = r["hieuluc"]?.ToString() == "1",
                ["provisional"] = r["tamdat"]?.ToString() == "1",
                ["equiv"] = Clean(r["ghichu"]),
            }).ToArray()),
        };
    }

    /// <summary>"50/132" → (50, 132).</summary>
    private static (int? Done, int? Need) Fraction(JsonNode? n)
    {
        var parts = (n?.ToString() ?? "").Split('/');
        return (int.TryParse(parts.ElementAtOrDefault(0), out var a) ? a : null, int.TryParse(parts.ElementAtOrDefault(1), out var b) ? b : null);
    }

    /// <summary>"4.11/10" → 4.11; "--" → null.</summary>
    private static double? Number(JsonNode? n) =>
        double.TryParse((n?.ToString() ?? "").Split('/')[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

    [GeneratedRegex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex RowRx();
    [GeneratedRegex(@"<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex CellRx();
    [GeneratedRegex(@"^\d{2}/\d{2}/\d{4} \d{2}:\d{2}$")] private static partial Regex TimeRx();

    /// <summary>Bảng các đợt đăng ký: STT · mã đợt · tên · bắt đầu · kết thúc.</summary>
    public static JsonArray ParseRegistration(string html)
    {
        var list = new JsonArray();
        foreach (Match row in RowRx().Matches(html ?? ""))
        {
            var cells = CellRx().Matches(row.Groups[1].Value).Select(c => WebUtility.HtmlDecode(TagRx().Replace(c.Groups[1].Value, "")).Trim()).ToList();
            var times = cells.Where(c => TimeRx().IsMatch(c)).ToList();
            if (cells.Count < 4 || times.Count < 2) continue;
            var text = cells.Where(c => !times.Contains(c) && !c.All(char.IsDigit)).ToList();
            long Ts(string s) => new DateTimeOffset(DateTime.ParseExact(s, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)).ToUnixTimeSeconds();
            list.Add(new JsonObject { ["code"] = text.ElementAtOrDefault(0) ?? "", ["name"] = text.ElementAtOrDefault(1) ?? "", ["start"] = Ts(times[0]), ["end"] = Ts(times[1]) });
        }
        return list;
    }
}
