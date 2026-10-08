using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Desktop.Views.Practice;

/// <summary>
/// Vẽ HTML đã lọc (<see cref="Html.Sanitize"/>) của bài học, câu hỏi bằng control có sẵn: đoạn văn là TextBlock với Inlines
/// (công thức trong dòng là InlineUIContainer chứa <see cref="MathView"/>), khối công thức là MathView, khối ghi chú là Border,
/// bảng là Grid, ảnh data URI là Image. Màu, cỡ, khoảng cách theo class (style ở Styles/Tokens.axaml), không viết số ở đây.
/// Thẻ chưa vẽ riêng thì hiện chữ bên trong, không mất nội dung.
/// </summary>
public static partial class ContentRenderer
{
    private static readonly HashSet<string> Blocks = ["p", "div", "ul", "ol", "table", "pre", "blockquote", "hr", "h2", "h3"];
    private static readonly HashSet<string> Callouts = ["warn", "keys", "tip", "note"];

    [GeneratedRegex(@"\\\(([\s\S]+?)\\\)|\\\[([\s\S]+?)\\\]|\$\$([\s\S]+?)\$\$")] private static partial Regex Tex();
    [GeneratedRegex(@"[ \t\r\n]+")] private static partial Regex Spaces();

    public static Control Render(string sanitizedHtml)
    {
        var panel = new StackPanel { Classes = { "content" } };
        AddBlocks(panel.Children, Html.Parse(sanitizedHtml));
        return panel;
    }

    // Gom các node trong dòng liền nhau thành một đoạn; thẻ khối thì thành control riêng.
    private static void AddBlocks(Avalonia.Controls.Controls output, IEnumerable<HtmlNode> nodes)
    {
        var inline = new List<HtmlNode>();
        void Flush()
        {
            if (inline.Count > 0 && Html.TextOf(inline).Trim().Length + inline.Count(n => n is HtmlElement { Tag: "img" }) > 0)
                output.Add(Paragraph(inline));
            inline = [];
        }
        foreach (var n in nodes)
        {
            if (n is HtmlElement e && Blocks.Contains(e.Tag))
            {
                Flush();
                output.Add(Block(e));
            }
            else inline.Add(n);
        }
        Flush();
    }

    private static Control Block(HtmlElement e)
    {
        switch (e.Tag)
        {
            case "div" when e.Classes.Contains("math"):
                return (Control?)MathBlock(Html.TextOf(e).Trim()) ?? Paragraph(e.Children);
            case "div" when e.Classes.FirstOrDefault(Callouts.Contains) is { } kind:
                {
                    var inner = new StackPanel { Classes = { "content" } };
                    AddBlocks(inner.Children, e.Children);
                    return new Border { Classes = { "callout", kind }, Child = inner };
                }
            case "div":
                {
                    var inner = new StackPanel { Classes = { "content" } };
                    AddBlocks(inner.Children, e.Children);
                    return inner;
                }
            case "ul" or "ol":
                return List(e, e.Tag == "ol");
            case "table":
                return Table(e);
            case "pre":
                return new TextBlock { Classes = { "pre" }, Text = Html.TextOf(e) };
            case "blockquote":
                {
                    var inner = new StackPanel { Classes = { "content" } };
                    AddBlocks(inner.Children, e.Children);
                    return new Border { Classes = { "quote" }, Child = inner };
                }
            case "hr":
                return new Separator();
            default:   // p, h2, h3
                {
                    var t = Paragraph(e.Children);
                    if (e.Tag != "p") t.Classes.Add(e.Tag);
                    return t;
                }
        }
    }

    /// <summary>Khối công thức: \[ \], $$ $$ là Display; khối chỉ có \( \) (phương án một công thức) giữ cỡ trong dòng.</summary>
    private static MathView? MathBlock(string tex)
    {
        if (tex.StartsWith(@"\[", StringComparison.Ordinal) && tex.EndsWith(@"\]", StringComparison.Ordinal)) return new MathView { Tex = tex[2..^2], Display = true };
        if (tex.StartsWith("$$", StringComparison.Ordinal) && tex.EndsWith("$$", StringComparison.Ordinal) && tex.Length >= 4) return new MathView { Tex = tex[2..^2], Display = true };
        if (tex.StartsWith(@"\(", StringComparison.Ordinal) && tex.EndsWith(@"\)", StringComparison.Ordinal)) return new MathView { Tex = tex[2..^2] };
        return null;
    }

    private static Control List(HtmlElement e, bool ordered)
    {
        var panel = new StackPanel { Classes = { "content", "list" } };
        var n = 0;
        foreach (var li in e.Children.OfType<HtmlElement>().Where(c => c.Tag == "li"))
        {
            n++;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            row.Children.Add(new TextBlock { Classes = { "marker" }, Text = ordered ? $"{n}." : "•" });
            var body = new StackPanel { Classes = { "content" } };
            AddBlocks(body.Children, li.Children);
            Grid.SetColumn(body, 1);
            row.Children.Add(body);
            panel.Children.Add(row);
        }
        return panel;
    }

