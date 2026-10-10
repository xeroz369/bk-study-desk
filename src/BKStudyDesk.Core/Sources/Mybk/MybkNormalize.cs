using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;

namespace SoHocTap.Sources.Mybk;

/// <summary>
/// Đổi JSON thô của API MyBK (tên field tiếng Việt không dấu, lúc số lúc chữ) thành dữ liệu gọn mà app lưu ở mybk.json.
/// Toàn hàm thuần: không đọc Config, không ghi Log, nên test được bằng fixture viết tay (tests/BKStudyDesk.Logic.Tests/Fixtures).
/// Giá trị lạ (chữ ở chỗ số, null, sai kiểu) không được throw: một dòng lạ không làm hỏng cả lần đồng bộ.
/// </summary>
public static partial class MybkNormalize
{
    [GeneratedRegex("<[^>]+>")] private static partial Regex TagRx();

    /// <summary>Bỏ thẻ HTML, giải mã entity, cắt khoảng trắng.</summary>
    public static string Clean(JsonNode? n) => WebUtility.HtmlDecode(TagRx().Replace(Str(n) ?? "", "")).Trim();

    private static JsonNode? V(JsonNode? n) => n?.DeepClone();

    /// <summary>Chuỗi của một node: chữ thì lấy nguyên, số/bool thì đổi ra chữ, object/mảng thì null.</summary>
    public static string? Str(JsonNode? n) => n is JsonValue v
        ? v.TryGetValue<string>(out var s) ? s : v.ToJsonString().Trim('"')
        : null;

