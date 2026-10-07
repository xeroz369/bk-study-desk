using System.Collections.Generic;
using XamlMath.Atoms;
using XamlMath.Exceptions;

namespace XamlMath.Parsers.Matrices;

// Bản vendored: \begin{array}{c|ccc} ... \end{array}. Cột l, c, r; vạch | giữa các cột; \hline giữa các hàng.
// Dùng cho bảng chân trị, bảng xét dấu, bảng sai phân trong đề.
internal sealed class ArrayEnvironmentParser : IEnvironmentParser
{
    public static readonly ArrayEnvironmentParser Instance = new();

    private const double ArrayColumnPadding = 1.0;

    // Trong thân array: & sang ô mới, \ sang hàng mới, \hline ghi vạch ngang trước hàng hiện tại.
    private sealed class ArrayInternalEnvironment(ICommandEnvironment parent, List<List<Atom>> rows, List<int> hlines)
        : NonRecursiveEnvironment(parent.CreateChildEnvironment(), Commands(rows, hlines))
    {
        private static IReadOnlyDictionary<string, ICommandParser> Commands(List<List<Atom>> rows, List<int> hlines)
        {
            var next = new NextRowCommand(rows);
            return new Dictionary<string, ICommandParser>
            {
                [@"\"] = next,
                ["cr"] = next,
                ["hline"] = new HlineCommand(rows, hlines),
            };
        }

        public override bool ProcessUnknownCharacter(TexFormula formula, char character)
        {
            if (character != '&') return false;
            NextRowCommand.NextCell(rows, formula);
            return true;
        }
    }

    private sealed class HlineCommand(List<List<Atom>> rows, List<int> hlines) : ICommandParser
    {
        public CommandProcessingResult ProcessCommand(CommandContext context)
        {
            var row = rows.Count - 1;
            if (!hlines.Contains(row)) hlines.Add(row);
            return new CommandProcessingResult(null, context.ArgumentsStartPosition);
        }
    }

    public EnvironmentProcessingResult ProcessEnvironment(EnvironmentContext context)
    {
        var body = context.EnvironmentBodySource;
        var afterSpec = TexFormulaParser.ReadElement(body, 0);
        var (alignments, vlines) = ReadColumnSpec(afterSpec.source.ToString());

        var rows = new List<List<Atom>> { new() };
        var hlines = new List<int>();
        var environment = new ArrayInternalEnvironment(context.Environment, rows, hlines);
        var cellsSource = body.Segment(afterSpec.position);
        var last = context.Parser.Parse(cellsSource, context.Formula.TextStyle, environment).RootAtom;
        if (last != null) rows[^1].Add(last);
        MatrixCommandParser.Normalize(rows);

        // \arraycolsep của LaTeX: 5pt mỗi bên cột, tức 1 em giữa hai cột (mặc định của MatrixAtom chỉ 0,35 em).
        var matrix = new MatrixAtom(context.EnvironmentSource, rows, MatrixCellAlignment.Center, horizontalPadding: ArrayColumnPadding)
        {
            ColumnAlignments = alignments,
            VerticalRules = vlines,
            HorizontalRules = hlines,
        };
        return new EnvironmentProcessingResult(matrix);
    }

    internal static (List<MatrixCellAlignment> Alignments, List<int> VerticalRules) ReadColumnSpec(string spec)
    {
        var alignments = new List<MatrixCellAlignment>();
        var vlines = new List<int>();
        foreach (var c in spec)
        {
            switch (c)
            {
                case 'l': alignments.Add(MatrixCellAlignment.Left); break;
                case 'c': alignments.Add(MatrixCellAlignment.Center); break;
                case 'r': alignments.Add(MatrixCellAlignment.Right); break;
                case '|': if (!vlines.Contains(alignments.Count)) vlines.Add(alignments.Count); break;
                case ' ': break;
                default: throw new TexParseException($"Unsupported column type '{c}' in array spec \"{spec}\".");
            }
        }
        return (alignments, vlines);
    }
}
