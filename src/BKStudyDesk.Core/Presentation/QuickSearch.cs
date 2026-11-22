using SoHocTap.Files;

namespace SoHocTap.Presentation;

public sealed record QuickItem(string Title, string Sub, string Target);

public static class QuickSearch
{
    public static IReadOnlyList<QuickItem> Match(IReadOnlyList<QuickItem> all, string query, int limit = 8)
    {
        var q = NameMatch.Fold(query.Trim());
        if (q.Length == 0) return [.. all.Take(limit)];
        return [.. all.Select(i => (Item: i, Rank: Rank(i, q))).Where(x => x.Rank >= 0).OrderBy(x => x.Rank).Take(limit).Select(x => x.Item)];
    }

    private static int Rank(QuickItem i, string q)
    {
        var title = NameMatch.Fold(i.Title);
        if (title.StartsWith(q, StringComparison.Ordinal)) return 0;
        if (title.Contains(" " + q, StringComparison.Ordinal)) return 1;
        if (title.Contains(q, StringComparison.Ordinal)) return 2;
        return NameMatch.Fold(i.Sub).Contains(q, StringComparison.Ordinal) ? 3 : -1;
    }
}
