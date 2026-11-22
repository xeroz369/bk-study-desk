using System.Net;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

public sealed partial class LmsSource
{
    public const int MessagesPerConversation = 300;

    private static async Task<JsonObject> CollectMessagesAsync(long uid, JsonObject prev, CancellationToken ct)
    {
        var limit = Config.Int("sources.lms.conversations", 50);
        var res = await CallAsync("core_message_get_conversations", [Arg("userid", uid), Arg("limitfrom", 0), Arg("limitnum", limit)], ct);
        var old = (prev["conversations"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(c => c["id"] is not null).ToDictionary(c => c["id"]!.GetValue<long>());
        var output = new JsonArray();
        var returned = new HashSet<long>();
        int asked = 0, reused = 0;
        foreach (var c in (res["conversations"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (c["type"]?.GetValue<int>() == 3) continue;
            if (c["id"]?.GetValue<long>() is not { } id) continue;
            returned.Add(id);
            var members = Members(c["members"] as JsonArray);
            var latest = (c["messages"] as JsonArray ?? []).OfType<JsonObject>().Select(m => m["timecreated"]?.GetValue<long>() ?? 0).DefaultIfEmpty(0).Max();
            var before = old.GetValueOrDefault(id);
            var kept = (before?["messages"] as JsonArray ?? []).OfType<JsonObject>().ToList();
            var lastKept = kept.Select(m => m["time"]?.GetValue<long>() ?? 0).DefaultIfEmpty(0).Max();
            List<JsonObject> messages;
            if (before is not null && kept.Count > 0 && lastKept >= latest)
            {
                reused++;
                messages = [.. kept.Select(m => (JsonObject)m.DeepClone())];
                foreach (var m in messages) m["text"] = LmsHtml.Slim(m["text"]?.GetValue<string>());
            }
            else
            {
                asked++;
                var raw = new List<JsonObject>();
                for (var page = 0; page < 3; page++)
                {
                    var got = await CallAsync("core_message_get_conversation_messages",
                        [Arg("currentuserid", uid), Arg("convid", id), Arg("limitfrom", page * 100), Arg("limitnum", 100), Arg("newest", 1)], ct);
                    foreach (var m in Members(got["members"] as JsonArray)) members.TryAdd(m.Key, m.Value);
                    var batch = (got["messages"] as JsonArray ?? []).OfType<JsonObject>().ToList();
                    raw.AddRange(batch);
                    if (batch.Count < 100 || batch.Any(m => (m["timecreated"]?.GetValue<long>() ?? 0) <= lastKept)) break;
                }
                var fresh = raw.Where(m => m["id"] is not null).Select(m =>
                {
                    var from = m["useridfrom"]?.GetValue<long>() ?? 0;
                    return new JsonObject
                    {
                        ["id"] = m["id"]?.GetValue<long>(),
                        ["from"] = from,
                        ["fromName"] = from == uid ? null : members.GetValueOrDefault(from),
                        ["mine"] = from == uid ? true : null,
                        ["text"] = LmsHtml.Slim(m["text"]?.GetValue<string>()),
                        ["time"] = m["timecreated"]?.GetValue<long>() ?? 0,
                    };
                });
                messages = [.. fresh.Concat(kept.Select(m => (JsonObject)m.DeepClone()))
                    .GroupBy(m => m["id"]?.GetValue<long>() ?? 0).Select(g => g.First())
                    .OrderBy(m => m["time"]!.GetValue<long>()).TakeLast(MessagesPerConversation)];
            }
            var others = members.Where(m => m.Key != uid).Select(m => m.Value).ToList();
            output.Add((JsonNode)new JsonObject
            {
                ["id"] = id,
                ["name"] = WebUtility.HtmlDecode(c["name"]?.GetValue<string>() is { Length: > 0 } n ? n : string.Join(", ", others)),
                ["group"] = c["type"]?.GetValue<int>() == 2 ? true : null,
                ["members"] = new JsonArray([.. others.Select(o => (JsonNode)o)]),
                ["unread"] = c["unreadcount"]?.GetValue<int?>() ?? 0,
                ["lastTime"] = Math.Max(latest, messages.Select(m => m["time"]!.GetValue<long>()).DefaultIfEmpty(0).Max()),
                ["url"] = $"{Site}/message/index.php?convid={id}",
                ["messages"] = new JsonArray([.. messages]),
            });
        }
        foreach (var (id, c) in old)
            if (!returned.Contains(id)) output.Add(c.DeepClone());
        Log.Info($"Tin nhắn LMS: {output.Count} hội thoại; đọc lại {asked}, dùng bản đã lưu {reused}");
        return new JsonObject { ["syncedAt"] = Now(), ["conversations"] = output };
    }

    private static Dictionary<long, string> Members(JsonArray? members) =>
        (members ?? []).OfType<JsonObject>().Where(m => m["id"] is not null)
            .GroupBy(m => m["id"]!.GetValue<long>())
            .ToDictionary(g => g.Key, g => WebUtility.HtmlDecode(g.First()["fullname"]?.GetValue<string>() ?? ""));
}

public sealed partial class LmsSource
{
    private static async Task<JsonObject> CollectPagesAsync(List<CourseInfo> current, JsonObject prev, CancellationToken ct)
    {
        var args = current.Select((c, i) => Arg($"courseids[{i}]", c.Id)).ToList();
        var res = await CallAsync("mod_page_get_pages_by_courses", args, ct);
        var pages = prev["pages"]?.DeepClone() as JsonObject ?? new JsonObject();
        foreach (var p in (res["pages"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (p["coursemodule"]?.GetValue<long>() is not { } cmid) continue;
            pages[cmid.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new JsonObject
            {
                ["course"] = p["course"]?.GetValue<long>(),
                ["name"] = WebUtility.HtmlDecode(p["name"]?.GetValue<string>() ?? ""),
                ["intro"] = LmsHtml.Slim(p["intro"]?.GetValue<string>()),
                ["html"] = LmsHtml.Slim(p["content"]?.GetValue<string>()),
                ["time"] = p["timemodified"]?.GetValue<long>(),
            };
        }
        Log.Info($"Trang LMS: lưu {pages.Count} trang của {current.Count} lớp");
        return new JsonObject { ["syncedAt"] = Now(), ["pages"] = pages };
    }
}
