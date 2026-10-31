using System.Net;
using System.Text.RegularExpressions;
using SoHocTap.Data;

namespace SoHocTap.Presentation;

/// <summary>Một nội dung LMS để đọc trong app (bài thông báo, tin nhắn, trang, đề bài): chữ đã bỏ thẻ, link, file đính kèm, link gốc.</summary>
public sealed record Reading(string Title, string Meta, IReadOnlyList<string> Paragraphs, IReadOnlyList<LmsLink> Links, IReadOnlyList<LmsLink> Attachments, string? WebUrl);

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
    [GeneratedRegex(@"</t[dh]\s*>", RegexOptions.IgnoreCase)] private static partial Regex Cell();
    [GeneratedRegex(@"<a\b[^>]*\bhref\s*=\s*[""']([^""']+)[""'][^>]*>([\s\S]*?)</a\s*>", RegexOptions.IgnoreCase)] private static partial Regex Anchor();
    [GeneratedRegex(@"[ \t ]+")] private static partial Regex Spaces();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex Blank();

    /// <summary>Đoạn chữ của <paramref name="html"/>; dòng trong một đoạn giữ nguyên (tin nhắn hay xuống dòng).</summary>
    public static IReadOnlyList<string> Paragraphs(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];
        var s = Hidden().Replace(html, "");
        s = Img().Replace(s, " [ảnh] ");
        s = Br().Replace(s, "\n");
        s = Li().Replace(s, "\n• ");
        s = Cell().Replace(s, "  ");
        s = Block().Replace(s, "\n\n");
        s = WebUtility.HtmlDecode(Practice.Text.PlainText(s)).Replace("\r", "");
        s = string.Join("\n", s.Split('\n').Select(l => Spaces().Replace(l, " ").Trim()));
        s = Blank().Replace(s, "\n\n").Trim();
        return s.Length == 0 ? [] : [.. s.Split("\n\n").Select(p => p.Trim()).Where(p => p.Length > 0)];
    }

    /// <summary>Link trong bài (http, https, mailto), bỏ trùng; chữ của link là chữ hiện trong bài, không có thì là địa chỉ.</summary>
    public static IReadOnlyList<LmsLink> Links(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];
        return [.. Anchor().Matches(html)
            .Select(m => (Url: WebUtility.HtmlDecode(m.Groups[1].Value).Trim(), Text: WebUtility.HtmlDecode(Practice.Text.PlainText(m.Groups[2].Value)).Trim()))
            .Where(l => l.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || l.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        || l.Url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            .DistinctBy(l => l.Url, StringComparer.OrdinalIgnoreCase)
            .Select(l => new LmsLink(l.Text.Length > 0 ? l.Text : l.Url, l.Url))];
    }

    public static Reading Of(string title, string meta, string? html, IReadOnlyList<LmsLink>? attachments, string? webUrl) =>
        new(title, meta, Paragraphs(html), [.. Links(html).Select(Browsable)], [.. (attachments ?? []).Select(Browsable)], webUrl);

    /// <summary>
    /// Link file của LMS qua web service (/webservice/pluginfile.php/) cần token trong địa chỉ; mở trong trình duyệt của app thì
    /// dùng bản /pluginfile.php/ (đi theo phiên đăng nhập), không gắn token vào link.
    /// </summary>
    public static LmsLink Browsable(LmsLink l) =>
        l.Url is { } u && u.Contains("/webservice/pluginfile.php/", StringComparison.OrdinalIgnoreCase)
            ? l with { Url = u.Replace("/webservice/pluginfile.php/", "/pluginfile.php/", StringComparison.OrdinalIgnoreCase) } : l;
}
