using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

public sealed partial class LmsSource
{
    private static async Task<JsonArray> CollectQuizzesAsync(List<CourseInfo> courses, JsonArray? previous, long lastSync, Action<string> log, CancellationToken ct)
    {
        var subj = courses.ToDictionary(c => c.Id, c => c.Subject);
        var old = (previous ?? []).OfType<JsonObject>().ToDictionary(q => q["id"]!.GetValue<long>());
        JsonNode res;
        try { res = await CallAsync("mod_quiz_get_quizzes_by_courses", Arr("courseids", courses.Select(c => (object)c.Id)), ct); }
        catch (Exception e) when (Recoverable(e, ct)) { Warn("danh sách quiz", e); return previous?.DeepClone().AsArray() ?? []; }
        Directory.CreateDirectory(QuizDir);
        var t = Now();
        var list = new JsonArray();
        foreach (var q in res["quizzes"]!.AsArray().OfType<JsonObject>())
        {
            var id = q["id"]!.GetValue<long>();
            long? open = q["timeopen"]?.GetValue<long>() is > 0 and var o ? o : null;
            long? close = q["timeclose"]?.GetValue<long>() is > 0 and var c ? c : null;
            var item = new JsonObject
            {
                ["id"] = id,
                ["course"] = q["course"]?.GetValue<long>(),
                ["subject"] = subj.GetValueOrDefault(q["course"]!.GetValue<long>(), ""),
                ["name"] = WebUtility.HtmlDecode(q["name"]!.GetValue<string>()),
                ["open"] = open,
                ["close"] = close,
                ["timelimit"] = q["timelimit"]?.GetValue<long>(),
                ["attempts"] = new JsonArray(),
                ["url"] = $"{Site}/mod/quiz/view.php?id={q["coursemodule"]}",
            };
            bool opened = open is null || open <= t;
            bool active = opened && (close is null || close >= lastSync - 86400);
            if (old.TryGetValue(id, out var prev) && !active && !Unsaved(id, prev)) { item["attempts"] = prev["attempts"]?.DeepClone(); list.Add((JsonNode)item); continue; }
            if (!opened) { list.Add((JsonNode)item); continue; }
            if (close is null && old.TryGetValue(id, out var last) && (last["checked"]?.GetValue<long>() ?? 0) > t - 86400 && !Unsaved(id, last))
            {
                item["attempts"] = last["attempts"]?.DeepClone();
                item["checked"] = last["checked"]?.DeepClone();
                list.Add((JsonNode)item);
                continue;
            }
            item["checked"] = t;
            JsonArray attempts;
            try { attempts = (await CallAsync("mod_quiz_get_user_attempts", [Arg("quizid", id), Arg("status", "finished")], ct))["attempts"]!.AsArray(); }
            catch (Exception e) when (Recoverable(e, ct)) { attempts = []; Warn($"lượt làm quiz {item["name"]}", e); }
            foreach (var a in attempts.OfType<JsonObject>())
            {
                var aid = a["id"]!.GetValue<long>();
                item["attempts"]!.AsArray().Add((JsonNode)new JsonObject { ["id"] = aid, ["finished"] = a["timefinish"]?.GetValue<long>(), ["grade"] = a["sumgrades"]?.DeepClone() });
                if (!SaveQuizzes) continue;
                var path = Path.Combine(QuizDir, $"{id}-{aid}.json");
                if (JsonStore.Read(path) is JsonObject saved && !NeedsRefetch(saved, close, t)) continue;
                var head = new JsonObject
                {
                    ["quiz"] = item["name"]?.DeepClone(),
                    ["subject"] = item["subject"]?.DeepClone(),
                    ["course"] = item["course"]?.DeepClone(),
                    ["attempt"] = aid,
                    ["finished"] = a["timefinish"]?.DeepClone(),
                    ["closesAt"] = close,
                    ["savedAt"] = t,
                };
                try
                {
                    var rev = await CallAsync("mod_quiz_get_attempt_review", [Arg("attemptid", aid), Arg("page", -1)], ct);
                    var questions = new JsonArray();
                    foreach (var x in rev["questions"]!.AsArray().OfType<JsonObject>())
                        questions.Add((JsonNode)new JsonObject
                        {
                            ["slot"] = x["slot"]?.DeepClone(),
                            ["type"] = x["type"]?.DeepClone(),
                            ["html"] = await CleanReviewAsync(x["html"]?.GetValue<string>() ?? "", ct),
                            ["mark"] = x["mark"]?.DeepClone(),
                            ["maxmark"] = x["maxmark"]?.DeepClone(),
                            ["status"] = x["status"]?.DeepClone(),
                        });
                    head["grade"] = rev["grade"]?.DeepClone();
                    head["answers"] = questions.Any(q => q?["html"]?.GetValue<string>().Contains("rightanswer", StringComparison.Ordinal) == true);
                    head["questions"] = questions;
                    JsonStore.Write(path, head);
                    log($"  lưu quiz: {item["name"]}{(head["answers"]!.GetValue<bool>() ? "" : " (chưa có đáp án, sẽ lấy lại sau khi quiz đóng)")}");
                }
                catch (LmsException e) when (e.Code is "noreview" or "noreviewattempt" or "noreviewavailable")
                {
                    head["grade"] = a["sumgrades"]?.DeepClone();
                    head["noReview"] = true;
                    head["reason"] = e.Message;
                    JsonStore.Write(path, head);
                    log($"  quiz không cho xem lại: {item["name"]}");
                }
                catch (Exception e) when (Recoverable(e, ct))
                {
                    Warn($"lưu quiz {item["name"]}", e);
                }
            }
            list.Add((JsonNode)item);
        }
        return list;
    }

    private static bool SaveQuizzes => Config.Bool("sources.lms.saveQuizzes", true);

    private static bool Unsaved(long quizId, JsonObject prev) =>
        SaveQuizzes && (prev["attempts"] as JsonArray ?? []).OfType<JsonObject>()
            .Any(a => JsonStore.Read(Path.Combine(QuizDir, $"{quizId}-{a["id"]}.json")) is not JsonObject f || f["savedAt"] is null);

    private static bool NeedsRefetch(JsonObject saved, long? close, long now)
    {
        if (saved["savedAt"] is null) return true;
        var incomplete = saved["noReview"]?.GetValue<bool>() == true || saved["answers"]?.GetValue<bool>() == false;
        var savedAt = saved["savedAt"]?.GetValue<long>() ?? 0;
        return incomplete && close is { } c && c <= now && savedAt < c;
    }

    [GeneratedRegex(@"<script\b[\s\S]*?</script>|<input type=""hidden""[^>]*>|<div class=""questionflag[\s\S]*?</label>\s*</div>", RegexOptions.IgnoreCase)]
    private static partial Regex ReviewNoiseRx();
    [GeneratedRegex(@"src=""(https?://[^""]+/pluginfile\.php/[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PluginFileRx();

    private static async Task<string> CleanReviewAsync(string html, CancellationToken ct)
    {
        html = ReviewNoiseRx().Replace(html, "");
        foreach (var url in PluginFileRx().Matches(html).Select(m => m.Groups[1].Value).Distinct().ToList())
            if (await DataUriAsync(WebUtility.HtmlDecode(url), ct) is { } data) html = html.Replace(url, data);
        return html;
    }
}