    private static Control Table(HtmlElement table)
    {
        IEnumerable<HtmlElement> Rows(HtmlElement el) => el.Children.OfType<HtmlElement>().SelectMany(c => c.Tag switch
        {
            "tr" => [c],
            "thead" or "tbody" or "tfoot" => Rows(c),
            _ => [],
        });
        static int Span(HtmlElement cell, string attr) => int.TryParse(cell.Attr(attr), out var v) && v > 0 ? v : 1;
        var rows = Rows(table).ToList();
        var grid = new Grid { Classes = { "table" }, HorizontalAlignment = HorizontalAlignment.Left };
        // Ô bị rowspan của hàng trên chiếm: đánh dấu để ô sau dời sang phải, như bảng HTML.
        var taken = new HashSet<(int, int)>();
        var columns = 0;
        for (var r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var c = 0;
            foreach (var cell in rows[r].Children.OfType<HtmlElement>().Where(x => x.Tag is "td" or "th"))
            {
                while (taken.Contains((r, c))) c++;
                int cs = Span(cell, "colspan"), rs = Span(cell, "rowspan");
                for (var dr = 0; dr < rs; dr++)
                    for (var dc = 0; dc < cs; dc++) taken.Add((r + dr, c + dc));
                var inner = new StackPanel { Classes = { "content" } };
                AddBlocks(inner.Children, cell.Children);
                var border = new Border { Classes = { "cell" }, Child = inner };
                if (cell.Tag == "th") border.Classes.Add("th");
                Grid.SetRow(border, r);
                Grid.SetColumn(border, c);
                Grid.SetColumnSpan(border, cs);
                Grid.SetRowSpan(border, rs);
                grid.Children.Add(border);
                c += cs;
                columns = System.Math.Max(columns, c);
            }
        }
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var i = grid.RowDefinitions.Count; i < taken.Select(t => t.Item1 + 1).DefaultIfEmpty(0).Max(); i++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        return grid;
    }

    private static TextBlock Paragraph(IEnumerable<HtmlNode> nodes)
    {
        var t = new TextBlock { Classes = { "content" } };
        var inlines = new InlineCollection();
        AddInlines(inlines, nodes);
        // Khoảng trắng đầu, cuối đoạn không hiện (như HTML).
        if (inlines.FirstOrDefault() is Run first) first.Text = first.Text?.TrimStart();
        if (inlines.LastOrDefault() is Run last) last.Text = last.Text?.TrimEnd();
        t.Inlines = inlines;
        return t;
    }

    private static void AddInlines(InlineCollection output, IEnumerable<HtmlNode> nodes)
    {
        foreach (var n in nodes)
        {
            if (n is HtmlText text)
            {
                AddText(output, text.Text);
                continue;
            }
            var e = (HtmlElement)n;
            Span? span = e.Tag switch
            {
                "b" or "strong" => new Bold(),
                "i" or "em" => new Italic(),
                "u" => new Underline(),
                "code" => new Span { Classes = { "code" } },
                "sub" => new Span { Classes = { "sub" }, BaselineAlignment = BaselineAlignment.Subscript },
                "sup" => new Span { Classes = { "sup" }, BaselineAlignment = BaselineAlignment.Superscript },
                "br" or "img" => null,
                _ => new Span(),   // span, small, mark... chưa có kiểu riêng: giữ chữ
            };
            if (e.Tag == "br") output.Add(new LineBreak());
            else if (e.Tag == "img") { if (Image(e) is { } img) output.Add(new InlineUIContainer { Child = img }); }
            else
            {
                AddInlines(span!.Inlines, e.Children);
                output.Add(span);
            }
        }
    }

    // Chữ trong dòng: gộp khoảng trắng như HTML; \( \) thành công thức trong dòng, \[ \] và $$ $$ thành công thức đứng riêng.
    private static void AddText(InlineCollection output, string text)
    {
        var at = 0;
        foreach (Match m in Tex().Matches(text))
        {
            if (m.Index > at) output.Add(new Run(Spaces().Replace(text[at..m.Index], " ")));
            if (m.Groups[1].Success) output.Add(new InlineUIContainer { Child = new MathView { Tex = m.Groups[1].Value } });
            else
            {
                output.Add(new LineBreak());
                output.Add(new InlineUIContainer { Child = new MathView { Tex = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value, Display = true } });
                output.Add(new LineBreak());
            }
            at = m.Index + m.Length;
        }
        if (at < text.Length) output.Add(new Run(Spaces().Replace(text[at..], " ")));
    }

    /// <summary>Ảnh nhúng data URI (bộ lọc đã chỉ giữ PNG, JPEG, GIF, WebP). Hỏng thì bỏ qua.</summary>
    private static Image? Image(HtmlElement e)
    {
        var src = e.Attr("src") ?? "";
        var comma = src.IndexOf(',');
        if (!src.StartsWith("data:image/", StringComparison.Ordinal) || comma < 0) return null;
        try
        {
            var bytes = Convert.FromBase64String(src[(comma + 1)..]);
            var img = new Image { Classes = { "content" }, Source = new Bitmap(new MemoryStream(bytes)), Stretch = Stretch.Uniform };
            if (double.TryParse(e.Attr("width"), out var w)) img.MaxWidth = w;
            return img;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
