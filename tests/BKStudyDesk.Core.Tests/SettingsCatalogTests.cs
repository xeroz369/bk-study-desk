using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

/// <summary>Danh mục Cài đặt: kiểm ô số, ô địa chỉ thư viện, chia Thường dùng và Nâng cao. Không ghi cài đặt thật.</summary>
public class SettingsCatalogTests
{
    private static readonly SettingsActions None = new(() => "", () => { }, () => Task.CompletedTask, () => { }, _ => { }, _ => { }, () => false, _ => false,
        () => Task.FromResult<string?>(null), _ => Task.CompletedTask, () => Task.CompletedTask, _ => { }, () => { });

    [Fact]
    public void Number_RejectsBelowMinAndNonNumbers()
    {
        var item = new NumberItem("x", null, 6, () => 6, _ => { });
        Assert.Equal(12, item.Parse(" 12 "));
        Assert.Null(item.Parse("5"));
        Assert.Null(item.Parse("-1"));
        Assert.Null(item.Parse("abc"));
    }

    [Fact]
    public void LibraryUrl_HttpsOrEmpty()
    {
        var url = SettingsCatalog.Groups(None).SelectMany(g => g.Items).OfType<TextItem>().Single(i => i.Error.Contains("https"));
        Assert.Equal("https://lib.example", url.Normalize(" https://lib.example "));
        Assert.Equal("", url.Normalize(""));
        Assert.Null(url.Normalize("http://lib.example"));
    }

    [Fact]
    public void Keys_PageRangeFollowsPageCount()
    {
        var keys = SettingsCatalog.Groups(None with { PageCount = 6 }).SelectMany(g => g.Items).OfType<InfoItem>().ToList();
        Assert.Contains(keys, k => k.Value.Contains("Ctrl+1") && k.Value.Contains("Ctrl+6"));
        Assert.Contains(keys, k => k.Value == "F5");
        // Không biết số trang thì không ghi dòng chuyển trang (không đoán số).
        Assert.DoesNotContain(SettingsCatalog.Groups(None).SelectMany(g => g.Items).OfType<InfoItem>(), k => k.Value.Contains("Ctrl+"));
    }

    [Fact]
    public void Language_ListsEveryLangFile()
    {
        var lang = SettingsCatalog.Groups(None).SelectMany(g => g.Items).OfType<ChoiceItem>().Single(c => c.Options.Any(o => o.Value == "en"));
        Assert.Contains(lang.Options, o => o.Value == "vi");
    }

    [Fact]
    public void Groups_CommonFirst_EveryGroupHasItems()
    {
        var groups = SettingsCatalog.Groups(None);
        Assert.All(groups, g => Assert.NotEmpty(g.Items));
        var firstAdvanced = groups.ToList().FindIndex(g => g.Advanced);
        Assert.True(firstAdvanced > 0);
        Assert.All(groups.Skip(firstAdvanced), g => Assert.True(g.Advanced));
    }
}
