using Avalonia.Headless.XUnit;
using Avalonia.Media;
using AvaloniaMath.Controls;
using XamlMath.Atoms;

namespace BKStudyDesk.Math.Tests;

public class RenderFixTests
{
    // Cỡ chữ công thức trong app (token MathScale).
    private const double AppScale = 18;

    private static FormulaBlock Block(string tex, IBrush foreground) => new() { Formula = tex, Foreground = foreground, Scale = AppScale };

    [AvaloniaTheory]
    [InlineData(@"\begin{pmatrix}1\\2\end{pmatrix}")]
    [InlineData(@"\pmatrix{1\\2}")]
    public void Pmatrix_uses_parentheses(string tex)
    {
        var root = Tex.Parse(tex).RootAtom;
        var fenced = Assert.IsType<FencedAtom>(root is RowAtom { Elements.Count: 1 } row ? row.Elements[0] : root);
        Assert.Equal("(", fenced.LeftDelimeter?.Name);
        Assert.Equal(")", fenced.RightDelimeter?.Name);
    }

    // Mọi pixel có mực phải nằm trên đoạn trắng-đỏ: G bằng B. Viền xanh, tím là khử răng cưa subpixel.
    [AvaloniaFact]
    public void Glyphs_use_foreground_only()
    {
        var px = Frames.Pixels(Block(@"a+b-[c]\,(x)", Brushes.Red));
        var tinted = 0;
        var ink = 0;
        foreach (var p in px)
        {
            if (p is (255, 255, 255)) continue;
            ink++;
            if (System.Math.Abs(p.G - p.B) > 8) tinted++;
        }
        Assert.True(ink > 0);
        Assert.True(tinted == 0, $"{tinted}/{ink} pixel bị tô màu khác");
    }

    // Vạch của \bar phải đủ đậm ở cỡ chữ của app: có một hàng pixel tối dài ít nhất 60% bề ngang chữ x.
    [AvaloniaFact]
    public void Bar_is_visible()
    {
        var plain = Frames.Pixels(Block("x", Brushes.Black));
        var (xTop, xBottom) = Frames.InkRows(plain);
        var xWidth = Enumerable.Range(xTop, xBottom - xTop + 1).Max(y => Enumerable.Range(0, plain.GetLength(1)).Count(x => plain[y, x].R < 128));

        var barred = Frames.Pixels(Block(@"\bar{x}", Brushes.Black));
        var (top, _) = Frames.InkRows(barred);
        // Cụm hàng có mực đầu tiên (từ trên xuống) là vạch, tách khỏi chữ x bằng ít nhất một hàng trắng.
        var y0 = top;
        var best = 0;
        while (y0 < barred.GetLength(0))
        {
            var dark = Enumerable.Range(0, barred.GetLength(1)).Count(x => barred[y0, x].R < 128);
            var any = Enumerable.Range(0, barred.GetLength(1)).Any(x => barred[y0, x] is not (255, 255, 255));
            if (!any) break;
            best = System.Math.Max(best, dark);
            y0++;
        }
        Assert.True(best >= 0.6 * xWidth, $"vạch dài nhất {best}px, chữ x rộng {xWidth}px");
    }
}
