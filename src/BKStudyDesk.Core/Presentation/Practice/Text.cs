using System.Text;
using System.Text.RegularExpressions;

namespace SoHocTap.Presentation.Practice;

public static partial class Text
{
    public static readonly char[] JsSpaces =
    [
        .. new[] { 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x20, 0xA0, 0x1680 }.Select(c => (char)c),
        .. Enumerable.Range(0x2000, 11).Select(c => (char)c),
        .. new[] { 0x2028, 0x2029, 0x202F, 0x205F, 0x3000, 0xFEFF }.Select(c => (char)c),
    ];

    public static readonly string JsWhitespace = "[" + string.Concat(JsSpaces.Select(c => @"\u" + ((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture))) + "]";

    private static readonly Regex Spaces = new(JsWhitespace + "+", RegexOptions.Compiled);

    [GeneratedRegex("<[^<>]*>")] private static partial Regex Tag();

    public static string PlainText(string html)
    {
        var s = html;
        string prev;
        do
        {
            prev = s;
            s = Tag().Replace(s, "");
        } while (s != prev);
        return s.Replace("<", "").Replace(">", "");
    }

    public static string NormText(string s) =>
        TrimJs(Spaces.Replace(s.Normalize(NormalizationForm.FormC).ToLowerInvariant(), " "));

    public static string TrimJs(string s) => s.Trim(JsSpaces);

    public static string CollapseJs(string s, string with) => Spaces.Replace(s, with);
}
