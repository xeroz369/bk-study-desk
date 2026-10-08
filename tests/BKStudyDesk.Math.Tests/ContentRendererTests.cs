using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using BKStudyDesk.Desktop.Views.Practice;

namespace BKStudyDesk.Math.Tests;

public class ContentRendererTests
{
    private const string Png1x1 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private static List<Control> Blocks(string html) => [.. ((StackPanel)ContentRenderer.Render(html)).Children];

    private static string Text(InlineCollection inlines) => string.Concat(inlines.Select(i => i switch
    {
        Run r => r.Text,
        Span s => Text(s.Inlines),
        LineBreak => "\n",
        _ => "",
    }));

    [AvaloniaFact]
    public void Inline_tex_becomes_inline_container()
    {
        var t = Assert.IsType<TextBlock>(Assert.Single(Blocks(@"<p>Cho \(x^2\) <b>nhé</b></p>")));
        var math = Assert.Single(t.Inlines!.OfType<InlineUIContainer>());
        var view = Assert.IsType<MathView>(math.Child);
        Assert.Equal(("x^2", false), (view.Tex, view.Display));
        Assert.Equal("Cho nhé", Text(t.Inlines!).Replace("  ", " "));
        Assert.IsType<Bold>(t.Inlines!.Last());
    }

    [AvaloniaFact]
    public void Math_div_is_display()
    {
        var view = Assert.IsType<MathView>(Assert.Single(Blocks(@"<div class=""math"">\[x=1\]</div>")));
        Assert.Equal(("x=1", true), (view.Tex, view.Display));
        // Phương án chỉ có một công thức \( \): giữ cỡ trong dòng.
        Assert.False(Assert.IsType<MathView>(Assert.Single(Blocks(@"<div class=""math"">\(r&lt;38\)</div>"))).Display);
        Assert.Equal("r<38", ((MathView)Blocks(@"<div class=""math"">\(r&lt;38\)</div>")[0]).Tex);
    }

    [AvaloniaFact]
    public void Callout_is_border_with_class()
    {
        var b = Assert.IsType<Border>(Assert.Single(Blocks(@"<div class=""warn"">Chú ý <b>x</b></div>")));
        Assert.Contains("callout", b.Classes);
        Assert.Contains("warn", b.Classes);
        Assert.IsType<TextBlock>(Assert.Single(((StackPanel)b.Child!).Children));
    }

    [AvaloniaFact]
    public void Table_is_grid()
    {
        var g = Assert.IsType<Grid>(Assert.Single(Blocks("<table><thead><tr><th>a</th><th>b</th><th>c</th></tr></thead><tbody><tr><td>1</td><td colspan=\"2\">2</td></tr></tbody></table>")));
        Assert.Equal((2, 3), (g.RowDefinitions.Count, g.ColumnDefinitions.Count));
        Assert.Equal(5, g.Children.Count);
        Assert.Contains("th", g.Children[0].Classes);
        Assert.Equal(2, Grid.GetColumnSpan(g.Children[4]));
    }

    [AvaloniaFact]
    public void Lists_and_paragraph_split()
    {
        var blocks = Blocks("Trước<ul><li>một</li><li>hai</li></ul><ol><li>a</li></ol>sau");
        Assert.Equal(4, blocks.Count);
        Assert.Equal(2, ((StackPanel)blocks[1]).Children.Count);
        var marker = (TextBlock)((Grid)((StackPanel)blocks[2]).Children[0]).Children[0];
        Assert.Equal("1.", marker.Text);
    }

    [AvaloniaFact]
    public void Image_from_data_uri()
    {
        var t = Assert.IsType<TextBlock>(Assert.Single(Blocks($"<p>Hình <img src=\"data:image/png;base64,{Png1x1}\" alt=\"x\" width=\"20\"></p>")));
        var img = Assert.IsType<Image>(Assert.Single(t.Inlines!.OfType<InlineUIContainer>()).Child);
        Assert.NotNull(img.Source);
        Assert.Equal(20, img.MaxWidth);
        // Ảnh hỏng thì bỏ, không ném lỗi.
        Assert.Empty(Assert.IsType<TextBlock>(Assert.Single(Blocks("<p>x<img src=\"data:image/png;base64,@@\"></p>"))).Inlines!.OfType<InlineUIContainer>());
    }

    [AvaloniaFact]
    public void Unknown_block_shows_text()
    {
        var t = Assert.IsType<TextBlock>(Assert.Single(Blocks("<p><mark>a</mark><small>b</small><span>c</span></p>")));
        Assert.Equal("abc", Text(t.Inlines!));
        var q = Assert.IsType<Border>(Assert.Single(Blocks("<blockquote>trích</blockquote>")));
        Assert.Contains("quote", q.Classes);
        Assert.IsType<Separator>(Assert.Single(Blocks("<hr>")));
    }

    [AvaloniaFact]
    public void Display_tex_inside_text_breaks_line()
    {
        var t = Assert.IsType<TextBlock>(Assert.Single(Blocks(@"Ta có \[x=2\] nên")));
        var view = (MathView)t.Inlines!.OfType<InlineUIContainer>().Single().Child!;
        Assert.True(view.Display);
        Assert.Equal(2, t.Inlines!.OfType<LineBreak>().Count());
    }

    [AvaloniaFact]
    public void Renders_in_window_without_error()
    {
        var html = string.Concat(
            @"<h2>Tiêu đề</h2><p>Đoạn \(a^2+b^2\) <code>x</code> H<sub>2</sub>O x<sup>2</sup></p>",
            @"<div class=""math"">\[\int_0^1 f\]</div><div class=""keys"">SHIFT<br>CALC</div>",
            "<table><tr><td>1</td></tr></table><pre>code</pre>");
        var window = new Window { Content = ContentRenderer.Render(html), Width = 500, Height = 400 };
        window.Resources["Text"] = Avalonia.Media.Brushes.Black;
        window.Show();
        Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
    }
}
