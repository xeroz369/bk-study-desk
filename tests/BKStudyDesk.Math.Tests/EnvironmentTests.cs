using Avalonia.Headless.XUnit;
using XamlMath;
using XamlMath.Atoms;

namespace BKStudyDesk.Math.Tests;

public class EnvironmentTests
{
    private static Atom Root(string tex) => Tex.Parse(tex).RootAtom is RowAtom { Elements.Count: 1 } row ? row.Elements[0] : Tex.Parse(tex).RootAtom!;

    private static MatrixAtom Matrix(Atom atom) => atom switch
    {
        MatrixAtom m => m,
        FencedAtom { BaseAtom: MatrixAtom m } => m,
        _ => throw new Xunit.Sdk.XunitException($"không phải ma trận: {atom.GetType().Name}"),
    };

    [AvaloniaFact]
    public void Cases_has_left_brace_only()
    {
        var fenced = Assert.IsType<FencedAtom>(Root(@"\begin{cases} x & x>0 \\ -x & x\le0 \end{cases}"));
        Assert.Equal("lbrace", fenced.LeftDelimeter?.Name);
        Assert.Null(fenced.RightDelimeter);
        var m = Matrix(fenced);
        Assert.Equal(2, m.MatrixCells.Count);
        Assert.All(m.MatrixCells, r => Assert.Equal(2, r.Count));
        Assert.Equal(MatrixCellAlignment.Left, m.MatrixCellAlignment);
    }

    [AvaloniaFact]
    public void Array_reads_column_spec_and_rules()
    {
        var m = Matrix(Root(@"\begin{array}{c|ccc} x & 1 & 2 & 3 \\ \hline y & 4 & 5 & 6 \end{array}"));
        Assert.Equal(2, m.MatrixCells.Count);
        Assert.All(m.MatrixCells, r => Assert.Equal(4, r.Count));
        Assert.Equal(Enumerable.Repeat(MatrixCellAlignment.Center, 4), m.ColumnAlignments);
        Assert.Equal([1], m.VerticalRules);
        Assert.Equal([1], m.HorizontalRules);
    }

    [AvaloniaFact]
    public void Array_left_right_columns()
    {
        var m = Matrix(Root(@"\begin{array}{lr} a & b \end{array}"));
        Assert.Equal([MatrixCellAlignment.Left, MatrixCellAlignment.Right], m.ColumnAlignments);
    }

    [AvaloniaTheory]
    [InlineData(@"\begin{cases} a \\ b \\ \end{cases}")]
    [InlineData(@"\begin{pmatrix} 1 \\ 2 \\ \end{pmatrix}")]
    [InlineData(@"\begin{array}{c} a \\ b \\ \end{array}")]
    public void Trailing_row_break_adds_no_row(string tex) => Assert.Equal(2, Matrix(Root(tex)).MatrixCells.Count);

    [AvaloniaFact]
    public void Hline_at_the_end_is_a_bottom_rule()
    {
        var m = Matrix(Root(@"\begin{array}{cc} \hline a & b \\ \hline \end{array}"));
        Assert.Single(m.MatrixCells);
        Assert.Equal([0, 1], m.HorizontalRules);
    }

    [AvaloniaFact]
    public void Vertical_rule_takes_space_and_is_drawn()
    {
        Assert.True(Tex.Width(@"\begin{array}{c|c} 1 & 2 \end{array}") > Tex.Width(@"\begin{array}{cc} 1 & 2 \end{array}"));
        // Cột pixel tối chạy dọc gần hết chiều cao bảng: đó là vạch |.
        var px = Frames.Pixels(new AvaloniaMath.Controls.FormulaBlock
        {
            Formula = @"\begin{array}{c|c} 1 & 2 \\ 3 & 4 \\ 5 & 6 \end{array}",
            Foreground = Avalonia.Media.Brushes.Black,
        });
        var (top, bottom) = Frames.InkRows(px);
        var tallest = Enumerable.Range(0, px.GetLength(1)).Max(x => Enumerable.Range(top, bottom - top + 1).Count(y => px[y, x].R < 128));
        Assert.True(tallest >= 0.9 * (bottom - top + 1), $"cột tối dài nhất {tallest}/{bottom - top + 1}");
    }

    [AvaloniaTheory]
    [InlineData(@"\begin{bmatrix} 1 & 2 \end{bmatrix}", "lbrack", "rbrack")]
    [InlineData(@"\begin{vmatrix} 1 & 2 \end{vmatrix}", "vert", "vert")]
    [InlineData(@"\begin{Vmatrix} 1 & 2 \end{Vmatrix}", "Vert", "Vert")]
    [InlineData(@"\begin{Bmatrix} 1 & 2 \end{Bmatrix}", "lbrace", "rbrace")]
    public void Matrix_family_delimiters(string tex, string left, string right)
    {
        var fenced = Assert.IsType<FencedAtom>(Root(tex));
        Assert.Equal(left, fenced.LeftDelimeter?.Name);
        Assert.Equal(right, fenced.RightDelimeter?.Name);
    }

    [AvaloniaFact]
    public void Plain_matrix_environment() => Assert.IsType<MatrixAtom>(Root(@"\begin{matrix} 1 & 2 \end{matrix}"));
}
