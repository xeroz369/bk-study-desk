using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

public sealed partial class LmsSource
{
    public const string DueLabel = "Hạn nộp";

    private static async Task<JsonArray> CollectEventsAsync(List<CourseInfo> courses, Dictionary<long, HashSet<string>> groups, long uid, CancellationToken ct)
    {
        var subj = courses.ToDictionary(c => c.Id, c => c.Subject + (c.Part is null ? "" : $" ({c.Part})"));
        var outMap = new Dictionary<string, JsonObject>();
        var noGroupInfo = new HashSet<long>();
        var assignEvents = new Dictionary<long, JsonObject>();
        var calendarComplete = false;
        var assignWithoutInstance = false;
        try
        {
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
                    ["phase"] = e["eventtype"]?.GetValue<string>(),
                    ["url"] = e["url"]?.GetValue<string>(),
                };
                if (e["action"]?["actionable"] is JsonValue act && act.TryGetValue<bool>(out var can) && !can) outMap["ev" + e["id"]]["done"] = true;
                if (mod == "assign")
                {
                    if (e["instance"]?.GetValue<long>() is { } inst) assignEvents[inst] = outMap["ev" + e["id"]];
                    else assignWithoutInstance = true;
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
                    if (AssignPairing.MatchInstance(aid, a["cmid"]?.GetValue<long>(), instances) is { } inst
                        && assignEvents.TryGetValue(inst, out var ev))
                    {
                        ev["intro"] = a["intro"]?.GetValue<string>();
                        ev["files"] = AttachmentFiles(a);
                        toAsk.Add((new SubmissionAsk(aid, a["cmid"]?.GetValue<long>(), due), ev));
                        continue;
                    }
                    var done = AssignPairing.InferDone(due, Now(), calendarComplete);
                    var asItem = new JsonObject
                    {
                        ["id"] = "as" + a["id"],
                        ["course"] = cid,
                        ["subject"] = subj.GetValueOrDefault(cid, ""),
                        ["name"] = WebUtility.HtmlDecode(a["name"]!.GetValue<string>()),
                        ["kind"] = "assign",
                        ["time"] = due,
                        ["label"] = DueLabel,
                        ["url"] = $"{Site}/mod/assign/view.php?id={a["cmid"]}",
                        ["intro"] = a["intro"]?.GetValue<string>(),
                        ["files"] = AttachmentFiles(a),
                        ["done"] = done ? true : null,
                    };
                    if (outMap.TryAdd("as" + aid, asItem)) toAsk.Add((new SubmissionAsk(aid, a["cmid"]?.GetValue<long>(), due), asItem));
                }
            await ApplySubmissionStatesAsync(toAsk, ct);
        }
        catch (Exception ex) when (Recoverable(ex, ct)) { Warn("bài tập LMS", ex); }
        await MarkCompletedAsync(outMap.Values, uid, ct);
        foreach (var e in outMap.Values.Where(e => e["done"] is null).ToList()) e.Remove("done");
        return new JsonArray(outMap.Values.OrderBy(e => e["time"]?.GetValue<long>() ?? 0).Cast<JsonNode>().ToArray());
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"[?&]id=(\d+)")] private static partial System.Text.RegularExpressions.Regex CmidRx();

    private static async Task MarkCompletedAsync(IEnumerable<JsonObject> items, long uid, CancellationToken ct)
    {
        var open = items.Where(e => e["done"] is null && e["kind"]?.GetValue<string>() == "event"
                                    && e["url"]?.GetValue<string>() is { } u && CmidRx().IsMatch(u)).ToList();
        foreach (var byCourse in open.GroupBy(e => e["course"]!.GetValue<long>()))
        {
            JsonNode res;
            try { res = await CallAsync("core_completion_get_activities_completion_status", [Arg("courseid", byCourse.Key), Arg("userid", uid)], ct); }
            catch (Exception ex) when (ex is LmsException or SessionExpiredException or HttpRequestException or TimeoutException)
            {
                Log.Debug($"Trạng thái hoàn thành lớp {byCourse.Key}: {ex.Message}");
                continue;
            }
            var completed = (res["statuses"] as JsonArray ?? []).OfType<JsonObject>()
                .Where(x => x["state"]?.GetValue<int>() is 1 or 2)
                .Select(x => x["cmid"]?.GetValue<long>() ?? 0).ToHashSet();
            foreach (var e in byCourse)
                if (long.TryParse(CmidRx().Match(e["url"]!.GetValue<string>()).Groups[1].Value, out var cmid) && completed.Contains(cmid)) e["done"] = true;
        }
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

    private static async Task<Dictionary<long, HashSet<string>>> MyGroupsAsync(List<CourseInfo> courses, long uid, CancellationToken ct)
    {
        var fresh = File.Exists(GroupsFile) && DateTime.UtcNow - File.GetLastWriteTimeUtc(GroupsFile) < TimeSpan.FromDays(7);
        var cached = fresh ? JsonStore.ReadObject(GroupsFile) : new JsonObject();
        var map = new Dictionary<long, HashSet<string>>();
        var asked = false;
        foreach (var c in courses)
        {
            if (cached[c.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)] is JsonArray known) { map[c.Id] = known.Select(x => x!.GetValue<string>()).ToHashSet(); continue; }
            asked = true;
            try
            {
                var r = await CallAsync("core_group_get_course_user_groups", [Arg("courseid", c.Id), Arg("userid", uid)], ct);
                map[c.Id] = r["groups"]!.AsArray().Select(g => g?["name"]?.GetValue<string>() ?? "").ToHashSet();
            }
            catch (Exception e) when (Recoverable(e, ct)) { Warn($"nhóm lớp {c.Subject}", e); }
        }
        if (asked && !JsonStore.Write(GroupsFile, map) && File.Exists(GroupsFile)) File.SetLastWriteTimeUtc(GroupsFile, DateTime.UtcNow);
        return map;
    }

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
