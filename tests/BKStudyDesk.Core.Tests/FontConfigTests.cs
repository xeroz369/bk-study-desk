using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

/// <summary>Ô chọn cỡ chữ: mặc định đứng đầu, giá trị là số theo InvariantCulture (máy để dấu phẩy thập phân vẫn đọc lại được).</summary>
public class FontConfigTests
{
    [Fact]
    public void SizeOptions_DefaultFirstThenSizes()
    {
        var o = FontConfig.SizeOptions();
        Assert.Equal("0", o[0].Value);
        Assert.Contains(o, x => x.Value == "18");
        Assert.All(o, x => Assert.True(double.TryParse(x.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)));
    }
}
