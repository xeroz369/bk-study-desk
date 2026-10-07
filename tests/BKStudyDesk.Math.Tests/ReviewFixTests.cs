using Avalonia.Headless.XUnit;
using XamlMath;

namespace BKStudyDesk.Math.Tests;

// Lỗi thấy khi xem tờ công thức thật (self-review cuối đợt 1).
public class ReviewFixTests
{
    // Chữ vẽ bằng font hệ thống (\text, \mathbb) phải có chiều cao, độ sâu như chữ thật, không phải cả chiều cao dòng:
    // nếu không, số mũ đặt quá cao và chữ lệch khỏi đường chân của công thức.
    [AvaloniaFact]
    public void System_font_letters_have_glyph_height()
    {
        var math = Tex.Height("R^2");
        var text = Tex.Height(@"\text{R}^2");
        Assert.True(text < 1.25 * math, $"\\text{{R}}^2 cao {text}, R^2 cao {math}");
        Assert.True(Tex.Layout(@"\text{R}").Depth < 0.05 * Tex.Em, "chữ R không có phần dưới đường chân");
        Assert.True(Tex.Layout(@"\text{g}").Depth > 0.1 * Tex.Em, "chữ g phải có phần dưới đường chân");
    }

    // \lim trong dòng (Text style) đặt cận bên cạnh như TeX, chỉ Display mới đặt cận ở dưới.
    [AvaloniaTheory]
    [InlineData(@"\lim_{(x,y)\to(0,0)} f")]
    [InlineData(@"\max_{x\in[0,1]} f")]
    [InlineData(@"\min_{x\in[0,1]} f")]
    [InlineData(@"\sup_{x\in[0,1]} f")]
    [InlineData(@"\inf_{x\in[0,1]} f")]
    [InlineData(@"\limsup_{n\to\infty} a_n")]
    public void Named_operators_put_limits_by_style(string tex)
    {
        var inline = Tex.Layout(tex, TexStyle.Text);
        var display = Tex.Layout(tex, TexStyle.Display);
        Assert.True(inline.TotalHeight < display.TotalHeight, $"trong dòng cao {inline.TotalHeight}, đứng riêng {display.TotalHeight}");
        Assert.True(inline.TotalWidth > display.TotalWidth);
    }

    // array: mỗi cột cách 5pt mỗi bên như \arraycolsep của LaTeX, tức 1 em giữa hai cột.
    [AvaloniaFact]
    public void Array_columns_have_arraycolsep()
    {
        var gap = (Tex.Width(@"\begin{array}{cc}1&2\end{array}") - Tex.Width("12")) / Tex.Em;
        Assert.Equal(2.0, gap, 1);
    }
}
