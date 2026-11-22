using System.Text.Json.Nodes;
using SoHocTap.Core;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

public sealed partial class LmsSource
{
    private static string SubmissionsFile => Paths.DataFile("lms-submissions.json");

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
        JsonStore.Write(SubmissionsFile, cache);
    }
}
