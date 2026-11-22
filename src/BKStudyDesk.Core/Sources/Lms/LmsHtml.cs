using System.Text.RegularExpressions;

namespace SoHocTap.Sources.Lms;

public static partial class LmsHtml
{
    [GeneratedRegex(@"<!--[\s\S]*?-->")] private static partial Regex Comments();
    [GeneratedRegex(@"\s(style|class|lang|align|dir|face|size|color|width|height|valign|border|cellpadding|cellspacing|data-[\w-]+)\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase)] private static partial Regex Attrs();
    [GeneratedRegex(@"</?(span|font|o:p|w:[\w-]+|m:[\w-]+)\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex Wrappers();
    [GeneratedRegex(@"<(style|xml)\b[\s\S]*?</\1\s*>", RegexOptions.IgnoreCase)] private static partial Regex Embedded();
    [GeneratedRegex(@"<[^<>]+>")] private static partial Regex Tag();

    public static string Slim(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var s = Embedded().Replace(html, "");
        s = Comments().Replace(s, "");
        s = Wrappers().Replace(s, "");
        return Tag().Replace(s, t => Attrs().Replace(t.Value, ""));
    }
}
