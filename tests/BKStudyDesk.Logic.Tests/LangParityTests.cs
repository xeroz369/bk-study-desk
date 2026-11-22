using System.Text.Json;
using System.Text.RegularExpressions;

namespace SoHocTap.Tests;

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
        foreach (var (code, dict) in new[] { ("vi", Vi), ("en", En) })
            foreach (var (k, v) in dict.Where(p => Placeholder().IsMatch(p.Value)))
            {
                var matches = Placeholder().Matches(v);
                var n = matches.Max(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)) + 1;
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
