using System.Text.Json.Nodes;

namespace SoHocTap.Sources.Lms;

/// <summary>Trạng thái nộp bài của một bài tập trên LMS. Unknown = chưa biết (chưa hỏi được), không phải "chưa nộp".</summary>
public enum SubmissionState { Unknown, NotSubmitted, Draft, Submitted, Graded }

/// <summary>Một bài cần hỏi trạng thái: id bài (assignid của mod_assign_*), cmid (khóa cache, null thì không cache) và hạn nộp.</summary>
public readonly record struct SubmissionAsk(long AssignId, long? Cmid, long Due);

public static class SubmissionStates
{
    /// <summary>Cache cũ hơn mức này thì bài "đã nộp" được hỏi lại (giảng viên có thể mở lại bài, hay chấm điểm).</summary>
    public const long SubmittedRecheck = 86400;

    /// <summary>
    /// Đọc kết quả mod_assign_get_submission_status. Có điểm (feedback.grade.grade) hay gradingstatus "graded" thì Graded;
    /// không thì theo lastattempt.submission.status: "submitted" Submitted, "draft" Draft, "new", "reopened" hoặc thiếu thì NotSubmitted.
    /// Kết quả không phải JSON object của Moodle (null, thiếu cả lastattempt lẫn assignmentdata) thì Unknown.
    /// </summary>
    public static SubmissionState FromMoodle(JsonObject? res)
    {
        if (res is null) return SubmissionState.Unknown;
        var last = res["lastattempt"] as JsonObject;
        if (last is null && res["assignmentdata"] is null && res["feedback"] is null) return SubmissionState.Unknown;
        if (res["feedback"]?["grade"]?["grade"] is not null || last?["gradingstatus"]?.ToString() == "graded") return SubmissionState.Graded;
        return last?["submission"]?["status"]?.ToString() switch
        {
            "submitted" => SubmissionState.Submitted,
            "draft" => SubmissionState.Draft,
            _ => SubmissionState.NotSubmitted,
        };
    }

    /// <summary>Tên lưu trong lms.json và lms-submissions.json (vắng hoặc lạ = Unknown).</summary>
    public static string Key(SubmissionState s) => s switch
    {
        SubmissionState.NotSubmitted => "notsubmitted",
        SubmissionState.Draft => "draft",
        SubmissionState.Submitted => "submitted",
        SubmissionState.Graded => "graded",
        _ => "unknown",
    };

    public static SubmissionState Parse(string? key) => key switch
    {
        "notsubmitted" => SubmissionState.NotSubmitted,
        "draft" => SubmissionState.Draft,
        "submitted" => SubmissionState.Submitted,
        "graded" => SubmissionState.Graded,
        _ => SubmissionState.Unknown,
    };

    /// <summary>Đã nộp xong (nộp hoặc đã chấm): không đếm, không nhắc.</summary>
    public static bool IsDone(SubmissionState s) => s is SubmissionState.Submitted or SubmissionState.Graded;

    /// <summary>
    /// Hỏi trạng thái các bài trong <paramref name="asks"/>, trả map từ assignid sang trạng thái và cập nhật <paramref name="cache"/> (khóa cmid, mỗi mục
    /// {state, at}; bài không có cmid thì hỏi nhưng không cache). Chỉ hỏi bài có hạn từ <c>now - WindowBefore</c> trở đi và chưa Graded trong cache;
    /// bài Submitted trong cache dưới <see cref="SubmittedRecheck"/> thì dùng cache, không hỏi lại. Gọi tuần tự (nhịp gọi do CallAsync giữ).
    /// Lỗi của riêng một bài (<see cref="LmsException"/>, vd. không có quyền) thì bài đó dùng cache cũ nếu có, không thì Unknown (không vào map),
    /// các bài sau vẫn hỏi. Lỗi đường truyền (<see cref="HttpRequestException"/>, <see cref="TimeoutException"/> hay lỗi bỏ qua được khác) thì dừng hỏi tiếp.
    /// Cả bước chỉ báo <paramref name="onError"/> một lần. Lỗi không bỏ qua được (hết phiên, hủy) thì ném tiếp.
    /// </summary>
    public static async Task<Dictionary<long, SubmissionState>> ResolveAsync(IEnumerable<SubmissionAsk> asks, JsonObject cache, long now,
        Func<long, CancellationToken, Task<JsonNode?>> fetch, Func<Exception, bool> recoverable, Action<Exception> onError, CancellationToken ct)
    {
        var result = new Dictionary<long, SubmissionState>();
        var kept = new JsonObject();
        var stop = false;
        var warned = false;
        foreach (var ask in asks)
        {
            if (ask.Due < now - AssignPairing.WindowBefore) continue;
            var key = ask.Cmid?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var old = key is null ? null : cache[key] as JsonObject;
            var oldState = Parse(old?["state"]?.ToString());
            // Mục cache hỏng (at không phải số) coi như thiếu: hỏi lại, không làm hỏng cả bước.
            var oldAt = old?["at"] is JsonValue v && v.TryGetValue<long>(out var at) ? at : 0;
            var reuse = oldState == SubmissionState.Graded || (oldState == SubmissionState.Submitted && now - oldAt < SubmittedRecheck);
            if (!reuse && !stop)
            {
                try
                {
                    var state = FromMoodle(await fetch(ask.AssignId, ct) as JsonObject);
                    if (state != SubmissionState.Unknown)
                    {
                        result[ask.AssignId] = state;
                        if (key is not null) kept[key] = new JsonObject { ["state"] = Key(state), ["at"] = now };
                        continue;
                    }
                }
                catch (Exception e) when (recoverable(e))
                {
                    // Lỗi riêng của bài (LMS trả exception) không chặn bài khác; lỗi đường truyền thì dừng cả bước.
                    if (e is not LmsException) stop = true;
                    if (!warned) { warned = true; onError(e); }
                }
            }
            if (oldState != SubmissionState.Unknown)
            {
                result[ask.AssignId] = oldState;
                if (key is not null) kept[key] = old!.DeepClone();
            }
        }
        cache.Clear();
        foreach (var p in kept.ToList()) { kept.Remove(p.Key); cache[p.Key] = p.Value; }
        return result;
    }
}
