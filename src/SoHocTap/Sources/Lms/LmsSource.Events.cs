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

    /// <param name="groups">Nhóm của mình trong từng lớp (<see cref="MyGroupsAsync"/>, một bước riêng của lượt đồng bộ): mốc của nhóm khác thì bỏ.
    /// Lớp không có trong map (đọc nhóm lỗi) hay có mà rỗng thì giữ mọi mốc (<see cref="GroupFilter.Keep"/>).</param>
    private async Task<JsonArray> CollectEventsAsync(List<CourseInfo> courses, Dictionary<long, HashSet<string>> groups, CancellationToken ct)
    {
        var subj = courses.ToDictionary(c => c.Id, c => c.Subject + (c.Part is null ? "" : $" ({c.Part})"));
        var outMap = new Dictionary<string, JsonObject>();
        var noGroupInfo = new HashSet<long>();
        // Bài tập có mốc trên lịch hành động: "instance" của mốc → mục "ev" tương ứng. Trên LMS trường, instance trùng cmid
        // của bài (đo trên dữ liệu thật 03/10/2026), không phải id như tài liệu; ghép theo cmid, id để dự phòng. Lịch đọc đủ (không lỗi, không bị cắt
        // ở MaxPages trang) thì biết chắc bài nào không còn mốc, tức là đã nộp (xem dưới).
        var assignEvents = new Dictionary<long, JsonObject>();
        var calendarComplete = false;
        var assignWithoutInstance = false;
        try
        {
            // Moodle chỉ cho tối đa 50 mục mỗi lần (limitnum 1..50): đọc theo trang bằng aftereventid, tối đa MaxPages trang.
            var events = new List<JsonObject>();
            long after = 0;
            for (var page = 0; page < AssignPairing.MaxPages; page++)
            {
                var res = await CallAsync("core_calendar_get_action_events_by_timesort",
                    [Arg("timesortfrom", Now() - AssignPairing.WindowBefore), Arg("limitnum", AssignPairing.PageSize),
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
                if (!subj.ContainsKey(cid) || !ForMyGroup(name, cid, groups, subj, noGroupInfo)) continue;
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
            var toAsk = new List<(SubmissionAsk Ask, JsonObject Item)>();
            foreach (var c in res["courses"]!.AsArray().OfType<JsonObject>())
                foreach (var a in c["assignments"]!.AsArray().OfType<JsonObject>())
                {
                    var cid = c["id"]!.GetValue<long>();
                    var due = a["duedate"]?.GetValue<long>() ?? 0;
                    if (due == 0 || !ForMyGroup(a["name"]!.GetValue<string>(), cid, groups, subj, noGroupInfo)) continue;
                    var aid = a["id"]!.GetValue<long>();
                    // Cùng một bài đã có mốc "ev" trên lịch: không thêm mục thứ hai (trước đây mỗi bài hiện và được nhắc 2 lần).
                    // Chỉ chép đề và file đính kèm sang mục "ev" để vẫn lưu về máy.
                    if (AssignPairing.MatchInstance(aid, a["cmid"]?.GetValue<long>(), instances) is { } inst
                        && assignEvents.TryGetValue(inst, out var ev))
                    {
                        ev["intro"] = a["intro"]?.GetValue<string>();
                        ev["files"] = AttachmentFiles(a);
                        toAsk.Add((new SubmissionAsk(aid, a["cmid"]?.GetValue<long>(), due), ev));
                        continue;
                    }
                    // Không có mốc trên lịch: giữ để lưu đề, nhưng nếu suy ra được là đã nộp thì đánh dấu done (không đếm, không nhắc).
                    var done = AssignPairing.InferDone(due, Now(), calendarComplete);
                    var asItem = new JsonObject
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
                    };
                    if (outMap.TryAdd("as" + aid, asItem)) toAsk.Add((new SubmissionAsk(aid, a["cmid"]?.GetValue<long>(), due), asItem));
                }
            await ApplySubmissionStatesAsync(toAsk, ct);
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
            // Lỗi thì để lớp này ngoài map (chưa biết nhóm): không cache, lần sau hỏi lại; mốc có mã nhóm của lớp vẫn được giữ.
            catch (Exception e) when (Recoverable(e, ct)) { Warn($"nhóm lớp {c.Subject}", e); }
        }
        // Cache 7 ngày tính theo mtime: hỏi lại mà nhóm không đổi (JsonStore không ghi) thì vẫn chạm mtime, kẻo lượt nào cũng hỏi lại hết.
        if (asked && !JsonStore.Write(GroupsFile, map) && File.Exists(GroupsFile)) File.SetLastWriteTimeUtc(GroupsFile, DateTime.UtcNow);
        return map;
    }

    /// <summary>
    /// Mốc có mã nhóm trong tên (theo groupPattern) mà chắc chắn không phải nhóm của mình thì bỏ. Lớp chưa biết nhóm thì giữ,
    /// và ghi WARN một lần mỗi lớp trong lượt đồng bộ (<paramref name="warned"/>) để biết lớp nào thiếu thông tin nhóm. Sổ điểm gọi không kèm
    /// <paramref name="warned"/> (bước mốc thời gian đã ghi rồi).
    /// </summary>
    private static bool ForMyGroup(string name, long cid, Dictionary<long, HashSet<string>> groups, Dictionary<long, string>? subj = null, HashSet<long>? warned = null)
    {
        var pattern = Settings.Lms.GroupPattern;
        if (pattern.Length == 0) return true;
        var codes = Regex.Matches(name, pattern).Select(x => x.Value).ToHashSet();
        var mine = groups.GetValueOrDefault(cid);
        if (codes.Count > 0 && mine is not { Count: > 0 } && warned?.Add(cid) == true)
            Log.Warn($"LMS: lớp {subj?.GetValueOrDefault(cid) ?? cid.ToString(System.Globalization.CultureInfo.InvariantCulture)} chưa có thông tin nhóm, giữ cả mốc có mã nhóm");
        return GroupFilter.Keep(codes, mine);
    }
}
