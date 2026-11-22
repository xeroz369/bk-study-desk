using SoHocTap.Files;

namespace SoHocTap.Library;

public static class LibraryMatch
{
    public static string Key(string? code) => NameMatch.Nfc(code).Trim().ToUpperInvariant();

    public static string LmsCode(string? lmsCode) => Key((lmsCode ?? "").Split('_')[0]);

    public static IReadOnlyList<CourseMatch> FindAll(IEnumerable<string?> userCodes, IReadOnlyList<CourseRef> library)
    {
        var codes = userCodes.Select(Key).Where(c => c.Length > 0).ToHashSet(StringComparer.Ordinal);
        if (codes.Count == 0) return [];
        var direct = library.Select(c => (Course: c, Rank: Rank(c, codes))).Where(x => x.Rank < int.MaxValue)
            .OrderBy(x => x.Rank).ThenBy(x => x.Course.Id, StringComparer.Ordinal).Select(x => x.Course).ToList();
        var byId = library.GroupBy(c => c.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var result = new List<CourseMatch>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in direct)
        {
            if (seen.Add(c.Id)) result.Add(new CourseMatch(c, null));
            var from = c;
            for (var hop = 0; hop < 5 && from.Retired && from.ReplacedBy is { } next && byId.TryGetValue(next, out var repl); hop++)
            {
                if (seen.Add(repl.Id)) result.Add(new CourseMatch(repl, c));
                from = repl;
            }
        }
        return result;
    }

    private static int Rank(CourseRef c, HashSet<string> codes)
    {
        int level;
        if (codes.Contains(Key(c.Id))) level = 0;
        else if (codes.Contains(Key(c.Code))) level = 1;
        else if ((c.Aliases ?? []).Any(a => codes.Contains(Key(a)))) level = 2;
        else return int.MaxValue;
        return level * 2 + (c.Retired ? 1 : 0);
    }

    public static Dictionary<string, IReadOnlyList<CourseMatch>> MatchAll(IEnumerable<(string Subject, IEnumerable<string?> Codes)> subjects, IReadOnlyList<CourseRef> library)
    {
        var map = new Dictionary<string, IReadOnlyList<CourseMatch>>(NameMatch.NfcIgnoreCase);
        foreach (var (subject, codes) in subjects)
            if (FindAll(codes, library) is { Count: > 0 } found) map.TryAdd(subject, found);
        return map;
    }
}

public sealed record CourseMatch(CourseRef Course, CourseRef? ReplacementOf);
