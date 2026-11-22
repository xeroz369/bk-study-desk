using SoHocTap.Ui;

namespace SoHocTap.Tests;

public class FontChoiceTests
{
    private static readonly HashSet<string> Machine = new(["Segoe UI", "Arial", "Times New Roman"], StringComparer.OrdinalIgnoreCase);
    private static bool Installed(string f) => Machine.Contains(f);

    [Fact]
    public void EmptyConfigIsDefault()
    {
        var c = FontChoice.Parse(null, null, Installed);
        Assert.Equal(FontChoice.Default, c);
        Assert.True(c.IsDefault);
        Assert.Equal(12, c.BodySize(12));
        Assert.Equal(1, c.Scale(12));
    }

    [Fact]
    public void InstalledFamilyIsKept()
    {
        Assert.Equal("Arial", FontChoice.Parse("  Arial ", 0, Installed).Family);
    }

    [Theory]
    [InlineData("Không có phông này")]
    [InlineData("")]
    [InlineData("   ")]
    public void UnknownFamilyFallsBackToDefault(string family)
    {
        var c = FontChoice.Parse(family, 14, Installed);
        Assert.Equal("", c.Family);
        Assert.Equal(14, c.Size);
    }

    [Theory]
    [InlineData("Arial, Segoe UI")]
    [InlineData("Arial\"; color: red")]
    [InlineData("Ari'al")]
    [InlineData("A;B")]
    [InlineData("A\nB")]
    public void UnsafeNameRejectedEvenIfReportedInstalled(string family)
    {
        Assert.Equal("", FontChoice.NormalizeFamily(family, _ => true));
        Assert.Equal("", FontChoice.NormalizeFamily(new string('a', 101), _ => true));
    }

    [Theory]
    [InlineData(13, 13)]
    [InlineData(14, 14)]
    [InlineData(18, 18)]
    [InlineData(15, 14)]
    [InlineData(15.5, 16)]
    [InlineData(1, 13)]
    [InlineData(72, 18)]
    public void SizeSnapsToAllowedList(double raw, double expected)
    {
        Assert.Equal(expected, FontChoice.NormalizeSize(raw));
        Assert.Contains(FontChoice.NormalizeSize(raw), FontChoice.Sizes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ZeroNegativeOrNotANumberIsDefaultSize(double raw)
    {
        Assert.Equal(0, FontChoice.NormalizeSize(raw));
    }

    [Fact]
    public void HeadingsScaleWithBody()
    {
        var c = new FontChoice("", 18);
        var scale = c.Scale(12);
        Assert.Equal(1.5, scale);
        Assert.Equal(30, FontChoice.Scaled(20, scale));
        Assert.Equal(16.5, FontChoice.Scaled(11, scale));
        Assert.Equal(20, FontChoice.Scaled(20, 1));
    }

    [Fact]
    public void WebScaleNeverShrinks()
    {
        var c = new FontChoice("", 13);
        Assert.True(c.Scale(15) < 1);
        Assert.Equal(1, c.WebScale(15));
        Assert.Equal(1.5, new FontChoice("", 18).WebScale(12));
    }
}
