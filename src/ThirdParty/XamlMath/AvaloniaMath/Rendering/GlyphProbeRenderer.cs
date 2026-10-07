using System.Collections.Generic;
using AvaloniaMath.Fonts;
using XamlMath;
using XamlMath.Boxes;
using XamlMath.Rendering;
using XamlMath.Rendering.Transformations;

namespace AvaloniaMath.Rendering;

/// <summary>
/// Bản vendored: "vẽ" khô để lấy glyph của mọi ký tự, không vẽ gì. Ký tự không có trong font ném lỗi ngay lúc dựng công thức,
/// thay vì ném trong Render (lỗi trong Render làm sập cả cửa sổ).
/// </summary>
internal sealed class GlyphProbeRenderer(double scale) : IElementRenderer
{
    public void RenderElement(Box box, double x, double y) => box.RenderTo(this, x, y);

    public void RenderCharacter(CharInfo info, double x, double y, IBrush? foreground) => info.GetGlyphRun(x, y, scale);

    public void RenderLine(Point point0, Point point1, IBrush? foreground) { }

    public void RenderRectangle(Rectangle rectangle, IBrush? foreground) { }

    public void RenderTransformed(Box box, IEnumerable<Transformation> transforms, double x, double y) => RenderElement(box, x, y);

    public void FinishRendering() { }
}
