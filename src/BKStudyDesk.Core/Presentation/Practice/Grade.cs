using System.Globalization;

namespace SoHocTap.Presentation.Practice;

/// <summary>Chấm một câu theo loại (port <c>grade.ts</c>). Đúng, Sai đã đổi thành single lúc nạp gói.</summary>
public static class Grade
{
    private const string Letters = "ABCDEFGH";

    public static string QType(Question q) => q.Type ?? "single";

    /// <summary>"1,5" (kiểu Việt) hay "1.5" đều đọc được; "1.234,5" và "1,234.5" cũng được.</summary>
    public static double? ParseNumber(string s)
    {
        var t = Text.CollapseJs(Text.TrimJs(s), "");
        if (t.Length == 0) return null;
        if (t.Contains(',') && t.Contains('.'))
            t = t.LastIndexOf(',') > t.LastIndexOf('.') ? ReplaceFirst(t.Replace(".", ""), ",", ".") : t.Replace(",", "");
        else t = ReplaceFirst(t, ",", ".");
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;
    }

    private static string ReplaceFirst(string s, string from, string to)
    {
        var i = s.IndexOf(from, StringComparison.Ordinal);
        return i < 0 ? s : s[..i] + to + s[(i + from.Length)..];
    }

    public static bool IsCorrect(Question q, Answer? answer)
    {
        if (Answer.IsBlankValue(answer)) return false;
        var a = answer!.Value;
        switch (QType(q))
        {
            case "multi":
                return a.Indices is { } got && got.Order().SequenceEqual((q.Answers ?? []).Order());
            case "numeric":
                {
                    var n = a.Number ?? ParseNumber(AsString(a));
                    if (n is null || q.Value is not { } v) return false;
                    return Math.Abs(n.Value - v) <= (q.Tolerance ?? 0) + Math.Abs(v) * 1e-12;
                }
            case "short":
                return (q.Accept ?? []).Any(x => Text.NormText(x) == Text.NormText(AsString(a)));
            default:
                return a.Number is { } i && i == q.Answer;
        }
    }

    // String(a) của JS: mảng ghép bằng dấu phẩy.
    private static string AsString(Answer a) =>
        a.Text ?? (a.Indices is { } ix ? string.Join(",", ix) : a.Number is { } n ? JsNumber.Format(n) : "");

    /// <summary>Đáp án đúng dạng chữ (hiện sau khi làm, ở trang kết quả).</summary>
    public static string AnswerText(Question q) => QType(q) switch
    {
        "multi" => string.Join(", ", (q.Answers ?? []).Select(i => i >= 0 && i < Letters.Length ? Letters[i].ToString() : "")),
        "numeric" => (q.Value is { } v ? JsNumber.Format(v) : "undefined")
            + (q.Tolerance is { } t && t != 0 ? " ± " + JsNumber.Format(t) : "")
            + (string.IsNullOrEmpty(q.Unit) ? "" : " " + q.Unit),
        "short" => string.Join(" / ", q.Accept ?? []),
        _ => q.Answer >= 0 && q.Answer < Letters.Length ? Letters[q.Answer].ToString() : "",
    };
}
