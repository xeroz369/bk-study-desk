using System.Text.Json;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using SoHocTap.Files;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

// Quiz: lượt làm, lưu bản xem lại.
public sealed partial class LmsSource
{
    /// <summary>Chỉ gọi lại attempt của quiz đang mở hoặc vừa đóng; quiz khác thì dùng lại data cũ.</summary>
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
            // Quiz cũ đã đóng: dùng lại data, trừ khi còn lượt đã nộp chưa lưu (vừa bật tự lưu, hoặc file bị xóa).
            if (old.TryGetValue(id, out var prev) && !active && !Unsaved(id, prev)) { item["attempts"] = prev["attempts"]?.DeepClone(); list.Add(item); continue; }
            if (!opened) { list.Add(item); continue; }
            // Quiz không có hạn đóng thì không biết bao giờ hết làm được: hỏi lượt làm tối đa ngày một lần, không hỏi ở mọi lần đồng bộ.
            if (close is null && old.TryGetValue(id, out var last) && (last["checked"]?.GetValue<long>() ?? 0) > t - 86400 && !Unsaved(id, last))
            {
                item["attempts"] = last["attempts"]?.DeepClone();
                item["checked"] = last["checked"]?.DeepClone();
                list.Add(item);
                continue;
            }
            item["checked"] = t;
            JsonArray attempts;
            try { attempts = (await CallAsync("mod_quiz_get_user_attempts", [Arg("quizid", id), Arg("status", "finished")], ct))["attempts"]!.AsArray(); }
            catch (Exception e) when (Recoverable(e, ct)) { attempts = []; Warn($"lượt làm quiz {item["name"]}", e); }
            foreach (var a in attempts.OfType<JsonObject>())
            {
                var aid = a["id"]!.GetValue<long>();
                item["attempts"]!.AsArray().Add(new JsonObject { ["id"] = aid, ["finished"] = a["timefinish"]?.GetValue<long>(), ["grade"] = a["sumgrades"]?.DeepClone() });
                if (!SaveQuizzes) continue;
                // Only finished attempts, read-only review API. Never touch an attempt that is in progress.
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
                        questions.Add(new JsonObject
                        {
                            ["slot"] = x["slot"]?.DeepClone(),
                            ["type"] = x["type"]?.DeepClone(),
                            ["html"] = await CleanReviewAsync(x["html"]?.GetValue<string>() ?? "", ct),
                            ["mark"] = x["mark"]?.DeepClone(),
                            ["maxmark"] = x["maxmark"]?.DeepClone(),
                            ["status"] = x["status"]?.DeepClone(),
                        });
                    head["grade"] = rev["grade"]?.DeepClone();
                    // LMS may hide the right answers until the quiz closes: remember it so we fetch again after close.
                    head["answers"] = questions.Any(q => q?["html"]?.GetValue<string>().Contains("rightanswer", StringComparison.Ordinal) == true);
                    head["questions"] = questions;
                    JsonStore.Write(path, head);
                    log($"  lưu quiz: {item["name"]}{(head["answers"]!.GetValue<bool>() ? "" : " (chưa có đáp án, sẽ lấy lại sau khi quiz đóng)")}");
                }
                catch (LmsException e) when (e.Code is "noreview" or "noreviewattempt" or "noreviewavailable")
                {
                    // Quiz does not allow review: keep a stub (name, grade) so the app can offer "ghi nhanh câu còn nhớ".
                    head["grade"] = a["sumgrades"]?.DeepClone();
                    head["noReview"] = true;
                    head["reason"] = e.Message;
                    JsonStore.Write(path, head);
                    log($"  quiz không cho xem lại: {item["name"]}");
                }
                catch (Exception e) when (Recoverable(e, ct))
                {
                    // Lỗi thoáng qua (mạng, server chậm…): không ghi file, lần đồng bộ sau thử lại (một request).
                    Warn($"lưu quiz {item["name"]}", e);
                }
            }
            list.Add(item);
        }
        return list;
    }

    /// <summary>Tự lưu bản xem lại quiz đã nộp (sources.lms.saveQuizzes, chỉnh trong Cài đặt hoặc Kho quiz).</summary>
    private static bool SaveQuizzes => Config.Bool("sources.lms.saveQuizzes", true);

    /// <summary>Quiz có lượt đã nộp mà chưa có file lưu, hoặc file kiểu cũ (chưa lọc sesskey, thiếu savedAt).</summary>
    private static bool Unsaved(long quizId, JsonObject prev) =>
        SaveQuizzes && (prev["attempts"] as JsonArray ?? []).OfType<JsonObject>()
            .Any(a => JsonStore.Read(Path.Combine(QuizDir, $"{quizId}-{a["id"]}.json")) is not JsonObject f || f["savedAt"] is null);

    /// <summary>Saved without answers (or without review) and the quiz has closed since: try once more.</summary>
    private static bool NeedsRefetch(JsonObject saved, long? close, long now)
    {
        if (saved["savedAt"] is null) return true;   // file kiểu cũ: lưu lại bằng bản đã lọc
        var incomplete = saved["noReview"]?.GetValue<bool>() == true || saved["answers"]?.GetValue<bool>() == false;
        var savedAt = saved["savedAt"]?.GetValue<long>() ?? 0;
        return incomplete && close is { } c && c <= now && savedAt < c;
    }

    [GeneratedRegex(@"<script\b[\s\S]*?</script>|<input type=""hidden""[^>]*>|<div class=""questionflag[\s\S]*?</label>\s*</div>", RegexOptions.IgnoreCase)]
    private static partial Regex ReviewNoiseRx();
    [GeneratedRegex(@"src=""(https?://[^""]+/pluginfile\.php/[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PluginFileRx();

    /// <summary>
    /// Review HTML from LMS: drop scripts and hidden inputs (they carry the session key), embed question images
    /// (pluginfile URLs need the token) as data URIs so the saved quiz works offline and can be shared.
    /// </summary>
    private static async Task<string> CleanReviewAsync(string html, CancellationToken ct)
    {
        html = ReviewNoiseRx().Replace(html, "");
        foreach (var url in PluginFileRx().Matches(html).Select(m => m.Groups[1].Value).Distinct().ToList())
            if (await DataUriAsync(WebUtility.HtmlDecode(url), ct) is { } data) html = html.Replace(url, data);
        return html;
    }
}
