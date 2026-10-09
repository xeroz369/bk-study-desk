using System.Text.Json.Nodes;
using SoHocTap.Core;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

// Trạng thái nộp bài thật của LMS (mod_assign_get_submission_status), thay cho suy đoán "mốc biến khỏi lịch là đã nộp".
public sealed partial class LmsSource
{
    private static string SubmissionsFile => Paths.DataFile("lms-submissions.json");

    /// <summary>
    /// Hỏi trạng thái từng bài (một request mỗi bài, tuần tự; lms-submissions.json cache theo cmid, kết quả trả theo assignid) rồi gán lên mục mốc:
    /// "state" và "done". Biết trạng thái thì done theo trạng thái (Submitted, Graded là xong); chưa biết thì giữ suy đoán cũ.
    /// Lỗi API chỉ ghi một dòng WARN cho cả bước, các bài chưa hỏi được để nguyên (Unknown).
    /// </summary>
    private static async Task ApplySubmissionStatesAsync(List<(SubmissionAsk Ask, JsonObject Item)> items, CancellationToken ct)
    {
        var cache = JsonStore.ReadObject(SubmissionsFile);
        var states = await SubmissionStates.ResolveAsync(items.Select(i => i.Ask), cache, Now(),
            (id, token) => CallAsync("mod_assign_get_submission_status", [Arg("assignid", id)], token)!,
            e => Recoverable(e, ct), e => Warn("trạng thái nộp bài", e), ct);
        foreach (var (ask, item) in items)
        {
            if (!states.TryGetValue(ask.AssignId, out var s)) continue;
            item["state"] = SubmissionStates.Key(s);
            item["done"] = SubmissionStates.IsDone(s) ? true : null;
        }
        // Ghi cache sau cùng: ghi lỗi (đã được JsonStore xử lý) không làm mất trạng thái vừa gán.
        JsonStore.Write(SubmissionsFile, cache);
    }
}
