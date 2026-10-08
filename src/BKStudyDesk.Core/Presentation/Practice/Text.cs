using System.Text;
using System.Text.RegularExpressions;

namespace SoHocTap.Presentation.Practice;

/// <summary>Chữ thuần để so sánh (port <c>src/ui/src/lib/study/text.ts</c>). Không dùng để hiển thị.</summary>
public static partial class Text
{
    /// <summary>
    /// Khoảng trắng theo <c>\s</c> và <c>trim()</c> của JavaScript (khác <c>\s</c> của .NET: có U+FEFF, không có U+0085).
    /// Ghi bằng mã số vì U+2028, U+2029 viết thẳng trong mã nguồn C# là xuống dòng.
    /// </summary>
    public static readonly char[] JsSpaces =
    [
        .. new[] { 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x20, 0xA0, 0x1680 }.Select(c => (char)c),
        .. Enumerable.Range(0x2000, 11).Select(c => (char)c),
        .. new[] { 0x2028, 0x2029, 0x202F, 0x205F, 0x3000, 0xFEFF }.Select(c => (char)c),
    ];

    /// <summary>Lớp ký tự regex khớp một khoảng trắng kiểu JavaScript.</summary>
    public static readonly string JsWhitespace = "[" + string.Concat(JsSpaces.Select(c => @"\u" + ((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture))) + "]";

    private static readonly Regex Spaces = new(JsWhitespace + "+", RegexOptions.Compiled);

    [GeneratedRegex("<[^<>]*>")] private static partial Regex Tag();

    /// <summary>Bỏ thẻ lặp lại tới khi hết (chuỗi kiểu "&lt;scr&lt;script&gt;ipt&gt;" không còn sót thẻ), rồi bỏ mọi dấu &lt; &gt; còn lại.</summary>
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

    /// <summary>NFC, chữ thường, gộp khoảng trắng, giữ dấu tiếng Việt (câu điền, tên chương, bài khi ghép gói).</summary>
    public static string NormText(string s) =>
        TrimJs(Spaces.Replace(s.Normalize(NormalizationForm.FormC).ToLowerInvariant(), " "));

    /// <summary><c>String.prototype.trim</c> của JavaScript.</summary>
    public static string TrimJs(string s) => s.Trim(JsSpaces);

    /// <summary>Thay mọi đoạn khoảng trắng kiểu JavaScript bằng <paramref name="with"/>.</summary>
    public static string CollapseJs(string s, string with) => Spaces.Replace(s, with);
}
