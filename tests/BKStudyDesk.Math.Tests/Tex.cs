using AvaloniaMath.Parsers;
using AvaloniaMath.Rendering;
using XamlMath;
using XamlMath.Boxes;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace BKStudyDesk.Math.Tests;

// Lối tắt cho test: parse, dựng box, gom lỗi thành một dòng.
internal static class Tex
{
    public const double Scale = 20;

    public static TexEnvironment Env(TexStyle style = TexStyle.Display) =>
        AvaloniaTeXEnvironment.Create(style: style, scale: Scale, systemTextFontName: "Arial");

    public static TexFormula Parse(string tex) => AvaloniaTeXFormulaParser.Instance.Parse(tex);

    public static Box Layout(string tex, TexStyle style = TexStyle.Display) => Parse(tex).CreateBox(Env(style));

    public static double Width(string tex) => Layout(tex).TotalWidth;

    public static double Height(string tex, TexStyle style = TexStyle.Display) => Layout(tex, style).TotalHeight;

    // 1 em của font toán (quad), cùng đơn vị với kích thước box.
    public static double Em => Env().MathFont.GetQuad(Env().MathFont.GetMuFontId(), TexStyle.Display);

    // Dựng box rồi vẽ thật: lỗi thiếu glyph chỉ ném ra lúc Render, không phải lúc parse.
    public static void Draw(string tex)
    {
        var box = Layout(tex);
        using var bitmap = new RenderTargetBitmap(new PixelSize(64, 64));
        using var context = bitmap.CreateDrawingContext();
        XamlMath.Rendering.TeXFormulaExtensions.Render(box, new AvaloniaElementRenderer(context, Scale, null, Brushes.Black), 0, 0);
    }

    public static string? Error(string tex)
    {
        try
        {
            Draw(tex);
            return null;
        }
        catch (Exception e)
        {
            return $"{e.GetType().Name}: {e.Message.Split('\n')[0]}";
        }
    }
}
