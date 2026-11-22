using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;

namespace SoHocTap.Sources.Mybk;

public static partial class MybkNormalize
{
    [GeneratedRegex("<[^>]+>")] private static partial Regex TagRx();

    public static string Clean(JsonNode? n) => WebUtility.HtmlDecode(TagRx().Replace(Str(n) ?? "", "")).Trim();

    private static JsonNode? V(JsonNode? n) => n?.DeepClone();

    public static string? Str(JsonNode? n) => n is JsonValue v
        ? v.TryGetValue<string>(out var s) ? s : v.ToJsonString().Trim('"')
        : null;

    public static long? Long(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<long>(out var l)) return l;
        if (v.TryGetValue<double>(out var d) && double.IsFinite(d)) return (long)Math.Round(d);
        if (v.TryGetValue<bool>(out var b)) return b ? 1 : 0;
        return double.TryParse(Str(n), NumberStyles.Float, CultureInfo.InvariantCulture, out d) && double.IsFinite(d) ? (long)Math.Round(d) : null;
    }

    private static int? Int(JsonNode? n) => Long(n) is { } l && l >= int.MinValue && l <= int.MaxValue ? (int)l : null;

    public static bool KeepOldSchedule(JsonArray fresh, JsonNode? old, bool sameTerm) =>
        sameTerm && fresh.Count == 0 && old is JsonArray { Count: > 0 };

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

    public static int Result(JsonNode? n) => Int(n) ?? 0;

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
            ["courseId"] = V(r["monHocId"]),
        }).ToArray());

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

    public static JsonArray Decisions(JsonArray? rows) => new((rows ?? []).OfType<JsonObject>().Select(r => (JsonNode)new JsonObject
    {
        ["type"] = V(r["type"]),
        ["term"] = V(r["yearSemesterName"]),
        ["reason"] = V(r["decisionReason"]),
        ["status"] = V(r["status"]),
        ["date"] = V(r["decisionDate"]),
    }).ToArray());

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

    public static JsonArray Fees(JsonArray? rows) => new((rows ?? []).OfType<JsonObject>()
        .Where(r => (Long(r["isCancelled"]) ?? 0) == 0)
        .Select(r => (JsonNode)new JsonObject
        {
            ["content"] = V(r["shortContent"]),
            ["amount"] = V(r["amtToBePaid"]),
            ["remaining"] = V(r["amtRemaining"]),
            ["due"] = V(r["expectedExpirationDate"]),
        }).ToArray());

    public static bool CurriculumReady(JsonArray? rows) =>
        rows is [JsonObject first, ..] && (first["msg"] is not JsonValue m || m.ToString() == "done");

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

    public static (int? Done, int? Need) Fraction(JsonNode? n)
    {
        var parts = (n?.ToString() ?? "").Split('/');
        return (int.TryParse(parts.ElementAtOrDefault(0), NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : null,
            int.TryParse(parts.ElementAtOrDefault(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : null);
    }

    public static double? Number(JsonNode? n) =>
        double.TryParse((n?.ToString() ?? "").Split('/')[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    [GeneratedRegex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex RowRx();
    [GeneratedRegex(@"<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex CellRx();
    [GeneratedRegex(@"^\d{2}/\d{2}/\d{4} \d{2}:\d{2}$")] private static partial Regex TimeRx();

    public static JsonArray ParseRegistration(string? html)
    {
        var list = new JsonArray();
        foreach (Match row in RowRx().Matches(html ?? ""))
        {
            var cells = CellRx().Matches(row.Groups[1].Value).Select(c => WebUtility.HtmlDecode(TagRx().Replace(c.Groups[1].Value, "")).Trim()).ToList();
            var times = cells.Where(c => TimeRx().IsMatch(c)).ToList();
            if (cells.Count < 4 || times.Count < 2) continue;
            var text = cells.Where(c => !times.Contains(c) && !c.All(char.IsDigit)).ToList();
            if (VnTime.ParseDateTime(times[0]) is not { } start || VnTime.ParseDateTime(times[1]) is not { } end) continue;
            list.Add((JsonNode)new JsonObject { ["code"] = text.ElementAtOrDefault(0) ?? "", ["name"] = text.ElementAtOrDefault(1) ?? "", ["start"] = start, ["end"] = end });
        }
        return list;
    }

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex TitleRx();

    private static readonly string[] ErrorFields = ["code", "status", "error", "message", "msg", "title"];

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
