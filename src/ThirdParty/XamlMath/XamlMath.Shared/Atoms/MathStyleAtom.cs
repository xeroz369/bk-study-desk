using XamlMath.Boxes;

namespace XamlMath.Atoms;

// Bản vendored: đổi style (Display, Text, Script) cho phần thân, dùng cho \displaystyle, \textstyle, \dfrac, \tfrac.
internal sealed record MathStyleAtom(SourceSpan? Source, Atom? Body, TexStyle Style) : Atom(Source)
{
    protected override Box CreateBoxCore(TexEnvironment environment) =>
        Body?.CreateBox(environment with { Style = Style }) ?? StrutBox.Empty;

    public override TexAtomType GetLeftType() => Body?.GetLeftType() ?? TexAtomType.Ordinary;

    public override TexAtomType GetRightType() => Body?.GetRightType() ?? TexAtomType.Ordinary;
}
