using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

/// <summary>Màu thanh trên cùng suy ra từ màu nhấn: chữ trắng luôn đạt tương phản tối thiểu.</summary>
public class AccentColorsTests
{
    [Theory]
    [InlineData("#0388B4")]
    [InlineData("#FFD700")]   // vàng sáng: phải làm tối nhiều
    [InlineData("#1A7F37")]
    public void Bar_MeetsContrast(string hex)
    {
        var accent = AccentColors.Parse(hex)!.Value;
        Assert.True(AccentColors.ContrastWithWhite(AccentColors.Bar(accent, dark: false)) >= AccentColors.MinBarContrastLight);
        Assert.True(AccentColors.ContrastWithWhite(AccentColors.Bar(accent, dark: true)) >= AccentColors.MinBarContrastDark);
    }

    [Fact]
    public void Bar_DarkEnoughAccent_Unchanged() =>
        Assert.Equal("#0B3D91", AccentColors.Hex(AccentColors.Bar(AccentColors.Parse("#0B3D91")!.Value, dark: false)));

    [Fact]
    public void Parse_RejectsBadHex()
    {
        Assert.Null(AccentColors.Parse("0388B4"));
        Assert.Null(AccentColors.Parse("#03G8B4"));
        Assert.Equal(((byte)3, (byte)136, (byte)180), AccentColors.Parse("#0388B4"));
    }

    [Fact]
    public void Mix_BlendsTowardAccent()
    {
        var bk = AccentColors.Parse("#0388B4")!.Value;
        var white = AccentColors.Parse("#FFFFFF")!.Value;
        Assert.Equal("#E6F3F8", AccentColors.Hex(AccentColors.Mix(bk, white, AccentColors.DateBoxTintLight)));   // ô ngày cũ của bản xanh BK
        Assert.Equal("#FFFFFF", AccentColors.Hex(AccentColors.Mix(bk, white, 0)));
        Assert.Equal("#0388B4", AccentColors.Hex(AccentColors.Mix(bk, white, 1)));
    }

    [Fact]
    public void Choices_FromDefaultConfig() => Assert.Contains(AccentColors.Choices(), c => c.Value == "system");

    [Fact]
    public void Choices_NamesFromLang() => Assert.All(AccentColors.Choices(), c => Assert.False(c.Name.StartsWith("accent.", StringComparison.Ordinal), c.Name));
}
