using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SoHocTap.Presentation.Practice;

public static partial class Fingerprint
{
    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex Img();
    [GeneratedRegex("<[^>]+>")] private static partial Regex Tag();
    [GeneratedRegex(@"\\[,;: !]")] private static partial Regex TexSpace();

    public static string FpText(string html)
    {
        var s = Img().Replace(html, " [img] ");
        s = Tag().Replace(s, " ")
            .Replace("&nbsp;", " ").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&")
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();
        s = TexSpace().Replace(s, "");
        return Text.CollapseJs(s, "");
    }

    private static string Hash(string s)
    {
        uint a = 0x811c9dc5, b = 0x01000193 ^ 0x5bd1e995;
        foreach (var c in s)
        {
            a = unchecked((a ^ c) * 0x01000193);
            b = unchecked((b ^ c) * 0x5bd1e995);
        }
        return a.ToString("x8", CultureInfo.InvariantCulture) + b.ToString("x8", CultureInfo.InvariantCulture);
    }

    public static string Of(string? type, string prompt, IEnumerable<string>? options, double? numericKey, IEnumerable<string>? accept)
    {
        var opts = (options ?? []).Select(FpText).Order(StringComparer.Ordinal);
        var key = type switch
        {
            "numeric" => numericKey is { } n ? JsNumber.Format(n) : "undefined",
            "short" => string.Join("|", (accept ?? []).Select(FpText).Order(StringComparer.Ordinal)),
            _ => "",
        };
        return Hash(string.Join("\u0001", [FpText(prompt), .. opts, key]));
    }

    public static string Of(Question q) => Of(q.Type, q.Prompt, q.Options, q.Value ?? q.Answer, q.Accept);
}
