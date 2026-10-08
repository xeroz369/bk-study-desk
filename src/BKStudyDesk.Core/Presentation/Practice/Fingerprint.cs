using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SoHocTap.Presentation.Practice;

/// <summary>
/// Fingerprint câu hỏi (port <c>fingerprint.ts</c>): cùng câu thì cùng giá trị dù đến từ đâu (bài có sẵn, gói, quiz, bản gói mới hơn).
/// Lịch ôn, ghi chú, thời gian làm câu trong <c>ket-qua.json</c> lưu theo khóa này, nên phải tính y hệt bản TS.
/// </summary>
public static partial class Fingerprint
{
    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex Img();
    [GeneratedRegex("<[^>]+>")] private static partial Regex Tag();
    [GeneratedRegex(@"\\[,;: !]")] private static partial Regex TexSpace();

    /// <summary>Chữ chỉ để băm: bỏ thẻ, entity, khoảng trắng; ảnh thành [img]; TeX giữ nhưng bỏ lệnh khoảng cách.</summary>
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

    /// <summary>FNV-1a với hai seed, ra 16 chữ số hex. Đủ để phân biệt câu, không dùng cho bảo mật.</summary>
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

    /// <summary>
    /// Phương án được sắp xếp (bản xáo của cùng câu vẫn khớp). <paramref name="numericKey"/>: đáp số của câu numeric
    /// (<c>value ?? answer</c> như bản TS; không có thì là chữ "undefined").
    /// </summary>
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
