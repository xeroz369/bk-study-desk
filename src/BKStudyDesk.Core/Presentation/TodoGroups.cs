using System.Text.RegularExpressions;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>
/// Gom việc giống nhau ở thẻ Cần làm: cùng tên gốc, cùng ngày, cùng hạn (ví dụ khảo sát học phần của 4 môn) thành một dòng
/// "KHẢO SÁT HỌC PHẦN (4 môn)", bấm thì xổ ra từng môn. Tên gốc: bỏ mã kỳ "[261] " đầu tên, tên song ngữ "VI/ EN" lấy phần tiếng Việt.
/// </summary>
public static partial class TodoGroups
{
    public const int MinGroup = 3;

    [GeneratedRegex(@"^\s*\[\d+\]\s*")] private static partial Regex TermTag();

    /// <summary>Tên gọn để hiện: bỏ "[261] " đầu tên.</summary>
    public static string Clean(string title) => TermTag().Replace(title, "");

    /// <summary>Tên gốc để gom: bỏ mã kỳ, lấy phần trước "/ " (tên song ngữ), cắt khoảng trắng. Ngắn quá thì không gom.</summary>
    public static string? Stem(string title)
    {
        var t = Clean(title);
        var slash = t.IndexOf("/ ", StringComparison.Ordinal);
        if (slash < 6) return null;
        return t[..slash].Trim();
    }

    /// <summary>Khóa nhóm: tên gốc và ngày (không theo chữ "còn N giờ" vì chữ đó đổi mỗi giờ, nhóm đang xổ sẽ tự thu lại).</summary>
    private static string? KeyOf(TodoItem t) => Stem(t.Title) is { } s ? $"grp:{s}|{t.Day:yyyyMMdd}" : null;

    /// <param name="expanded">Id các nhóm người dùng đã bấm xổ ra.</param>
    public static IReadOnlyList<TodoItem> Collapse(IReadOnlyList<TodoItem> items, IReadOnlySet<string> expanded)
    {
        var groups = items.Select((t, i) => (t, i, Key: KeyOf(t)))
            .Where(x => x.Key is not null).GroupBy(x => x.Key!).Where(g => g.Count() >= MinGroup).ToDictionary(g => g.Key, g => g.ToList());
        var output = new List<TodoItem>();
        var done = new HashSet<string>();
        foreach (var t in items)
        {
            var key = KeyOf(t);
            if (key is null || !groups.TryGetValue(key, out var members))
            {
                output.Add(t with { Title = Clean(t.Title) });
                continue;
            }
            if (!done.Add(key)) continue;
            var open = expanded.Contains(key);
            var subjects = members.Select(m => m.t.Sub.Split(", ")[0]).Distinct();
            output.Add(t with
            {
                Id = key,
                Title = L.F(open ? "home.group.open" : "home.group", Stem(t.Title)!, members.Count),
                Sub = string.Join(", ", subjects),
                Url = null,
                State = "",
                Group = true,
            });
            if (open) output.AddRange(members.Select(m => m.t with { Title = Clean(m.t.Title), Indent = true }));
        }
        return output;
    }
}
