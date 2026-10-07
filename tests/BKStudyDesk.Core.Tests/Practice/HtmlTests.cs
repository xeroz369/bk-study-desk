using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

public class HtmlTests
{
    private static HtmlElement El(HtmlNode n) => Assert.IsType<HtmlElement>(n);

    [Fact]
    public void Parses_nested_tags_and_attributes()
    {
        var nodes = Html.Parse("<div class='warn x' data-a=\"1\" title=t>A<b>đậm</b><br/>B<img src=\"data:image/png;base64,AA\" alt=\"x\"></div>");
        var div = El(Assert.Single(nodes));
        Assert.Equal("div", div.Tag);
        Assert.Equal(["warn", "x"], div.Classes);
        Assert.Equal("1", div.Attr("data-a"));
        Assert.Equal("t", div.Attr("title"));
        Assert.Equal(["text", "b", "br", "text", "img"], div.Children.Select(c => c is HtmlElement e ? e.Tag : "text"));
        Assert.Equal("đậm", Html.TextOf(div.Children[1]));
        Assert.Empty(El(div.Children[2]).Children);
    }

    [Fact]
    public void Decodes_entities()
    {
        var t = Assert.IsType<HtmlText>(Assert.Single(Html.Parse("a&amp;b&lt;c&gt;&nbsp;&#39;&#x41;&quot;&unknown;")));
        Assert.Equal("a&b<c> 'A\"&unknown;", t.Text);
    }

    [Theory]
    [InlineData(@"\(f(0)<0\)")]
    [InlineData("x < 10 và y<=3")]
    [InlineData("a <- b")]
    [InlineData("1<2")]
    public void Less_than_in_text_is_text(string s) => Assert.Equal(s, Html.TextOf(Html.Parse(s)));

    [Theory]
    [InlineData("<p>a<b>b</p>c", "abc")]
    [InlineData("<div><p>x</div>y", "xy")]
    [InlineData("<b>a</i>b", "ab")]
    [InlineData("<ul><li>1<li>2</ul>", "12")]
    [InlineData("<p>a<!-- bỏ -->b</p>", "ab")]
    public void Unclosed_tags_keep_text(string html, string text) => Assert.Equal(text, Html.TextOf(Html.Parse(html)));

    [Fact]
    public void List_items_close_each_other()
    {
        var ul = El(Assert.Single(Html.Parse("<ul><li>1<li>2</ul>")));
        Assert.Equal(2, ul.Children.Count);
    }

    [Fact]
    public void Serialize_round_trips_tex_and_escapes()
    {
        // "a<b" thì trình duyệt cũng đọc thành thẻ <b> (vì vậy SPEC dặn viết \lt); "<" trước số, dấu cách là chữ.
        const string s = @"<p class=""warn"">\(a<1\) &amp; x</p>";
        var output = Html.Serialize(Html.Parse(s));
        Assert.Equal(@"<p class=""warn"">\(a&lt;1\) &amp; x</p>", output);
        Assert.Equal(@"\(a<1\) & x", Html.TextOf(Html.Parse(output)));
    }

    [Fact]
    public void Sanitize_follows_pack_rules()
    {
        Assert.Equal("<p>ab</p>", Html.Sanitize("<p>a<script>alert(1)</script>b</p>"));
        Assert.Equal("chữ", Html.Sanitize("<font color=red>chữ</font>"));
        Assert.Equal("<b>x</b>", Html.Sanitize("<b onclick=\"x()\" style=\"c\">x</b>"));
        Assert.Equal("<div class=\"warn\">w</div>", Html.Sanitize("<div class=\"warn evil\">w</div>"));
        Assert.Equal("<div>w</div>", Html.Sanitize("<div class=\"evil\">w</div>"));
        Assert.Equal("a", Html.Sanitize("a<img src=\"https://x/y.png\">"));
        Assert.Equal("<img src=\"data:image/png;base64,AA\" alt=\"x\" width=\"20\">",
            Html.Sanitize("<img src=\"data:image/png;base64,AA\" alt=\"x\" width=\"20\" height=\"20px\" onerror=\"x\">"));
        Assert.Equal("<table><tbody><tr><td colspan=\"2\">c</td></tr></tbody></table>",
            Html.Sanitize("<table><tbody><tr><td colspan=\"2\" rowspan=\"abc\">c</td></tr></tbody></table>"));
        Assert.Equal("", Html.Sanitize("<svg><script>x</script></svg><iframe src=x></iframe>"));
        Assert.Equal(@"\(x<1\)", Html.TextOf(Html.Parse(Html.Sanitize(@"\(x<1\)"))));
    }
}
