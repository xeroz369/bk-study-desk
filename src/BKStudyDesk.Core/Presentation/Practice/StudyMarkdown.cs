using System.Text;
using System.Text.RegularExpressions;

namespace SoHocTap.Presentation.Practice;

/// <summary>
/// Study Markdown (port <c>markdown.ts</c>, spec <c>studypack/SPEC.md</c> mục 2): định dạng nội dung duy nhất của app,
/// cho bài học, gói chia sẻ, đề thi thử. # chương, ## bài, ### câu, - [x] phương án, = đáp án, &gt; lời giải.
/// </summary>
public static partial class StudyMarkdown
{
    // Dấu giữ chỗ cho công thức, mã trong dòng: ký tự 0 không bao giờ có trong nội dung thật.
    private static readonly string Mark = ((char)0).ToString();

    [GeneratedRegex(@"\\\[[\s\S]*?\\\]|\$\$[\s\S]*?\$\$|\\\([\s\S]*?\\\)|`[^`\n]+`")] private static partial Regex Protected();
    [GeneratedRegex(@"!\[([^\]]*)\]\(([^)\s]+)\)")] private static partial Regex Image();
    [GeneratedRegex(@"\*\*([^*]+)\*\*")] private static partial Regex Bold();
    [GeneratedRegex(@"(^|[^*\w])\*([^*\n]+)\*(?!\w)", RegexOptions.ECMAScript)] private static partial Regex Italic();
    [GeneratedRegex(" {2,}$", RegexOptions.Multiline)] private static partial Regex HardBreak();
    [GeneratedRegex(@"^\s*\|.*\|\s*$")] private static partial Regex TableRow();
    [GeneratedRegex(@"^\s*\|[\s:|-]+\|\s*$")] private static partial Regex TableSep();
    [GeneratedRegex(@"^\||\|$")] private static partial Regex TableEdge();
    [GeneratedRegex(@"^\s*[-*]\s+(?!\[[ xX]\])")] private static partial Regex Ul();
    [GeneratedRegex(@"^\s*\d+[.)]\s+")] private static partial Regex Ol();
    [GeneratedRegex(@"^\s*<(div|table|ul|ol|p|pre|blockquote)\b", RegexOptions.IgnoreCase)] private static partial Regex HtmlBlock();
    [GeneratedRegex(@"^(#{2,3})\s+(.*)$")] private static partial Regex PageHeading();
    [GeneratedRegex(@"[^a-z0-9]+")] private static partial Regex NonSlug();

    private static Regex HeldOnly { get; } = new("^\\s*" + Mark + "\\d+" + Mark + "\\s*$");
    private static Regex Held { get; } = new(Mark + "(\\d+)" + Mark);

