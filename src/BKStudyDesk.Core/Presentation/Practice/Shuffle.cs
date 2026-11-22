using System.Text.RegularExpressions;

namespace SoHocTap.Presentation.Practice;

public static partial class Shuffle
{
    [GeneratedRegex(@"^\s*(tất cả|tat ca|các (đáp án|phương án|câu)|cac (dap an|phuong an)|cả\s+\S+\s+(và|lẫn)|ca\s+\S+\s+va|không (có )?(đáp án|phương án|câu) nào|none of|all of|both\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Positional();

    public static bool IsPositional(string optionHtml) => Positional().IsMatch(Text.PlainText(optionHtml));

    public static Func<double> Rng(uint seed)
    {
        var s = seed == 0 ? 1u : seed;
        return () =>
        {
            s = unchecked(s * 1664525u + 1013904223u);
            return s / 4294967296.0;
        };
    }

    public static uint SeedFor(uint seed, string id)
    {
        var h = seed;
        foreach (var c in id) h = unchecked((h ^ c) * 16777619u);
        return h;
    }

    public static T[] Shuffled<T>(IReadOnlyList<T> items, Func<double> rand)
    {
        var a = items.ToArray();
        for (var i = a.Length - 1; i > 0; i--)
        {
            var j = (int)Math.Floor(rand() * (i + 1));
            (a[i], a[j]) = (a[j], a[i]);
        }
        return a;
    }

    public static int[] OptionOrder(Question q, Func<double> rand)
    {
        var idx = Enumerable.Range(0, q.Options.Count).ToArray();
        if (q.KeepOrder == true || q.Options.Count < 3) return idx;
        var positional = idx.Select(i => IsPositional(q.Options[i])).ToArray();
        var mixed = Shuffled(idx.Where(i => !positional[i]).ToArray(), rand);
        var k = 0;
        return idx.Select(i => positional[i] ? i : mixed[k++]).ToArray();
    }
}
