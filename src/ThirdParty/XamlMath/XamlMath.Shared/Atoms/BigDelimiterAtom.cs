using XamlMath.Boxes;

namespace XamlMath.Atoms;

// Bản vendored: \big, \Big, \bigg, \Bigg. Delimiter cao cố định Size em (1,2 / 1,8 / 2,4 / 3,0 như KaTeX), căn giữa trục toán.
internal sealed record BigDelimiterAtom(SourceSpan? Source, SymbolAtom Delimiter, double Size, TexAtomType Kind) : Atom(Source, Kind)
{
    protected override Box CreateBoxCore(TexEnvironment environment)
    {
        var font = environment.MathFont;
        var em = font.GetQuad(font.GetMuFontId(), environment.Style);
        var box = DelimiterFactory.CreateBox(Delimiter.Name, Size * em, environment, Source);
        var axis = font.GetAxisHeight(environment.Style);
        box.Shift = -((box.Height + box.Depth) / 2 - box.Height) - axis;
        // Bọc trong HorizontalBox để Shift có tác dụng như delimiter của \left \right.
        return new HorizontalBox(box);
    }

    public override TexAtomType GetLeftType() => Kind;

    public override TexAtomType GetRightType() => Kind;
}