    /// <summary>Số nguyên của một node: nhận cả số dạng chữ ("3"), số thực thì làm tròn; không đọc được thì null.</summary>
    public static long? Long(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<long>(out var l)) return l;
        if (v.TryGetValue<double>(out var d) && double.IsFinite(d)) return (long)Math.Round(d);
        if (v.TryGetValue<bool>(out var b)) return b ? 1 : 0;
        return double.TryParse(Str(n), NumberStyles.Float, CultureInfo.InvariantCulture, out d) && double.IsFinite(d) ? (long)Math.Round(d) : null;
    }

    private static int? Int(JsonNode? n) => Long(n) is { } l && l >= int.MinValue && l <= int.MaxValue ? (int)l : null;

    // ------------------------------------------------------------------ thời khóa biểu, lịch thi

    public static JsonArray Schedule(JsonArray? rows) => new((rows ?? []).OfType<JsonObject>().Select(r =>
    {
        var subj = r["subject"] as JsonObject; var room = r["room"] as JsonObject; var emp = r["employee"] as JsonObject;
        var weeks = (Str(r["weekSeriesDisplay"]) ?? "").Split('|').Select(w => int.TryParse(w, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : (int?)null)
            .Where(w => w is not null).Select(w => (JsonNode)w!.Value).ToArray();
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
            ["teacher"] = $"{Str(emp?["lastName"])} {Str(emp?["firstName"])}".Trim(),
            ["year"] = V(r["calendarYear"]),
            ["weeks"] = new JsonArray(weeks),
        };
    }).OrderBy(x => Int(x["day"]) ?? 9).ThenBy(x => Str(x["start"]), StringComparer.Ordinal).ToArray());

    /// <summary>Lịch thi: API trả mảng, hoặc object có mảng "data" (tùy bản MyBK).</summary>
    public static JsonArray Exams(JsonNode? d)
    {
        var rows = d is JsonObject o ? o["data"] as JsonArray : d as JsonArray;
        return new((rows ?? []).OfType<JsonObject>().Select(r => (JsonNode)new JsonObject
        {
            ["code"] = V(r["MAMONHOC"]),
            ["name"] = V(r["TENMONHOC"]),
            ["type"] = V(r["LOAITHI"]),
            ["date"] = V(r["NGAYTHI"]),
            ["time"] = V(r["GIOBD"]),
            ["minutes"] = Int(r["GIO_SOPHUT"]),
            ["room"] = V(r["MAPHONG"]),
            ["campus"] = V(r["MACOSO"]),
            ["group"] = V(r["NHOMLOP"]),
        }).OrderBy(x => x["date"]?.ToString(), StringComparer.Ordinal).ThenBy(x => x["time"]?.ToString(), StringComparer.Ordinal).ToArray());
    }

    // ------------------------------------------------------------------ điểm

    public static JsonArray GradeTerms(JsonArray? rows) => new((rows ?? []).OfType<JsonObject>()
        .OrderByDescending(r => r["maNamHocHocKy"]?.ToString(), StringComparer.Ordinal)
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

    /// <summary>diemDat / diemdat của MyBK: 1 đạt, 0 chưa đạt, -1 không tính (chứng chỉ, môn chuyển điểm).</summary>
    public static int Result(JsonNode? n) => Int(n) ?? 0;

    /// <summary>
    /// Điểm số > 10 là mã đặc biệt (MT miễn thi, VT vắng thi, RT rút môn, DT đạt chứng chỉ…), bảng mã ở sources.mybk.specialScores
    /// (truyền vào <paramref name="codes"/>). Mã chưa rõ nghĩa (vd. 11) thì để nguyên mã, không đoán.
    /// </summary>
    public static string? Special(JsonNode? score, IReadOnlyDictionary<string, string> codes)
    {
        if (!double.TryParse(score?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v <= 10) return null;
        var code = ((int)v).ToString(CultureInfo.InvariantCulture);
        return codes.GetValueOrDefault(code, "mã " + code);
    }

    public static JsonArray Grades(JsonArray? rows, IReadOnlyDictionary<string, string> special) => new((rows ?? []).OfType<JsonObject>()
        .OrderByDescending(r => r["maHocKy"]?.ToString(), StringComparer.Ordinal)
        .Select(r => (JsonNode)new JsonObject
        {
            ["term"] = V(r["maHocKy"]),
            ["code"] = V(r["maMonHoc"]),
            ["name"] = V(r["tenMonHoc"]),
            ["credits"] = V(r["soTinChi"]),
            ["score"] = Special(r["diemSo"], special) is null ? V(r["diemSo"]) : null,
            ["letter"] = V(r["diemChu"]),
            ["special"] = Special(r["diemSo"], special),
            ["result"] = Result(r["diemDat"]),
            ["passed"] = Result(r["diemDat"]) == 1,
            ["group"] = V(r["nhomTo"]),
            ["note"] = Clean(r["ghiChu"]),
            ["termId"] = V(r["namHocHocKyId"]),
            ["courseId"] = V(r["monHocId"]),   // để ghép điểm thành phần
        }).ToArray());

    /// <summary>
    /// Điểm thành phần chính thức: mỗi môn mỗi kỳ, các cột điểm kèm tỉ lệ (JSON lồng trong chuỗi diemThanhPhanJson).
    /// Bỏ cột tổng kết (tket, tkethp) vì điểm tổng đã có trong bảng điểm.
    /// </summary>
    public static JsonArray Components(JsonArray? rows, IReadOnlyDictionary<string, string> special) => new((rows ?? []).OfType<JsonObject>().Select(r =>
    {
        JsonArray cols = [];
        try { cols = JsonNode.Parse(Str(r["diemThanhPhanJson"]) ?? "[]") as JsonArray ?? []; } catch (JsonException) { }
        return (JsonNode)new JsonObject
        {
            ["termId"] = V(r["namHocHocKyId"]),
            ["courseId"] = V(r["monHocId"]),
            ["status"] = V(r["tenTinhTrang"]),
            ["items"] = new JsonArray(cols.OfType<JsonObject>().Where(c => Str(c["ma"]) is not ("tket" or "tkethp"))
                .Select(c => (JsonNode)new JsonObject
                {
                    ["code"] = V(c["ma"]),
                    ["name"] = V(c["ten"]),
                    ["weight"] = V(c["tyle"]),
                    ["score"] = Special(c["diem"], special) is null ? V(c["diem"]) : null,
                    ["special"] = Special(c["diem"], special),
                }).ToArray()),
        };
    }).ToArray());

    // ------------------------------------------------------------------ học vụ, CTXH, phí, đăng ký

    /// <summary>Quyết định học vụ (vào/ra, cảnh cáo, thôi học…): chỉ loại, kỳ, lý do, tình trạng, ngày.</summary>
    public static JsonArray Decisions(JsonArray? rows) => new((rows ?? []).OfType<JsonObject>().Select(r => (JsonNode)new JsonObject
    {
        ["type"] = V(r["type"]),
        ["term"] = V(r["yearSemesterName"]),
        ["reason"] = V(r["decisionReason"]),
        ["status"] = V(r["status"]),
        ["date"] = V(r["decisionDate"]),
    }).ToArray());

    /// <summary>Ngày công tác xã hội: tổng đã quy đổi và từng hoạt động (tên, ngày, số ngày).</summary>
    public static JsonObject SocialWork(JsonObject? o) => new()
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
    public static JsonArray Registered(JsonArray? rows, string sem)
    {
        if (sem.Length < 3) return [];
        var prefix = "HK" + sem[^3..];
        return new JsonArray((rows ?? []).OfType<JsonObject>()
            .Where(r => Str(r["semesterCode"])?.StartsWith(prefix, StringComparison.Ordinal) == true)
            .GroupBy(r => Str(r["subjectCode"]) ?? "")
            .Select(g => g.MaxBy(r => Long(r["semesterId"]) ?? 0)!)
            .OrderBy(r => Str(r["subjectCode"]), StringComparer.Ordinal)
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
    public static JsonArray Fees(JsonArray? rows) => new((rows ?? []).OfType<JsonObject>()
        .Where(r => (Long(r["isCancelled"]) ?? 0) == 0 && (Long(r["amtRemaining"]) ?? 0) > 0)
        .Select(r => (JsonNode)new JsonObject
        {
            ["content"] = V(r["shortContent"]),
            ["amount"] = V(r["amtToBePaid"]),
            ["remaining"] = V(r["amtRemaining"]),
            ["due"] = V(r["expectedExpirationDate"]),
        }).ToArray());

    // ------------------------------------------------------------------ chương trình đào tạo

    /// <summary>
    /// Chương trình đào tạo. Mỗi row API là một môn nhưng kèm luôn số liệu của KHỐI (tcyeucau, tcdat, mhdat, hoanthanh),
    /// nên tách ra blocks[] và courses[]. Tiến độ khối tính theo tín chỉ (mhyeucau luôn là 0). Trạng thái môn do UI tự suy
    /// từ result/attempted, ghép với bảng điểm (CTĐT có thể cũ hơn) và thời khóa biểu (môn đang học).
    /// <paramref name="info"/> null (API phần đầu lỗi hay trả rỗng): phần đầu lấy của <paramref name="previous"/> (bản đã lưu), danh sách vẫn mới.
    /// </summary>
    public static JsonObject Curriculum(JsonObject? info, JsonArray? rows, IReadOnlyDictionary<string, string> special, JsonObject? previous = null)
    {
        var list = (rows ?? []).OfType<JsonObject>().OrderBy(r => Long(r["stt"]) ?? 0).ToList();
        var (done, need) = Fraction(info?["sotctl"]);
        JsonNode? Head(string key, JsonNode? fresh) => info is null ? previous?[key]?.DeepClone() : fresh;
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
            ["program"] = Head("program", V(info?["tenctdt"])),
            ["faculty"] = Head("faculty", V(info?["tenkhoa"])),
            ["year"] = Head("year", V(info?["namapdung"])),
            ["code"] = Head("code", V(info?["mactdt"])),
            ["creditsDone"] = Head("creditsDone", done),
            ["creditsNeed"] = Head("creditsNeed", need),
            ["gpa10"] = Head("gpa10", Number(info?["tbtlhe10"])),
            ["gpa4"] = Head("gpa4", Number(info?["tbtlhe4"])),
            ["updated"] = Head("updated", V(info?["capnhatcuoi"])),
            ["blocks"] = new JsonArray(blocks),
            ["courses"] = new JsonArray(list.Select(r => (JsonNode)new JsonObject
            {
                ["block"] = r["khoikienthucid"]?.ToString(),
                ["code"] = V(r["mamonhoc"]),
                ["name"] = V(r["tenmonhoc"]),
                ["credits"] = V(r["sotc"]),
                ["score"] = Special(r["diemso"], special) is null && r["hieuluc"]?.ToString() == "1" ? V(r["diemso"]) : null,
                ["letter"] = r["diemchu"]?.ToString() is { } l && l != "--" ? l : null,
                ["special"] = Special(r["diemso"], special),
                ["result"] = Result(r["diemdat"]),
                ["attempted"] = r["hieuluc"]?.ToString() == "1",
                ["provisional"] = r["tamdat"]?.ToString() == "1",
                ["equiv"] = Clean(r["ghichu"]),
            }).ToArray()),
        };
    }

    /// <summary>"50/132" → (50, 132).</summary>
    public static (int? Done, int? Need) Fraction(JsonNode? n)
    {
        var parts = (n?.ToString() ?? "").Split('/');
        return (int.TryParse(parts.ElementAtOrDefault(0), NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : null,
            int.TryParse(parts.ElementAtOrDefault(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : null);
    }

    /// <summary>"4.11/10" → 4.11; "--" → null.</summary>
    public static double? Number(JsonNode? n) =>
        double.TryParse((n?.ToString() ?? "").Split('/')[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    // ------------------------------------------------------------------ trang đăng ký môn (HTML)

    [GeneratedRegex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex RowRx();
    [GeneratedRegex(@"<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex CellRx();
    [GeneratedRegex(@"^\d{2}/\d{2}/\d{4} \d{2}:\d{2}$")] private static partial Regex TimeRx();

    /// <summary>Bảng các đợt đăng ký: STT, mã đợt, tên, bắt đầu, kết thúc (giờ VN).</summary>
    public static JsonArray ParseRegistration(string? html)
    {
        var list = new JsonArray();
        foreach (Match row in RowRx().Matches(html ?? ""))
        {
            var cells = CellRx().Matches(row.Groups[1].Value).Select(c => WebUtility.HtmlDecode(TagRx().Replace(c.Groups[1].Value, "")).Trim()).ToList();
            var times = cells.Where(c => TimeRx().IsMatch(c)).ToList();
            if (cells.Count < 4 || times.Count < 2) continue;
            var text = cells.Where(c => !times.Contains(c) && !c.All(char.IsDigit)).ToList();
            // Giờ trên trang là giờ VN: đổi bằng offset cố định, không theo múi giờ của máy. Ngày sai (31/02) thì bỏ dòng, không throw.
            if (VnTime.ParseDateTime(times[0]) is not { } start || VnTime.ParseDateTime(times[1]) is not { } end) continue;
            list.Add(new JsonObject { ["code"] = text.ElementAtOrDefault(0) ?? "", ["name"] = text.ElementAtOrDefault(1) ?? "", ["start"] = start, ["end"] = end });
        }
        return list;
    }

    // ------------------------------------------------------------------ báo lỗi

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex TitleRx();

    private static readonly string[] ErrorFields = ["code", "status", "error", "message", "msg", "title"];

    /// <summary>
    /// Tóm tắt body của một response lỗi để ghi log mà không chép dữ liệu cá nhân: JSON thì chỉ lấy các field báo lỗi
    /// (code, status, error, message, msg, title), HTML thì lấy &lt;title&gt;, còn lại chỉ ghi độ dài.
    /// </summary>
    public static string ErrorSummary(string? body)
    {
        var b = (body ?? "").Trim();
        if (b.Length == 0) return "body rỗng";
        static string Cut(string s) => (s.Length > 120 ? s[..120] : s).ReplaceLineEndings(" ");
        if (b[0] is '{' or '[')
        {
            try
            {
                if (JsonNode.Parse(b) is JsonObject o)
                {
                    var parts = ErrorFields.Where(k => o[k] is JsonValue).Select(k => $"{k}={Cut(o[k]!.ToString())}").ToList();
                    return parts.Count > 0 ? $"JSON {string.Join(", ", parts)}" : $"JSON không có field báo lỗi, {b.Length} ký tự";
                }
                return $"JSON mảng, {b.Length} ký tự";
            }
            catch (JsonException) { return $"JSON hỏng, {b.Length} ký tự"; }
        }
        if (b[0] == '<')
        {
            var t = TitleRx().Match(b);
            return t.Success ? $"HTML \"{Cut(WebUtility.HtmlDecode(t.Groups[1].Value).Trim())}\", {b.Length} ký tự" : $"HTML không có title, {b.Length} ký tự";
        }
        return $"text {b.Length} ký tự";
    }
}
