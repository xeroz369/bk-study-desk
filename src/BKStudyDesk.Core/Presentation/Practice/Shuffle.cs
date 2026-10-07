using System.Text.RegularExpressions;

namespace SoHocTap.Presentation.Practice;

/// <summary>
/// Xáo câu và phương án khi luyện, thi thử (port <c>shuffle.ts</c>). Đáp án luôn lưu theo chỉ số GỐC của phương án,
/// nên chấm điểm và kết quả đã lưu không đổi. Bộ sinh số ngẫu nhiên cho cùng dãy với bản TS.
/// </summary>
public static partial class Shuffle
{
    // Phương án nhắc tới phương án khác phải giữ chỗ ("Cả A và B", "Các đáp án khác đều sai"...).
    [GeneratedRegex(@"^\s*(tất cả|tat ca|các (đáp án|phương án|câu)|cac (dap an|phuong an)|cả\s+\S+\s+(và|lẫn)|ca\s+\S+\s+va|không (có )?(đáp án|phương án|câu) nào|none of|all of|both\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Positional();

    public static bool IsPositional(string optionHtml) => Positional().IsMatch(Text.PlainText(optionHtml));

    /// <summary>LCG có seed: thứ tự giữ nguyên khi trang vẽ lại. Ra số trong [0, 1).</summary>
    public static Func<double> Rng(uint seed)
    {
        var s = seed == 0 ? 1u : seed;
        return () =>
        {
            s = unchecked(s * 1664525u + 1013904223u);
            return s / 4294967296.0;
        };
    }

    /// <summary>Seed của một câu: cùng seed trang và id câu thì cùng thứ tự phương án, dù thứ tự câu đã xáo lại.</summary>
    public static uint SeedFor(uint seed, string id)
    {
        var h = seed;
        foreach (var c in id) h = unchecked((h ^ c) * 16777619u);
        return h;
    }

    /// <summary>Fisher-Yates trên bản sao, không đổi danh sách gốc.</summary>
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

    /// <summary>Thứ tự hiện phương án (vị trí hiện thành chỉ số gốc). Giữ nguyên khi keepOrder hay dưới 3 phương án.</summary>
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
