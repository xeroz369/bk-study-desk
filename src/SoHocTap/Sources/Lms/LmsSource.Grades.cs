using System.Text.Json;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using SoHocTap.Files;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

// Sổ điểm LMS của từng lớp.
public sealed partial class LmsSource
{
    /// <summary>
    /// Sổ điểm LMS của từng lớp kỳ này (điểm thành phần: quiz, BTL, giữa kỳ… do giảng viên nhập). Dùng API
    /// gradereport_user_get_grade_items của app mobile, mỗi lớp một request. Lớp nào chưa có mục điểm thì bỏ qua.
    /// </summary>
    private static async Task<JsonArray> CollectGradesAsync(List<CourseInfo> current, long uid, CancellationToken ct)
    {
        // Nhóm của mình (đã ghi lúc lấy mốc, ngay trước bước này). Lớp thí nghiệm có mục điểm của mọi nhóm nên chỉ giữ nhóm mình.
        var groupsJson = JsonStore.ReadObject(GroupsFile);
        var groups = groupsJson.ToDictionary(p => long.Parse(p.Key), p => (p.Value as JsonArray ?? []).Select(x => x!.GetValue<string>()).ToHashSet());
        var list = new JsonArray();
        foreach (var c in current)
        {
            try
            {
                var r = await CallAsync("gradereport_user_get_grade_items", [Arg("courseid", c.Id), Arg("userid", uid)], ct);
                var items = (r["usergrades"]?[0]?["gradeitems"] as JsonArray ?? []).OfType<JsonObject>()
                    .Where(i => ForMyGroup(i["itemname"]?.GetValue<string>() ?? "", c.Id, groups))
                    .Select(i => (JsonNode)new JsonObject
                    {
                        ["name"] = i["itemname"]?.GetValue<string>() is { Length: > 0 } n ? WebUtility.HtmlDecode(n) : "Tổng khóa học",
                        ["kind"] = i["itemtype"]?.GetValue<string>() == "mod" ? i["itemmodule"]?.GetValue<string>() : i["itemtype"]?.GetValue<string>(),
                        ["grade"] = i["graderaw"]?.GetValue<double?>(),
                        ["text"] = i["gradeformatted"]?.GetValue<string>(),
                        ["max"] = i["grademax"]?.GetValue<double?>(),
                        ["percent"] = i["percentageformatted"]?.GetValue<string>(),
                        ["feedback"] = i["feedback"]?.GetValue<string>(),
                    }).ToArray();
                // Chỉ có row tổng (giảng viên chưa tạo mục điểm nào) thì bỏ.
                if (!items.Any(i => i!["kind"]?.GetValue<string>() is not ("course" or "category"))) continue;
                list.Add(new JsonObject { ["course"] = c.Id, ["subject"] = c.Subject, ["part"] = c.Part, ["items"] = new JsonArray(items) });
            }
            catch (Exception e) when (Recoverable(e, ct)) { Warn($"sổ điểm {c.Subject}", e); }
        }
        return list;
    }
}
