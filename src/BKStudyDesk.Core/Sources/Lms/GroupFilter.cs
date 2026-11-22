using System.Text.RegularExpressions;

namespace SoHocTap.Sources.Lms;

public static class GroupFilter
{
    public static IReadOnlyList<string> Codes(string name, string pattern)
    {
        return Matcher(pattern)(name);
    }

    public static Func<string, IReadOnlyList<string>> Matcher(string pattern)
    {
        if (pattern.Length == 0) return _ => [];
        var re = new Regex($"(?<![A-Za-z0-9])(?:{pattern})(?![A-Za-z0-9])");
        return name => name.Length == 0 ? [] : [.. re.Matches(name).Select(m => m.Value).Distinct()];
    }

    public static bool Keep(IReadOnlySet<string> codesInName, IReadOnlySet<string>? myGroups) =>
        codesInName.Count == 0 || myGroups is not { Count: > 0 } || codesInName.Overlaps(myGroups);
}
