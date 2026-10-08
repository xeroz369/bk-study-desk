using System.Text.Json;
using System.Text.RegularExpressions;

namespace SoHocTap.Tests;

/// <summary>
/// Danh sách thẻ Cài đặt (Ui/Settings/SettingsSections.cs). Thẻ là UserControl WPF nên project test không dựng được;
/// test đọc mã nguồn (chép ra thư mục output): mỗi file *Section.xaml.cs phải có một dòng trong danh sách, và ngược lại,
/// TitleKey không trùng và có trong lang/vi.json. Quên đăng ký thẻ mới là test đỏ.
/// </summary>
public partial class SettingsSectionsTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "settings");

    [GeneratedRegex(@"new\s+(\w+Section)\s*\(")] private static partial Regex Registered();
    [GeneratedRegex(@"TitleKey\s*=>\s*""([^""]+)""")] private static partial Regex TitleKey();

    [Fact]
    public void SettingsSections_AllDistinct()
    {
        var registry = File.ReadAllText(Path.Combine(Dir, "SettingsSections.cs"));
        var names = Registered().Matches(registry).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(12, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());

        var files = Directory.GetFiles(Dir, "*Section.xaml.cs").Select(f => Path.GetFileName(f)[..^".xaml.cs".Length]).Order(StringComparer.Ordinal);
        Assert.Equal(files, names.Order(StringComparer.Ordinal));

        var vi = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "lang", "vi.json")))!;
        var keys = names.Select(n =>
        {
            var found = TitleKey().Matches(File.ReadAllText(Path.Combine(Dir, n + ".xaml.cs")));
            Assert.True(found.Count == 1, $"{n}: cần đúng một TitleKey => \"...\"");
            return found[0].Groups[1].Value;
        }).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, k => Assert.True(vi.ContainsKey(k), $"lang/vi.json thiếu {k}"));
    }
}
