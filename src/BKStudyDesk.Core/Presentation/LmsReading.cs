using System.Net;
using System.Text.RegularExpressions;
using SoHocTap.Data;

namespace SoHocTap.Presentation;

/// <summary>Một nội dung LMS để đọc trong app (bài thông báo, tin nhắn, trang, đề bài): chữ đã bỏ thẻ, link, file đính kèm, link gốc.</summary>
/// <param name="Blocks">Nội dung theo thứ tự: đoạn chữ và ảnh (ảnh LMS đã tải về thì xem được khi LMS sập). Null: chỉ có Paragraphs.</param>
public sealed record Reading(string Title, string Meta, IReadOnlyList<string> Paragraphs, IReadOnlyList<LmsLink> Links, IReadOnlyList<LmsLink> Attachments, string? WebUrl,
    IReadOnlyList<ReadingBlock>? Blocks = null);

/// <summary>
/// Một khối nội dung: đoạn chữ (Text) hay ảnh (Image: địa chỉ ảnh trong bài). Spans: đoạn chữ có link thì chia thành phần chữ thường
/// và phần link (Url) theo đúng thứ tự, để link nằm ngay trong chữ như trên LMS; không có link thì null. Text luôn là chữ đầy đủ.
/// </summary>
public sealed record ReadingBlock(string? Text, string? Image = null, IReadOnlyList<ReadingSpan>? Spans = null);

/// <summary>Một phần của đoạn chữ: chữ thường (Url null) hay link.</summary>
public sealed record ReadingSpan(string Text, string? Url = null);

