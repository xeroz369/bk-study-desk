using System.Globalization;

namespace SoHocTap.Presentation.Practice;

/// <summary>
/// <c>String(number)</c> của JavaScript (ECMAScript Number::toString): fingerprint câu numeric và chữ đáp án ghép số vào chuỗi,
/// phải ra đúng như bản TS. Chữ số lấy từ "R" của .NET (ngắn nhất mà đọc lại vẫn đúng), chỉ đổi cách đặt dấu chấm và số mũ.
/// </summary>
public static class JsNumber
{
    public static string Format(double d)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsInfinity(d)) return d > 0 ? "Infinity" : "-Infinity";
        if (d == 0) return "0";
        var sign = d < 0 ? "-" : "";
        var (digits, n) = Decompose(Math.Abs(d).ToString("R", CultureInfo.InvariantCulture));
        var k = digits.Length;
        string body;
        if (k <= n && n <= 21) body = digits + new string('0', n - k);
        else if (0 < n && n <= 21) body = digits[..n] + "." + digits[n..];
        else if (-6 < n && n <= 0) body = "0." + new string('0', -n) + digits;
        else
        {
            var e = n - 1;
            body = (k == 1 ? digits : digits[0] + "." + digits[1..]) + "e" + (e >= 0 ? "+" : "-") + Math.Abs(e);
        }
        return sign + body;
    }

    // Chữ số có nghĩa và n: giá trị = 0.digits x 10^n (số chữ số đứng trước dấu chấm khi viết thường).
    private static (string Digits, int N) Decompose(string r)
    {
        int exp = 0;
        var e = r.IndexOfAny(['E', 'e']);
        var mantissa = r;
        if (e >= 0)
        {
            exp = int.Parse(r[(e + 1)..], CultureInfo.InvariantCulture);
            mantissa = r[..e];
        }
        var dot = mantissa.IndexOf('.');
        var intPart = dot < 0 ? mantissa : mantissa[..dot];
        var frac = dot < 0 ? "" : mantissa[(dot + 1)..];
        var all = intPart + frac;
        var n = intPart.Length + exp;
        var lead = all.Length - all.TrimStart('0').Length;
        all = all[lead..];
        n -= lead;
        all = all.TrimEnd('0');
        return (all.Length == 0 ? "0" : all, n);
    }
}
