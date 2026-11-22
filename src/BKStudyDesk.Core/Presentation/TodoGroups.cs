using System.Text.RegularExpressions;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public static partial class TodoGroups
{
    public const int MinGroup = 3;

    [GeneratedRegex(@"^\s*\[\d+\]\s*")] private static partial Regex TermTag();

    public static string Clean(string title) => TermTag().Replace(title, "");

    public static string? Stem(string title)
    {
        var t = Clean(title);
        var slash = t.IndexOf("/ ", StringComparison.Ordinal);
        if (slash < 6) return null;
        return t[..slash].Trim();
    }

    private static string? KeyOf(TodoItem t) => Stem(t.Title) is { } s ? $"grp:{s}|{t.Day:yyyyMMdd}" : null;

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
            static string Subject(TodoItem m) => m.Sub.Split(", ")[0];
            var todo = members.Where(m => !m.t.Done).Select(m => Subject(m.t)).Distinct().ToList();
            var finished = members.Where(m => m.t.Done).Select(m => Subject(m.t)).Distinct().ToList();
            var parts = new List<string>();
            if (t.Hour.Length > 0) parts.Add(L.F("home.group.hour", t.Hour));
            if (todo.Count > 0) parts.Add(L.F("home.group.todo", string.Join(", ", todo)));
            if (finished.Count > 0) parts.Add(L.F("home.group.finished", string.Join(", ", finished)));
            var first = members.Select(m => m.t).FirstOrDefault(m => !m.Done) ?? t;
            output.Add(first with
            {
                Id = key,
                Title = L.F(open ? "home.group.open" : "home.group", Stem(t.Title)!, members.Count),
                Sub = string.Join(". ", parts),
                Url = null,
                State = finished.Count == 0 ? "" : L.F("home.group.progress", finished.Count, members.Count),
                Group = true,
                Done = todo.Count == 0,
            });
            if (open) output.AddRange(members.Select(m => m.t with { Title = Clean(m.t.Title), Indent = true }));
        }
        return output;
    }
}
