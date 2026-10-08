using Avalonia.Headless.XUnit;
using XamlMath;

namespace BKStudyDesk.Math.Tests;

public class CommandTests
{
    [AvaloniaTheory]
    [InlineData(@"a\ b")]
    [InlineData(@"a\quad b")]
    [InlineData(@"a\qquad b")]
    [InlineData(@"A\iff B")]
    [InlineData(@"1,2,\dots,n")]
    [InlineData(@"\iint_D f\,dA")]
    [InlineData(@"\mathbb{R}^2")]
    [InlineData(@"x\in\mathbb{N}, \mathbb{Z}, \mathbb{Q}, \mathbb{C}")]
    [InlineData(@"\displaystyle\sum_{i=1}^n i")]
    [InlineData(@"\textstyle\frac12")]
    [InlineData(@"\dfrac{1}{2}")]
    [InlineData(@"\tfrac{1}{2}")]
    [InlineData(@"\big( x \big)")]
    [InlineData(@"\Big[ x \Big]")]
    [InlineData(@"\bigl( x \bigr)")]
    [InlineData(@"\Bigl| x \Bigr|")]
    [InlineData(@"\bigg\{ x \Bigg\}")]
    public void Parses(string tex) => Assert.Null(Tex.Error(tex));

    [AvaloniaFact]
    public void Quad_is_one_em_and_qquad_two()
    {
        var plain = Tex.Width("ab");
        Assert.Equal(1.0, (Tex.Width(@"a\quad b") - plain) / Tex.Em, 2);
        Assert.Equal(2.0, (Tex.Width(@"a\qquad b") - plain) / Tex.Em, 2);
        Assert.True(Tex.Width(@"a\ b") > plain);
    }

    [AvaloniaFact]
    public void Dfrac_is_taller_than_tfrac()
    {
        Assert.True(Tex.Height(@"\dfrac{1}{2}", TexStyle.Text) > Tex.Height(@"\tfrac{1}{2}", TexStyle.Text));
        Assert.True(Tex.Height(@"\dfrac{1}{2}", TexStyle.Text) > Tex.Height(@"\frac{1}{2}", TexStyle.Text));
        Assert.Equal(Tex.Height(@"\frac{1}{2}", TexStyle.Display), Tex.Height(@"\dfrac{1}{2}", TexStyle.Text), 6);
    }

    [AvaloniaFact]
    public void Displaystyle_enlarges_the_rest_of_the_group()
    {
        Assert.True(Tex.Height(@"\displaystyle\sum_{i=1}^n", TexStyle.Text) > Tex.Height(@"\sum_{i=1}^n", TexStyle.Text));
        Assert.Equal(Tex.Height(@"\sum_{i=1}^n", TexStyle.Text), Tex.Height(@"\textstyle\sum_{i=1}^n", TexStyle.Display), 6);
        Assert.True(Tex.Height(@"\textstyle\sum_{i=1}^n", TexStyle.Display) < Tex.Height(@"\sum_{i=1}^n", TexStyle.Display));
        // Chỉ tác dụng trong nhóm: phần ngoài {...} giữ style cũ.
        Assert.Equal(Tex.Height(@"{\textstyle a}\sum_{i=1}^n", TexStyle.Display), Tex.Height(@"\sum_{i=1}^n", TexStyle.Display), 6);
    }

    [AvaloniaFact]
    public void Big_delimiters_grow()
    {
        var normal = Tex.Height("(");
        var big = Tex.Height(@"\big(");
        var bigUpper = Tex.Height(@"\Big(");
        var bigg = Tex.Height(@"\bigg(");
        var biggUpper = Tex.Height(@"\Bigg(");
        Assert.True(normal < big && big < bigUpper && bigUpper < bigg && bigg < biggUpper, $"{normal} {big} {bigUpper} {bigg} {biggUpper}");
    }

    [AvaloniaFact]
    public void Iint_is_wider_than_int() => Assert.True(Tex.Width(@"\iint") > Tex.Width(@"\int"));
}

public class BlackboardRenderTests
{
    // Arial, Segoe UI không có ℝ: phải lấy font dự phòng, không ném lỗi lúc Render.
    [AvaloniaFact]
    public void Mathbb_draws_with_fallback_font()
    {
        Tex.Draw(@"\mathbb{R}");
        Assert.True(Frames.DarkPixels(new AvaloniaMath.Controls.FormulaBlock { Formula = @"\mathbb{R}", Foreground = Avalonia.Media.Brushes.Black }) > 0);
    }
}
