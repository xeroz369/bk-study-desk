using Avalonia.Headless.XUnit;
using Avalonia.Media;
using AvaloniaMath.Controls;

namespace BKStudyDesk.Math.Tests;

public class SmokeTests
{
    [AvaloniaFact]
    public void Fraction_has_size()
    {
        var box = Tex.Layout(@"\frac{1}{2}");
        Assert.True(box.TotalWidth > 0);
        Assert.True(box.TotalHeight > 0);
    }

    [AvaloniaFact]
    public void Glyph_run_uses_font()
    {
        var dark = Frames.DarkPixels(new FormulaBlock { Formula = "x^2", Foreground = Brushes.Black });
        Assert.True(dark > 0, "không có pixel tối nào");
    }
}
