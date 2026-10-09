using System.Text.Json;
using System.Text.RegularExpressions;

namespace SoHocTap.Tests;

/// <summary>vi.json và en.json phải cùng bộ key và cùng placeholder {n}: thiếu key thì UI hiện tên key, lệch placeholder thì string.Format lỗi.</summary>
public partial class LangParityTests
{
    private static Dictionary<string, string> Read(string code) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "lang", code + ".json")))!;

    private static readonly Dictionary<string, string> Vi = Read("vi");
    private static readonly Dictionary<string, string> En = Read("en");

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")] private static partial Regex Placeholder();

    private static string Holes(string s) => string.Join(",", Placeholder().Matches(s).Select(m => m.Groups[1].Value).Distinct().Order(StringComparer.Ordinal));

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void NoDuplicateKeys(string code)
    {
        // Dictionary đọc im lặng ghi đè key trùng, nên đọc thẳng token để bắt.
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "lang", code + ".json"))));
        var seen = new HashSet<string>();
        var dup = new List<string>();
        while (reader.Read())
            if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1 && !seen.Add(reader.GetString()!)) dup.Add(reader.GetString()!);
        Assert.Empty(dup);
    }

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void NoKeysDifferOnlyInCase(string code)
    {
        // "autoLogin.note" và "autologin.note" là hai key khác nhau với Dictionary nhưng dễ nhầm khi sửa: một bản bị bỏ quên mà không ai thấy.
        var clash = Read(code).Keys.GroupBy(k => k, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => string.Join(" / ", g)).ToList();
        Assert.Empty(clash);
    }

    [Fact]
    public void SameKeys()
    {
        Assert.Empty(Vi.Keys.Except(En.Keys).Order(StringComparer.Ordinal));
        Assert.Empty(En.Keys.Except(Vi.Keys).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void SamePlaceholders()
    {
        var bad = Vi.Keys.Intersect(En.Keys).Where(k => Holes(Vi[k]) != Holes(En[k])).Select(k => $"{k}: vi {{{Holes(Vi[k])}}} en {{{Holes(En[k])}}}").ToList();
        Assert.Empty(bad);
    }

    [Fact]
    public void FormatStringsAreValid()
    {
        // Chuỗi có placeholder phải format được với đủ tham số (dấu { lẻ làm string.Format throw lúc chạy).
        foreach (var (code, dict) in new[] { ("vi", Vi), ("en", En) })
            foreach (var (k, v) in dict.Where(p => Placeholder().IsMatch(p.Value)))
            {
                var matches = Placeholder().Matches(v);
                var n = matches.Max(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)) + 1;
                // Tham số theo định dạng: "{0:dd/MM}" là ngày, "{0:0}" là số, không có định dạng thì chuỗi.
                var args = new object[n];
                for (var i = 0; i < n; i++) args[i] = "x";
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    var i = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    var spec = m.Value.Contains(':') ? m.Value[(m.Value.IndexOf(':') + 1)..^1] : "";
                    if (spec.Length > 0) args[i] = spec.IndexOfAny(['d', 'M', 'y', 'H', 'h', 'm']) >= 0 ? DateTime.UnixEpoch : 1.5;
                }
                var ex = Record.Exception(() => string.Format(System.Globalization.CultureInfo.InvariantCulture, v, args));
                Assert.True(ex is null, $"{code}.json {k}: {ex?.Message}");
            }
    }
}
