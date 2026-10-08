using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using AvaloniaMath.Controls;
using BKStudyDesk.Desktop.Views.Practice;

namespace BKStudyDesk.Math.Tests;

public class MathViewTests
{
    // Cửa sổ có token như app: Text, MathScale, MathScaleDisplay.
    private static Window Host(Control content, IBrush? text = null)
    {
        var window = new Window { Width = 400, Height = 200, Background = Brushes.White, Content = content };
        window.Resources["Text"] = text ?? Brushes.Black;
        window.Resources["MathScale"] = 18d;
        window.Resources["MathScaleDisplay"] = 21d;
        window.Show();
        return window;
    }

    private static List<string> Captured(Action run)
    {
        var warnings = new List<string>();
        var old = MathView.Warn;
        MathView.Warn = warnings.Add;
        try { run(); }
        finally { MathView.Warn = old; }
        return warnings;
    }

    [AvaloniaFact]
    public void Empty_formula_is_not_an_error()
    {
        var view = new MathView { Tex = "  " };
        Host(view);
        Assert.IsType<FormulaBlock>(view.Content);
        Assert.False(view.Block.HasError);
        Assert.Equal(0, view.DesiredSize.Width);
    }

    [AvaloniaTheory]
    [InlineData("x=đ")]
    [InlineData(@"\foo{x}")]
    [InlineData(@"\frac{1}")]
    [InlineData(@"\left( x")]
    public void Bad_formula_falls_back_to_source(string tex)
    {
        var warnings = Captured(() =>
        {
            var view = new MathView { Tex = tex };
            var window = Host(view);
            window.CaptureRenderedFrame();   // vẽ thật: không được ném lỗi
            var text = Assert.IsType<TextBlock>(view.Content);
            Assert.Equal($@"\({tex}\)", text.Text);
        });
        Assert.Contains(warnings, w => w.Contains(tex));
    }

    // U+0378 chưa được gán trong Unicode. Upstream đo được (FormattedText) nhưng ném lỗi trong Render, làm sập cửa sổ.
    // Windows, Linux: không font nào có ký tự này, MathView phải đổi sang chữ thường trước khi vẽ.
    // macOS: font LastResort có glyph cho mọi mã (cả mã chưa gán), công thức vẽ được một ô thay thế; chỉ cần vẽ xong không lỗi.
    [AvaloniaFact]
    public void Glyph_missing_everywhere_falls_back_before_render()
    {
        var view = new MathView { Tex = "\\text{a\u0378}" };
        var window = Host(view);
        window.CaptureRenderedFrame();
        if (OperatingSystem.IsMacOS()) Assert.NotNull(view.Content);
        else Assert.IsType<TextBlock>(view.Content);
    }

    // Input dị dạng hay gặp khi người dùng tự soạn: không được ném lỗi ở bất kỳ bước nào.
    [AvaloniaTheory]
    [InlineData("x^")]
    [InlineData("a_{")]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("^2")]
    [InlineData(@"\sqrt[")]
    [InlineData(@"\frac")]
    [InlineData(@"\big")]
    [InlineData("&")]
    [InlineData(@"\\")]
    [InlineData(@"\color{red}")]
    [InlineData(@"\begin{array}{}\end{array}")]
    [InlineData(@"\begin{array}{c}\end{array}")]
    [InlineData(@"\begin{array}{q}a\end{array}")]
    [InlineData(@"\begin{pmatrix}\end{pmatrix}")]
    [InlineData(@"\begin{cases}\end{cases}")]
    [InlineData(@"\begin{array}{c|c} a & b & c \\ d \end{array}")]
    [InlineData(@"\left( \displaystyle x \right)")]
    [InlineData(@"\mathbb{}")]
    [InlineData(@"\dfrac{}{}")]
    public void Malformed_input_never_throws(string tex)
    {
        var view = new MathView { Tex = tex };
        Host(view).CaptureRenderedFrame();
        Assert.True(view.Content is TextBlock || view.Content is FormulaBlock { HasError: false });
    }

    [AvaloniaFact]
    public void Display_fallback_uses_display_delimiters()
    {
        var view = new MathView { Tex = @"\foo", Display = true };
        Host(view);
        Assert.Equal(@"\[\foo\]", Assert.IsType<TextBlock>(view.Content).Text);
    }

    [AvaloniaFact]
    public void Same_bad_formula_is_logged_once()
    {
        var tex = @"\notacommand" + Guid.NewGuid().ToString("N");
        var warnings = Captured(() =>
        {
            Host(new MathView { Tex = tex });
            Host(new MathView { Tex = tex });
        });
        Assert.Single(warnings, w => w.Contains(tex));
    }

    [AvaloniaFact]
    public void Bad_formula_does_not_affect_neighbour()
    {
        var bad = new MathView { Tex = @"\foo" };
        var good = new MathView { Tex = @"\frac12" };
        Host(new StackPanel { Children = { bad, good } }).CaptureRenderedFrame();
        Assert.IsType<TextBlock>(bad.Content);
        Assert.IsType<FormulaBlock>(good.Content);
        Assert.False(good.Block.HasError);
        Assert.True(good.DesiredSize.Width > 0);
    }

    [AvaloniaFact]
    public void Foreground_follows_text_token()
    {
        var view = new MathView { Tex = "x" };
        var window = Host(view, Brushes.Black);
        Assert.Equal(Brushes.Black, view.Block.Foreground);
        window.Resources["Text"] = Brushes.White;
        Assert.Equal(Brushes.White, view.Block.Foreground);
    }

    // Trong dòng chữ, đường chân của công thức phải trùng đường chân của chữ: báo cho TextBlock biết qua BaselineOffset
    // (khoảng từ đỉnh control tới đường chân), dòng chữ dành chỗ cho cả phần dưới đường chân (chỉ số, mẫu phân số).
    [AvaloniaFact]
    public void Formula_reports_baseline_to_text()
    {
        var view = new MathView { Tex = @"x_n+\dfrac{1}{2}" };
        var text = new TextBlock { Inlines = [new Avalonia.Controls.Documents.Run("a "), new Avalonia.Controls.Documents.InlineUIContainer { Child = view }] };
        Host(text);
        var box = Tex.Layout(@"x_n+\dfrac{1}{2}", XamlMath.TexStyle.Text);
        Assert.InRange(TextBlock.GetBaselineOffset(view), box.Height * 18 - 0.5, box.Height * 18 + 0.5);
        // Dòng chữ cao đủ cả phần trên lẫn phần dưới đường chân.
        Assert.True(text.DesiredSize.Height >= box.TotalHeight * 18 - 0.5, $"{text.DesiredSize.Height} < {box.TotalHeight * 18}");
    }

    [AvaloniaFact]
    public void Scale_and_style_follow_display_flag()
    {
        var inline = new MathView { Tex = @"\sum_{i=1}^n i" };
        var display = new MathView { Tex = @"\sum_{i=1}^n i", Display = true };
        Host(new StackPanel { Children = { inline, display } });
        Assert.Equal(18, inline.Block.Scale);
        Assert.Equal(21, display.Block.Scale);
        Assert.Equal(XamlMath.TexStyle.Text, inline.Block.MathStyle);
        Assert.Equal(XamlMath.TexStyle.Display, display.Block.MathStyle);
    }
}
