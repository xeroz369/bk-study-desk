using System.Text.Json;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using SoHocTap.Files;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

// Thông báo: diễn đàn của các môn kỳ này và notification của Moodle.
public sealed partial class LmsSource
{
    /// <summary>
    /// Thông báo: bài đăng diễn đàn (Các thông báo, diễn đàn thường) của các môn kỳ này, kể cả khóa phụ cùng môn bị ẩn khỏi
    /// danh sách lớp (vd. "…_Video" có diễn đàn hỏi đáp đề thi), cộng thêm notification hệ thống của Moodle. Chỉ giữ announcementDays ngày.
    /// Mỗi lần chạy: 1 request lấy danh sách diễn đàn + 1 request notification + 1 request cho mỗi diễn đàn có thể có bài mới.
    /// Diễn đàn chỉ đọc lại khi numdiscussions đổi, khi core_course_get_updates_since báo module có update,
    /// hoặc đã quá 24 giờ (để bắt reply mới); còn lại thì dùng cache bài cũ. Mốc lưu ở lms.json → forumsSeen {id: {n, at}}.
    /// </summary>
    private static async Task<JsonArray> CollectAnnouncementsAsync(JsonArray enrolled, List<CourseInfo> current, long uid,
        JsonObject prev, HashSet<long> changedModules, CancellationToken ct)
    {
        var seen = prev["forumsSeen"] as JsonObject ?? new JsonObject();
        var prevItems = (prev["announcements"] as JsonArray ?? []).OfType<JsonObject>().Where(x => x["forumId"] is not null)
            .GroupBy(x => x["forumId"]!.GetValue<long>()).ToDictionary(g => g.Key, g => g.ToList());
        var nowSeen = new JsonObject();
        int asked = 0, reused = 0;
        var subjects = current.Select(c => c.Subject).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Chỉ lớp của học kỳ hiện tại (kể cả lớp bị bỏ khi đồng bộ tài liệu như lớp video của môn): lớp kỳ trước cùng tên môn
        // (học lại, học cải thiện) không đọc diễn đàn nữa.
        var term = current.FirstOrDefault()?.Term;
        var courseSubject = enrolled.OfType<JsonObject>()
            .Where(c => Organizer.SubjectOf(c["fullname"]?.GetValue<string>() ?? "") is not null)
            .Select(CourseMeta)
            .Where(m => m.Term == term && subjects.Contains(m.Subject))
            .ToDictionary(m => m.Id, m => m.Subject);
        var since = Now() - Config.Int("sources.lms.announcementDays", 60) * 86400L;
        var perForum = Config.Int("sources.lms.announcementsPerForum", 10);
        var items = new List<JsonObject>();
        try
        {
            var forums = (JsonArray)await CallAsync("mod_forum_get_forums_by_courses", [], ct);
            foreach (var f in forums.OfType<JsonObject>())
            {
                var cid = f["course"]?.GetValue<long>() ?? -1;
                if (!courseSubject.TryGetValue(cid, out var subject)) continue;
                var fid = f["id"]!.GetValue<long>();
                var count = f["numdiscussions"]?.GetValue<long?>();
                if (seen[fid.ToString()] is JsonObject last && count is not null && last["n"]?.GetValue<long?>() == count
                    && Now() - (last["at"]?.GetValue<long>() ?? 0) < 86400 && !changedModules.Contains(f["cmid"]?.GetValue<long>() ?? -1))
                {
                    reused++;
                    nowSeen[fid.ToString()] = last.DeepClone();
                    items.AddRange(prevItems.GetValueOrDefault(fid, []).Where(x => (x["time"]?.GetValue<long>() ?? 0) >= since).Select(x => (JsonObject)x.DeepClone()));
                    continue;
                }
                asked++;
                var d = await CallAsync("mod_forum_get_forum_discussions", [Arg("forumid", fid), Arg("perpage", perForum)], ct);
                nowSeen[fid.ToString()] = new JsonObject { ["n"] = count, ["at"] = Now() };
                foreach (var x in (d["discussions"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    var time = x["timemodified"]?.GetValue<long>() ?? 0;
                    if (time < since) continue;
                    items.Add(new JsonObject
                    {
                        ["id"] = "fd" + x["discussion"],
                        ["forumId"] = fid,
                        ["kind"] = f["type"]?.GetValue<string>() == "news" ? "news" : "forum",
                        ["course"] = cid,
                        ["subject"] = subject,
                        ["forum"] = WebUtility.HtmlDecode(f["name"]?.GetValue<string>() ?? ""),
                        ["title"] = WebUtility.HtmlDecode(x["name"]?.GetValue<string>() ?? ""),
                        ["author"] = x["userfullname"]?.GetValue<string>(),
                        ["time"] = time,
                        ["created"] = x["created"]?.GetValue<long>(),
                        ["replies"] = x["numreplies"]?.GetValue<int>() ?? 0,
                        ["pinned"] = x["pinned"]?.GetValue<bool>() ?? false,
                        ["message"] = x["message"]?.GetValue<string>() ?? "",
                        ["attachments"] = new JsonArray((x["attachments"] as JsonArray ?? []).OfType<JsonObject>()
                            .Select(a => (JsonNode)new JsonObject { ["name"] = a["filename"]?.GetValue<string>(), ["url"] = a["fileurl"]?.GetValue<string>() }).ToArray()),
                        ["url"] = $"{Site}/mod/forum/discuss.php?d={x["discussion"]}",
                    });
                }
            }
        }
        catch (Exception e) when (Recoverable(e, ct)) { Warn("thông báo diễn đàn", e); nowSeen.Clear(); }   // lần sau đọc lại hết diễn đàn
        Log.Info($"Thông báo LMS: {courseSubject.Count} khóa của môn đang học, {items.Count} bài trong {Config.Int("sources.lms.announcementDays", 60)} ngày; gọi {asked} diễn đàn, dùng cache {reused}");
        prev["forumsSeen"] = nowSeen;   // ghi chung với lms.json (xem SyncCoreAsync)
        try
        {
            var n = await CallAsync("message_popup_get_popup_notifications", [Arg("useridto", uid), Arg("limit", 20), Arg("newestfirst", 1)], ct);
            foreach (var x in (n["notifications"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var time = x["timecreated"]?.GetValue<long>() ?? 0;
                if (time < since) continue;
                items.Add(new JsonObject
                {
                    ["id"] = "nt" + x["id"],
                    ["kind"] = "system",
                    ["subject"] = "",
                    ["forum"] = "Thông báo LMS",
                    ["title"] = x["subject"]?.GetValue<string>() ?? "",
                    ["author"] = "",
                    ["time"] = time,
                    ["replies"] = 0,
                    ["pinned"] = false,
                    ["message"] = x["fullmessagehtml"]?.GetValue<string>() is { Length: > 0 } h ? h : x["smallmessage"]?.GetValue<string>() ?? "",
                    ["attachments"] = new JsonArray(),
                    ["url"] = x["contexturl"]?.GetValue<string>(),
                });
            }
        }
        catch (Exception e) when (Recoverable(e, ct)) { Warn("thông báo LMS", e); }
        return new JsonArray(items.OrderByDescending(i => i["time"]!.GetValue<long>()).Cast<JsonNode>().ToArray());
    }
}
