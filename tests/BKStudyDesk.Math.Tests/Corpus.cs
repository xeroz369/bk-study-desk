using System.Text.RegularExpressions;

namespace BKStudyDesk.Math.Tests;

// Lấy công thức trong \( \), \[ \], $$ $$ từ file nội dung (như tools-dev texscan). File .json nhân đôi dấu \ nên đổi lại trước.
internal static partial class Corpus
{
    [GeneratedRegex(@"\\\(([\s\S]+?)\\\)|\\\[([\s\S]+?)\\\]|\$\$([\s\S]+?)\$\$")]
    private static partial Regex Delimited();

    public static IReadOnlyList<string> FromText(string text, bool json)
    {
        if (json) text = text.Replace(@"\\", @"\");
        var seen = new HashSet<string>();
        var list = new List<string>();
        foreach (Match m in Delimited().Matches(text))
        {
            var tex = (m.Groups[1].Success ? m.Groups[1] : m.Groups[2].Success ? m.Groups[2] : m.Groups[3]).Value.Trim();
            if (seen.Add(tex)) list.Add(tex);
        }
        return list;
    }

    public static IReadOnlyList<string> FromFiles(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>();
        var list = new List<string>();
        foreach (var p in paths)
            foreach (var tex in FromText(File.ReadAllText(p), p.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                if (seen.Add(tex)) list.Add(tex);
        return list;
    }

    // Thư mục repo: đi ngược từ thư mục test tới khi thấy studypack/.
    public static string RepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "studypack", "examples"))) return d.FullName;
        throw new DirectoryNotFoundException("không thấy studypack/examples");
    }

    public static IEnumerable<string> Files(string dir, params string[] exts) =>
        Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => exts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}"))
            .Order(StringComparer.Ordinal);

    public static List<(string Tex, string Error)> Failures(IEnumerable<string> formulas) =>
        formulas.Select(t => (Tex: t, Error: Tex.Error(t))).Where(x => x.Error != null).Select(x => (x.Tex, x.Error!)).ToList();
}
