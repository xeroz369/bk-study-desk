using System.Globalization;
using System.Text;

namespace SoHocTap.Presentation.Practice;

public abstract record HtmlNode;

/// <summary>Chữ (đã giải entity).</summary>
public sealed record HtmlText(string Text) : HtmlNode;

public sealed record HtmlElement(string Tag) : HtmlNode
{
    /// <summary>Thuộc tính theo thứ tự trong nguồn, tên chữ thường.</summary>
    public List<KeyValuePair<string, string>> Attributes { get; } = [];
    public List<HtmlNode> Children { get; } = [];

    public string? Attr(string name) => Attributes.FirstOrDefault(a => a.Key == name) is { Key: not null } a ? a.Value : null;

    public string[] Classes => (Attr("class") ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>
/// Bộ đọc HTML nhỏ, tự viết (spec mục 4b): đủ cho HTML của bài học, gói và trang xem lại quiz Moodle. Đọc như trình duyệt ở
/// những chỗ hay gặp: "&lt;" không theo sau bởi chữ cái là chữ thường (công thức <c>\(f(0)&lt;0\)</c>), thẻ không đóng thì tự đóng,
/// li, tr, td tự đóng nhau, khối mới đóng p đang mở. Không ném lỗi với HTML hỏng.
/// </summary>
public static class Html
{
    private static readonly HashSet<string> Void = ["br", "img", "hr", "input", "meta", "link", "area", "base", "col", "embed", "source", "track", "wbr"];
    private static readonly HashSet<string> RawText = ["script", "style"];
    private static readonly HashSet<string> ClosesP = ["p", "div", "ul", "ol", "table", "pre", "blockquote", "hr", "h1", "h2", "h3", "h4", "h5", "h6"];

    // Thẻ bị bỏ cả nội dung khi lọc (như sanitize.ts).
    private static readonly HashSet<string> DropWithContent =
    [
        "script", "style", "iframe", "object", "embed", "template", "noscript", "svg", "math", "video", "audio",
        "form", "input", "textarea", "button", "select", "link", "meta", "base",
    ];

    private static readonly char Nbsp = (char)0xA0;

    private static readonly Dictionary<string, string> Entities = new()
    {
        ["amp"] = "&", ["lt"] = "<", ["gt"] = ">", ["quot"] = "\"", ["apos"] = "'", ["nbsp"] = Nbsp.ToString(),
        ["ndash"] = ((char)0x2013).ToString(), ["mdash"] = ((char)0x2014).ToString(), ["hellip"] = ((char)0x2026).ToString(),
        ["times"] = ((char)0xD7).ToString(), ["divide"] = ((char)0xF7).ToString(), ["deg"] = ((char)0xB0).ToString(),
        ["plusmn"] = ((char)0xB1).ToString(), ["le"] = ((char)0x2264).ToString(), ["ge"] = ((char)0x2265).ToString(),
        ["ne"] = ((char)0x2260).ToString(), ["minus"] = ((char)0x2212).ToString(), ["middot"] = ((char)0xB7).ToString(),
        ["laquo"] = ((char)0xAB).ToString(), ["raquo"] = ((char)0xBB).ToString(), ["copy"] = ((char)0xA9).ToString(),
    };

    public static List<HtmlNode> Parse(string html)
    {
        var root = new HtmlElement("#root");
        var stack = new List<HtmlElement> { root };
        HtmlElement Top() => stack[^1];
        var text = new StringBuilder();
        void Flush()
        {
            if (text.Length == 0) return;
            Top().Children.Add(new HtmlText(Decode(text.ToString())));
            text.Clear();
        }
        void CloseTo(string tag)
        {
            var i = stack.FindLastIndex(e => e.Tag == tag);
            if (i > 0) stack.RemoveRange(i, stack.Count - i);
        }
        bool Open(params string[] tags) => stack.Skip(1).Any(e => tags.Contains(e.Tag));

        var s = html;
        var p = 0;
        while (p < s.Length)
        {
            var c = s[p];
            if (c != '<')
            {
                text.Append(c);
                p++;
                continue;
            }
            if (string.CompareOrdinal(s, p, "<!--", 0, 4) == 0)
            {
                Flush();
                var end = s.IndexOf("-->", p + 4, StringComparison.Ordinal);
                p = end < 0 ? s.Length : end + 3;
                continue;
            }
            var next = p + 1 < s.Length ? s[p + 1] : '\0';
            if (next == '!' || next == '?')
            {
                Flush();
                var end = s.IndexOf('>', p);
                p = end < 0 ? s.Length : end + 1;
                continue;
            }
            if (next == '/' && p + 2 < s.Length && char.IsAsciiLetter(s[p + 2]))
            {
                Flush();
                var q = p + 2;
                while (q < s.Length && char.IsAsciiLetterOrDigit(s[q])) q++;
                var name = s[(p + 2)..q].ToLowerInvariant();
                var end = s.IndexOf('>', q);
                p = end < 0 ? s.Length : end + 1;
                CloseTo(name);
                continue;
            }
            if (!char.IsAsciiLetter(next))
            {
                text.Append(c);
                p++;
                continue;
            }
            Flush();
            var (el, after, selfClosed) = ReadTag(s, p + 1);
            p = after;
            var tag = el.Tag;
            if (ClosesP.Contains(tag) && Top().Tag == "p") stack.RemoveAt(stack.Count - 1);
            if (tag == "li" && Open("li")) CloseTo("li");
            if (tag == "tr" && Open("tr")) CloseTo("tr");
            if (tag is "td" or "th" && Open("td", "th")) { CloseTo("td"); CloseTo("th"); }
            Top().Children.Add(el);
            if (Void.Contains(tag) || selfClosed) continue;
            if (RawText.Contains(tag))
            {
                var close = s.IndexOf("</" + tag, p, StringComparison.OrdinalIgnoreCase);
                var body = close < 0 ? s[p..] : s[p..close];
                if (body.Length > 0) el.Children.Add(new HtmlText(body));
                var end = close < 0 ? -1 : s.IndexOf('>', close);
                p = end < 0 ? s.Length : end + 1;
                continue;
            }
            stack.Add(el);
        }
        Flush();
        return root.Children;
    }

    private static (HtmlElement El, int After, bool SelfClosed) ReadTag(string s, int p)
    {
        var q = p;
        while (q < s.Length && (char.IsAsciiLetterOrDigit(s[q]) || s[q] == '-')) q++;
        var el = new HtmlElement(s[p..q].ToLowerInvariant());
        p = q;
        while (p < s.Length)
        {
            while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
            if (p >= s.Length) break;
            if (s[p] == '>') return (el, p + 1, false);
            if (s[p] == '/' && p + 1 < s.Length && s[p + 1] == '>') return (el, p + 2, true);
            if (s[p] == '/') { p++; continue; }
            var n = p;
            while (n < s.Length && !char.IsWhiteSpace(s[n]) && s[n] is not ('=' or '>' or '/')) n++;
            if (n == p) { p++; continue; }
            var name = s[p..n].ToLowerInvariant();
            p = n;
            while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
            var value = "";
            if (p < s.Length && s[p] == '=')
            {
                p++;
                while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
                if (p < s.Length && s[p] is '"' or '\'')
                {
                    var quote = s[p];
                    var end = s.IndexOf(quote, p + 1);
                    if (end < 0) end = s.Length;
                    value = s[(p + 1)..end];
                    p = Math.Min(end + 1, s.Length);
                }
                else
                {
                    var v = p;
                    while (v < s.Length && !char.IsWhiteSpace(s[v]) && s[v] != '>') v++;
                    value = s[p..v];
                    p = v;
                }
            }
            if (el.Attr(name) is null) el.Attributes.Add(new(name, Decode(value)));
        }
        return (el, s.Length, false);
    }

    /// <summary>Giải entity có tên thường gặp và entity số; entity lạ giữ nguyên.</summary>
    public static string Decode(string text)
    {
        if (!text.Contains('&')) return text;
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '&')
            {
                var semi = text.IndexOf(';', i + 1);
                if (semi > i + 1 && semi - i <= 12)
                {
                    var name = text[(i + 1)..semi];
                    string? value = null;
                    if (name.StartsWith('#'))
                    {
                        var hex = name.Length > 1 && name[1] is 'x' or 'X';
                        if (int.TryParse(hex ? name[2..] : name[1..], hex ? NumberStyles.HexNumber : NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
                            && code is > 0 and <= 0x10FFFF)
                            value = char.ConvertFromUtf32(code);
                    }
                    else Entities.TryGetValue(name, out value);
                    if (value != null)
                    {
                        sb.Append(value);
                        i = semi;
                        continue;
                    }
                }
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    /// <summary>Escape chữ như innerHTML của trình duyệt: &amp;, &lt;, &gt; và dấu cách cứng.</summary>
    public static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace(Nbsp.ToString(), "&nbsp;");

    private static string EscapeAttr(string v) => v.Replace("&", "&amp;").Replace("\"", "&quot;").Replace(Nbsp.ToString(), "&nbsp;");

    public static string Serialize(IEnumerable<HtmlNode> nodes)
    {
        var sb = new StringBuilder();
        foreach (var n in nodes) Write(sb, n);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, HtmlNode n)
    {
        if (n is HtmlText t)
        {
            sb.Append(Escape(t.Text));
            return;
        }
        var e = (HtmlElement)n;
        sb.Append('<').Append(e.Tag);
        foreach (var (k, v) in e.Attributes) sb.Append(' ').Append(k).Append("=\"").Append(EscapeAttr(v)).Append('"');
        sb.Append('>');
        if (Void.Contains(e.Tag)) return;
        foreach (var c in e.Children) Write(sb, c);
        sb.Append("</").Append(e.Tag).Append('>');
    }

    /// <summary>Chữ thuần của các node (đã giải entity).</summary>
    public static string TextOf(IEnumerable<HtmlNode> nodes) => string.Concat(nodes.Select(TextOf));

    /// <summary>Mọi phần tử con cháu theo thứ tự trong tài liệu (như querySelectorAll('*')).</summary>
    public static IEnumerable<HtmlElement> Elements(IEnumerable<HtmlNode> nodes)
    {
        foreach (var n in nodes)
            if (n is HtmlElement e)
            {
                yield return e;
                foreach (var d in Elements(e.Children)) yield return d;
            }
    }

    /// <summary>Phần tử đầu tiên có class này (như querySelector('.x')).</summary>
    public static HtmlElement? FirstByClass(IEnumerable<HtmlNode> nodes, string cls) => Elements(nodes).FirstOrDefault(e => e.Classes.Contains(cls));

    /// <summary>innerHTML của phần tử.</summary>
    public static string InnerHtml(HtmlElement e) => Serialize(e.Children);

    public static string TextOf(HtmlNode n) => n switch
    {
        HtmlText t => t.Text,
        HtmlElement e => TextOf(e.Children),
        _ => "",
    };

    /// <summary>
    /// Lọc HTML của gói trước khi hiển thị (port <c>sanitize.ts</c>): gói có thể đến từ người khác nên không tin nội dung.
    /// Giữ thẻ trong <see cref="StudyPack.AllowedTags"/>, bỏ mọi thuộc tính trừ class (lọc theo <see cref="StudyPack.AllowedClasses"/>),
    /// colspan, rowspan; thẻ lạ bỏ thẻ giữ chữ; script, iframe... bỏ cả nội dung; ảnh chỉ nhận data URI raster.
    /// </summary>
    /// <param name="headings">Trang tĩnh (lộ trình): giữ thêm h2, h3.</param>
    public static string Sanitize(string html, bool headings = false) => Serialize(Clean(Parse(html), headings));

    private static List<HtmlNode> Clean(List<HtmlNode> nodes, bool headings)
    {
        var output = new List<HtmlNode>();
        foreach (var n in nodes)
        {
            if (n is not HtmlElement e)
            {
                output.Add(n);
                continue;
            }
            if (DropWithContent.Contains(e.Tag)) continue;
            var children = Clean(e.Children, headings);
            if (!StudyPack.AllowedTags.Contains(e.Tag) && !(headings && e.Tag is "h2" or "h3"))
            {
                output.AddRange(children);
                continue;
            }
            if (e.Tag == "img" && !StudyPack.ImgSrc().IsMatch(e.Attr("src") ?? "")) continue;
            var kept = new HtmlElement(e.Tag);
            foreach (var (name, value) in e.Attributes)
            {
                if (e.Tag == "img" && (name is "src" or "alt" || (name is "width" or "height" && IsDigits(value, 4))))
                    kept.Attributes.Add(new(name, value));
                else if (name == "class")
                {
                    var keep = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Where(StudyPack.AllowedClasses.Contains).ToList();
                    if (keep.Count > 0) kept.Attributes.Add(new("class", string.Join(" ", keep)));
                }
                else if (name is "colspan" or "rowspan" && IsDigits(value, 2)) kept.Attributes.Add(new(name, value));
            }
            kept.Children.AddRange(children);
            output.Add(kept);
        }
        return output;
    }

    private static bool IsDigits(string s, int max) => s.Length is > 0 && s.Length <= max && s.All(char.IsAsciiDigit);
}