/// <summary>
/// HTML của LMS thành đoạn chữ và danh sách link, đọc được khi LMS sập (không tải gì thêm, không chạy script).
/// Không vẽ lại HTML: chỉ giữ chữ theo đoạn, xuống dòng, gạch đầu dòng; link gom xuống cuối để bấm mở (cần mạng).
/// Ảnh trong bài là link pluginfile cần đăng nhập nên không hiện, chỉ ghi [ảnh].
/// </summary>
public static partial class LmsReading
{
    [GeneratedRegex(@"<(script|style)\b[\s\S]*?</\1\s*>", RegexOptions.IgnoreCase)] private static partial Regex Hidden();
    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)] private static partial Regex Br();
    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex Li();
    [GeneratedRegex(@"</?(p|div|h[1-6]|ul|ol|table|tr|blockquote|pre|hr)\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex Block();
    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex Img();
    [GeneratedRegex(@"\bsrc\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)] private static partial Regex Src();
    [GeneratedRegex(@"</t[dh]\s*>", RegexOptions.IgnoreCase)] private static partial Regex Cell();
    [GeneratedRegex(@"<a\b[^>]*\bhref\s*=\s*[""']([^""']+)[""'][^>]*>([\s\S]*?)</a\s*>", RegexOptions.IgnoreCase)] private static partial Regex Anchor();
    [GeneratedRegex(@"[ \t ]+")] private static partial Regex Spaces();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex Blank();

    /// <summary>Đoạn chữ của <paramref name="html"/>; dòng trong một đoạn giữ nguyên (tin nhắn hay xuống dòng). Ảnh ghi [ảnh].</summary>
    public static IReadOnlyList<string> Paragraphs(string? html) => Split(html, images: false).Select(b => b.Text!).ToList();

    /// <summary>Khối nội dung theo thứ tự: mỗi ảnh một khối riêng (để hiện ảnh đã tải về), chữ chia đoạn như <see cref="Paragraphs"/>.</summary>
    public static IReadOnlyList<ReadingBlock> Blocks(string? html) => Split(html, images: true);

    private const char Mark = '\u0001';
    // Link trong chữ: LinkOpen địa chỉ LinkMid chữ LinkClose, giữ qua các bước bỏ thẻ rồi tách thành ReadingSpan ở cuối.
    private const char LinkOpen = '\u0002', LinkMid = '\u0003', LinkClose = '\u0004';

    private static IReadOnlyList<ReadingBlock> Split(string? html, bool images)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];
        var s = Hidden().Replace(html, "");
        s = Img().Replace(s, m => images && Src().Match(m.Value) is { Success: true } src
            ? $"\n\n{Mark}{WebUtility.HtmlDecode(src.Groups[1].Value)}{Mark}\n\n" : " [ảnh] ");
        s = Anchor().Replace(s, m =>
        {
            var url = WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
            var inner = m.Groups[2].Value;
            // Link bọc ảnh hay link lạ (javascript:...) thì để như cũ: chỉ lấy chữ.
            if (!IsWebLink(url) || inner.Contains(Mark) || url.IndexOfAny([LinkOpen, LinkMid, LinkClose]) >= 0) return inner;
            var text = Spaces().Replace(WebUtility.HtmlDecode(Practice.Text.PlainText(Block().Replace(Br().Replace(inner, " "), " "))), " ").Trim();
            if (text.Length == 0) text = url;
            return $"{LinkOpen}{url}{LinkMid}{text.Replace('<', ' ').Replace('>', ' ')}{LinkClose}";
        });
        s = Br().Replace(s, "\n");
        s = Li().Replace(s, "\n• ");
        s = Cell().Replace(s, "  ");
        s = Block().Replace(s, "\n\n");
        s = WebUtility.HtmlDecode(Practice.Text.PlainText(s)).Replace("\r", "");
        s = string.Join("\n", s.Split('\n').Select(l => Spaces().Replace(l, " ").Trim()));
        s = Blank().Replace(s, "\n\n").Trim();
        if (s.Length == 0) return [];
        return [.. s.Split("\n\n").Select(p => p.Trim()).Where(p => p.Length > 0)
            .Select(p => p.Length > 2 && p[0] == Mark && p[^1] == Mark ? new ReadingBlock(null, p[1..^1]) : TextBlock(p.Replace(Mark.ToString(), "")))];
    }

    /// <summary>Đoạn chữ: tách phần link (dấu LinkOpen ... LinkClose) thành ReadingSpan; Text là chữ đã bỏ dấu.</summary>
    private static ReadingBlock TextBlock(string p)
    {
        if (p.IndexOf(LinkOpen) < 0) return new ReadingBlock(p);
        var spans = new List<ReadingSpan>();
        var i = 0;
        while (i < p.Length)
        {
            var open = p.IndexOf(LinkOpen, i);
            var mid = open < 0 ? -1 : p.IndexOf(LinkMid, open);
            var close = mid < 0 ? -1 : p.IndexOf(LinkClose, mid);
            if (open < 0 || mid < 0 || close < 0)
            {
                spans.Add(new ReadingSpan(Strip(p[i..])));
                break;
            }
            if (open > i) spans.Add(new ReadingSpan(Strip(p[i..open])));
            spans.Add(new ReadingSpan(p[(mid + 1)..close], p[(open + 1)..mid]));
            i = close + 1;
        }
        spans.RemoveAll(x => x.Text.Length == 0);
        return new ReadingBlock(string.Concat(spans.Select(x => x.Text)), null, spans.Any(x => x.Url is not null) ? spans : null);
    }

    private static string Strip(string s) => s.Replace(LinkOpen.ToString(), "").Replace(LinkMid.ToString(), "").Replace(LinkClose.ToString(), "");

    private static bool IsWebLink(string url) => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    /// <summary>Link trong bài (http, https, mailto), bỏ trùng; chữ của link là chữ hiện trong bài, không có thì là địa chỉ.</summary>
    public static IReadOnlyList<LmsLink> Links(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];
        return [.. Anchor().Matches(html)
            .Select(m => (Url: WebUtility.HtmlDecode(m.Groups[1].Value).Trim(), Text: WebUtility.HtmlDecode(Practice.Text.PlainText(m.Groups[2].Value)).Trim()))
            .Where(l => l.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || l.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        || l.Url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            .Where(l => !ShownInBody(l.Text, l.Url))
            .DistinctBy(l => l.Url, StringComparer.OrdinalIgnoreCase)
            .Select(l => new LmsLink(l.Text.Length > 0 ? l.Text : l.Url, l.Url))];
    }

    /// <summary>Chữ của link chính là địa chỉ (hiện nguyên trong bài, chọn và sao chép được): không in lại ở danh sách link.</summary>
    private static bool ShownInBody(string text, string url)
    {
        static string Norm(string v)
        {
            v = v.Trim().TrimEnd('/');
            foreach (var p in new[] { "https://", "http://", "mailto:" })
                if (v.StartsWith(p, StringComparison.OrdinalIgnoreCase)) { v = v[p.Length..]; break; }
            return v.TrimEnd('/');
        }
        return text.Length > 0 && string.Equals(Norm(text), Norm(url), StringComparison.OrdinalIgnoreCase);
    }

    public static Reading Of(string title, string meta, string? html, IReadOnlyList<LmsLink>? attachments, string? webUrl) =>
        new(title, meta, Paragraphs(html), [.. Links(html).Select(Browsable)], [.. (attachments ?? []).Select(Browsable)], webUrl, Blocks(html));

    /// <summary>
    /// Link file của LMS qua web service (/webservice/pluginfile.php/) cần token trong địa chỉ; mở trong trình duyệt của app thì
    /// dùng bản /pluginfile.php/ (đi theo phiên đăng nhập), không gắn token vào link.
    /// </summary>
    public static LmsLink Browsable(LmsLink l) =>
        l.Url is { } u && u.Contains("/webservice/pluginfile.php/", StringComparison.OrdinalIgnoreCase)
            ? l with { Url = u.Replace("/webservice/pluginfile.php/", "/pluginfile.php/", StringComparison.OrdinalIgnoreCase) } : l;
}
