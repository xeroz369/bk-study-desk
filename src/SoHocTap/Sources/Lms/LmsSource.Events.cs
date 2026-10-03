using System.Text.Json;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using SoHocTap.Files;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

// Mốc thời gian: lịch hành động, bài tập, nhóm lớp.
public sealed partial class LmsSource
{
    // ------------------------------------------------------------------ mốc thời gian, quiz

    private async Task<JsonArray> CollectEventsAsync(List<CourseInfo> courses, long uid, CancellationToken ct)
    {
        var subj = courses.ToDictionary(c => c.Id, c => c.Subject + (c.Part is null ? "" : $" ({c.Part})"));
        var groups = await MyGroupsAsync(courses, uid, ct);   // CollectGradesAsync cũng dùng cái này để lọc mục điểm của nhóm khác
        var outMap = new Dictionary<string, JsonObject>();
        // Bài tập có mốc trên lịch hành động: "instance" của mốc → mục "ev" tương ứng. Trên LMS trường, instance trùng cmid
        // của bài (đo trên dữ liệu thật 03/10/2026), không phải id như tài liệu; ghép theo cmid, id để dự phòng. Lịch đọc đủ (không lỗi, không bị cắt
        // ở 4 trang) thì biết chắc bài nào không còn mốc, tức là đã nộp (xem dưới).
        var assignEvents = new Dictionary<long, JsonObject>();
        var calendarComplete = false;
        var assignWithoutInstance = false;
        try
        {
            // Moodle chỉ cho tối đa 50 mục mỗi lần (limitnum 1..50): đọc theo trang bằng aftereventid, tối đa 4 trang.
            var events = new List<JsonObject>();
            long after = 0;
            for (var page = 0; page < AssignPairing.MaxPages; page++)
            {
                var res = await CallAsync("core_calendar_get_action_events_by_timesort",
                    [Arg("timesortfrom", Now() - AssignPairing.WindowBefore), Arg("timesortto", Now() + AssignPairing.WindowAfter), Arg("limitnum", AssignPairing.PageSize),
                     Arg("aftereventid", after), Arg("limittononsuspendedevents", 1)], ct);
                var batch = (res["events"] as JsonArray ?? []).OfType<JsonObject>().ToList();
                events.AddRange(batch);
                if (batch.Count < AssignPairing.PageSize) break;
                after = batch[^1]["id"]?.GetValue<long>() ?? 0;
                if (after == 0) break;
            }
            foreach (var e in events)
            {
                var cid = e["course"]?["id"]?.GetValue<long>() ?? -1;
                var name = e["name"]?.GetValue<string>() ?? "";
                if (!subj.ContainsKey(cid) || !ForMyGroup(name, cid, groups)) continue;
                var mod = e["modulename"]?.GetValue<string>();
                outMap["ev" + e["id"]] = new JsonObject
                {
                    ["id"] = "ev" + e["id"],
                    ["course"] = cid,
                    ["subject"] = subj[cid],
                    ["name"] = WebUtility.HtmlDecode(e["activityname"]?.GetValue<string>() ?? name),
                    ["kind"] = mod is "assign" or "quiz" ? mod : "event",
                    ["time"] = e["timesort"]?.GetValue<long>() ?? e["timestart"]?.GetValue<long>(),
                    ["label"] = WebUtility.HtmlDecode(name),
                    // Loại mốc của Moodle: quiz có "open"/"close", bài nộp có "due". Mốc mở không phải hạn nộp.
                    ["phase"] = e["eventtype"]?.GetValue<string>(),
                    ["url"] = e["url"]?.GetValue<string>(),
                };
                if (mod == "assign")
                {
                    if (e["instance"]?.GetValue<long>() is { } inst) assignEvents[inst] = outMap["ev" + e["id"]];
                    else assignWithoutInstance = true;   // thiếu instance thì không ghép được, cũng không suy ra "đã nộp"
                }
            }
            calendarComplete = AssignPairing.CalendarComplete(events.Count, assignWithoutInstance);
        }
        catch (Exception ex) when (Recoverable(ex, ct)) { Warn("lịch LMS", ex); calendarComplete = false; }
        try
        {
            var res = await CallAsync("mod_assign_get_assignments", Arr("courseids", courses.Select(c => (object)c.Id)), ct);
            var instances = assignEvents.Keys.ToHashSet();
            foreach (var c in res["courses"]!.AsArray().OfType<JsonObject>())
                foreach (var a in c["assignments"]!.AsArray().OfType<JsonObject>())
                {
                    var cid = c["id"]!.GetValue<long>();
                    var due = a["duedate"]?.GetValue<long>() ?? 0;
                    if (due == 0 || !ForMyGroup(a["name"]!.GetValue<string>(), cid, groups)) continue;
                    var aid = a["id"]!.GetValue<long>();
                    // Cùng một bài đã có mốc "ev" trên lịch: không thêm mục thứ hai (trước đây mỗi bài hiện và được nhắc 2 lần).
                    // Chỉ chép đề và file đính kèm sang mục "ev" để vẫn lưu về máy.
                    if (AssignPairing.MatchInstance(aid, a["cmid"]?.GetValue<long>(), instances) is { } inst
                        && assignEvents.TryGetValue(inst, out var ev))
                    {
                        ev["intro"] = a["intro"]?.GetValue<string>();
                        ev["files"] = AttachmentFiles(a);
                        continue;
                    }
                    // Không có mốc trên lịch: giữ để lưu đề, nhưng nếu suy ra được là đã nộp thì đánh dấu done (không đếm, không nhắc).
                    var done = AssignPairing.InferDone(due, Now(), calendarComplete);
                    outMap.TryAdd("as" + aid, new JsonObject
                    {
                        ["id"] = "as" + a["id"],
                        ["course"] = cid,
                        ["subject"] = subj.GetValueOrDefault(cid, ""),
                        ["name"] = WebUtility.HtmlDecode(a["name"]!.GetValue<string>()),
                        ["kind"] = "assign",
                        ["time"] = due,
                        ["label"] = "Hạn nộp",
                        ["url"] = $"{Site}/mod/assign/view.php?id={a["cmid"]}",
                        // Đề và file đính kèm có sẵn trong kết quả này (không tốn thêm request): để lưu về máy.
                        ["intro"] = a["intro"]?.GetValue<string>(),
                        ["files"] = AttachmentFiles(a),
                        ["done"] = done ? true : null,
                    });
                }
        }
        catch (Exception ex) when (Recoverable(ex, ct)) { Warn("bài tập LMS", ex); }
        foreach (var e in outMap.Values.Where(e => e["done"] is null).ToList()) e.Remove("done");
        return new JsonArray(outMap.Values.OrderBy(e => e["time"]?.GetValue<long>() ?? 0).Cast<JsonNode>().ToArray());
    }

