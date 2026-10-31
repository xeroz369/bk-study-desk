using System.Text.RegularExpressions;

namespace SoHocTap.Sources.Lms;

/// <summary>
/// Gọt HTML của LMS trước khi lưu (tin nhắn, trang): bài dán từ Word mang style, class, span, font, ghi chú điều kiện của Office
/// cho từng chữ, một tin vài dòng nặng tới 50 KB (đo 31/10/2026: 161 tin thành 2,2 MB). App chỉ đọc chữ và link
/// (Presentation.LmsReading) nên bỏ các phần đó không mất gì.
/// </summary>
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
        // Chỉ bỏ thuộc tính bên trong thẻ: chữ trong bài như "width = 10" phải giữ nguyên.
        return Tag().Replace(s, t => Attrs().Replace(t.Value, ""));
    }
}