    /// <summary>
    /// Vẽ tập con Markdown của gói ra HTML. TeX (\( \), \[ \], $$ $$) và HTML thô giữ nguyên; ảnh đổi đường dẫn qua
    /// <paramref name="img"/>. Kết quả vẫn phải lọc (<see cref="Html.Sanitize"/>) trước khi hiển thị.
    /// <paramref name="headings"/>: trang tĩnh cho phép ## và ### thành tiêu đề (bài học và gói thì không, # là cấu trúc).
    /// </summary>
    public static string MdToHtml(string md, Func<string, string>? img = null, bool headings = false)
    {
        img ??= s => s;
        var keep = new List<string>();
        string Hold(string s)
        {
            keep.Add(s);
            return Mark + (keep.Count - 1) + Mark;
        }
        var t = md.Replace("\r", "");
        // Giữ công thức và mã trong dòng trước, để * _ | bên trong không bị đụng tới.
        t = Protected().Replace(t, m => Hold(m.Value.StartsWith('`') ? $"<code>{EscapeHtml(m.Value[1..^1])}</code>" : m.Value));
        string Inline(string s)
        {
            s = Image().Replace(s, m => Hold($"<img src=\"{img(m.Groups[2].Value)}\" alt=\"{EscapeAttr(m.Groups[1].Value)}\">"));
            s = Bold().Replace(s, "<b>$1</b>");
            s = Italic().Replace(s, "$1<i>$2</i>");
            return HardBreak().Replace(s, "<br>");
        }
        var output = new StringBuilder();
        var lines = t.Split('\n');
        for (var i = 0; i < lines.Length;)
        {
            var line = lines[i];
            if (line.Trim().Length == 0)
            {
                i++;
                continue;
            }
            if (TableRow().IsMatch(line) && i + 1 < lines.Length && TableSep().IsMatch(lines[i + 1]))
            {
                List<string> Row(string r) => TableEdge().Replace(r.Trim(), "").Split('|').Select(c => Inline(c.Trim())).ToList();
                var head = Row(line);
                i += 2;
                var body = new List<List<string>>();
                while (i < lines.Length && TableRow().IsMatch(lines[i])) body.Add(Row(lines[i++]));
                output.Append("<table><thead><tr>").Append(string.Concat(head.Select(c => $"<th>{c}</th>"))).Append("</tr></thead><tbody>")
                    .Append(string.Concat(body.Select(r => "<tr>" + string.Concat(r.Select(c => $"<td>{c}</td>")) + "</tr>")))
                    .Append("</tbody></table>");
                continue;
            }
            if (Ul().IsMatch(line) || Ol().IsMatch(line))
            {
                var ordered = Ol().IsMatch(line);
                var re = ordered ? Ol() : Ul();
                var items = new List<string>();
                while (i < lines.Length && re.IsMatch(lines[i])) items.Add(Inline(re.Replace(lines[i++], "", 1)));
                var tag = ordered ? "ol" : "ul";
                output.Append('<').Append(tag).Append('>').Append(string.Concat(items.Select(x => $"<li>{x}</li>"))).Append("</").Append(tag).Append('>');
                continue;
            }
            // Khối HTML thô hay công thức riêng dòng là một khối riêng.
            if (HtmlBlock().IsMatch(line) || HeldOnly.IsMatch(line))
            {
                var block = new List<string>();
                while (i < lines.Length && lines[i].Trim().Length > 0) block.Add(lines[i++]);
                var s = string.Join("\n", block);
                output.Append(HeldOnly.IsMatch(s) ? $"<div class=\"math\">{s.Trim()}</div>" : s);
                continue;
            }
            if (headings && PageHeading().Match(line) is { Success: true } h)
            {
                var tag = h.Groups[1].Value.Length == 2 ? "h2" : "h3";
                output.Append('<').Append(tag).Append('>').Append(Inline(h.Groups[2].Value.Trim())).Append("</").Append(tag).Append('>');
                i++;
                continue;
            }
            var para = new List<string>();
            while (i < lines.Length && lines[i].Trim().Length > 0 && !Ul().IsMatch(lines[i]) && !Ol().IsMatch(lines[i]) && !TableRow().IsMatch(lines[i])
                   && !(headings && PageHeading().IsMatch(lines[i])))
                para.Add(lines[i++]);
            output.Append("<p>").Append(Inline(string.Join("\n", para))).Append("</p>");
        }
        return Held.Replace(output.ToString(), m => keep[int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)]);
    }

    private static string EscapeHtml(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    private static string EscapeAttr(string s) => EscapeHtml(s).Replace("\"", "&quot;");

    /// <summary>Id từ tên: bỏ dấu, đ thành d, chữ thường, ký tự khác thành gạch ngang, tối đa 48 ký tự.</summary>
    public static string Slug(string s, string fallback)
    {
        var d = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (c is < (char)0x300 or > (char)0x36F) d.Append(c is 'đ' or 'Đ' ? 'd' : c);
        var output = NonSlug().Replace(d.ToString().ToLowerInvariant(), "-").Trim('-');
        if (output.Length > 48) output = output[..48];
        return output.Length >= 2 ? output : fallback;
    }
}