    private static JsonArray AttachmentFiles(JsonObject a) => new((a["introattachments"] as JsonArray ?? []).OfType<JsonObject>()
        .Select(f => (JsonNode)new JsonObject
        {
            ["filename"] = f["filename"]?.DeepClone(),
            ["fileurl"] = f["fileurl"]?.DeepClone(),
            ["filesize"] = f["filesize"]?.DeepClone(),
            ["timemodified"] = f["timemodified"]?.DeepClone(),
            ["filepath"] = "/",
            ["type"] = "file",
        }).ToArray());

    /// <summary>
    /// Tên nhóm của mình trong từng lớp (lớp thí nghiệm dùng chung cho nhiều nhóm). Nhóm gần như không đổi trong kỳ nên
    /// cache vào lms-groups.json, chỉ gọi API cho lớp chưa biết, 7 ngày thì hỏi lại hết.
    /// </summary>
    private static async Task<Dictionary<long, HashSet<string>>> MyGroupsAsync(List<CourseInfo> courses, long uid, CancellationToken ct)
    {
        var fresh = File.Exists(GroupsFile) && DateTime.UtcNow - File.GetLastWriteTimeUtc(GroupsFile) < TimeSpan.FromDays(7);
        var cached = fresh ? JsonStore.ReadObject(GroupsFile) : new JsonObject();
        var map = new Dictionary<long, HashSet<string>>();
        var asked = false;
        foreach (var c in courses)
        {
            if (cached[c.Id.ToString()] is JsonArray known) { map[c.Id] = known.Select(x => x!.GetValue<string>()).ToHashSet(); continue; }
            asked = true;
            try
            {
                var r = await CallAsync("core_group_get_course_user_groups", [Arg("courseid", c.Id), Arg("userid", uid)], ct);
                map[c.Id] = r["groups"]!.AsArray().Select(g => g?["name"]?.GetValue<string>() ?? "").ToHashSet();
            }
            catch (Exception e) when (Recoverable(e, ct)) { map[c.Id] = []; Warn($"nhóm lớp {c.Subject}", e); }
        }
        if (asked) JsonStore.Write(GroupsFile, map);
        return map;
    }

    /// <summary>Mốc có mã nhóm trong tên (theo groupPattern) mà không phải nhóm của mình thì bỏ.</summary>
    private static bool ForMyGroup(string name, long cid, Dictionary<long, HashSet<string>> groups)
    {
        var pattern = Config.Str("sources.lms.groupPattern");
        if (pattern.Length == 0) return true;
        var codes = Regex.Matches(name, pattern).Select(x => x.Value).ToHashSet();
        return codes.Count == 0 || codes.Overlaps(groups.GetValueOrDefault(cid, []));
    }
}
