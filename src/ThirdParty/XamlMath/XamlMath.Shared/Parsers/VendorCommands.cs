using System.Collections.Generic;
using System.Linq;
using XamlMath.Atoms;

namespace XamlMath.Parsers;

// Bản vendored: lệnh thêm cho nội dung của BK Study Desk (đo trên corpus studypack/examples và content/).
internal static class VendorCommands
{
    // \displaystyle, \textstyle...: đổi style cho phần còn lại của nhóm hiện tại, như TeX.
    private sealed class StyleSwitchCommand(TexStyle style) : ICommandParser
    {
        public CommandProcessingResult ProcessCommand(CommandContext context)
        {
            var source = context.CommandSource;
            var rest = context.Parser.Parse(
                source.Segment(context.ArgumentsStartPosition),
                context.Formula.TextStyle,
                context.Environment).RootAtom;
            var atomSource = source.Segment(context.CommandNameStartPosition);
            return new CommandProcessingResult(new MathStyleAtom(atomSource, rest, style), source.Length);
        }
    }

    // \dfrac, \tfrac: \frac đặt trong style cố định.
    private sealed class StyledFractionCommand(TexStyle style) : ICommandParser
    {
        public CommandProcessingResult ProcessCommand(CommandContext context)
        {
            var source = context.CommandSource;
            var position = context.ArgumentsStartPosition;
            var afterTop = TexFormulaParser.ReadElement(source, position);
            position = afterTop.position;
            var top = context.Parser.Parse(afterTop.source, context.Formula.TextStyle, context.Environment.CreateChildEnvironment());
            var afterBottom = TexFormulaParser.ReadElement(source, position);
            position = afterBottom.position;
            var bottom = context.Parser.Parse(afterBottom.source, context.Formula.TextStyle, context.Environment.CreateChildEnvironment());
            var start = context.CommandNameStartPosition;
            var atomSource = source.Segment(start, position - start);
            var fraction = new FractionAtom(atomSource, top.RootAtom, bottom.RootAtom, true);
            return new CommandProcessingResult(new MathStyleAtom(atomSource, fraction, style), position);
        }
    }

    // \big( ... \Bigg): delimiter cỡ cố định, không cần cặp \left \right.
    private sealed class BigDelimiterCommand(double size, TexAtomType type) : ICommandParser
    {
        public CommandProcessingResult ProcessCommand(CommandContext context)
        {
            var source = context.CommandSource;
            var start = context.CommandNameStartPosition;
            var position = context.ArgumentsStartPosition;
            while (position < source.Length && char.IsWhiteSpace(source[position])) position++;
            var delimiter = TexFormulaParser.ParseDelimiter(source, start, ref position);
            var atomSource = source.Segment(start, position - start);
            return new CommandProcessingResult(new BigDelimiterAtom(atomSource, delimiter, size, type), position);
        }
    }

    // \mathbb: font kèm theo không có chữ kép (msbm). Chữ có bản Unicode trong khối BMP (ℂ ℍ ℕ ℙ ℚ ℝ ℤ)
    // vẽ bằng font chữ thường của hệ thống (có font dự phòng), chữ khác giữ nguyên dạng đứng.
    private sealed class BlackboardCommand : ICommandParser
    {
        private static readonly Dictionary<char, char> Letters = new()
        {
            ['C'] = 'ℂ', ['H'] = 'ℍ', ['N'] = 'ℕ', ['P'] = 'ℙ',
            ['Q'] = 'ℚ', ['R'] = 'ℝ', ['Z'] = 'ℤ',
        };

        public CommandProcessingResult ProcessCommand(CommandContext context)
        {
            var source = context.CommandSource;
            var after = TexFormulaParser.ReadElement(source, context.ArgumentsStartPosition);
            var text = new string(after.source.ToString().Select(c => Letters.TryGetValue(c, out var bb) ? bb : c).ToArray());
            var span = new SourceSpan(after.source.SourceName, text, 0, text.Length);
            var formula = TexFormulaParser.ConvertRawText(span, TexUtilities.TextStyleName);
            return new CommandProcessingResult(formula.RootAtom, after.position);
        }
    }

    public static void Register(IDictionary<string, ICommandParser> commands)
    {
        commands["displaystyle"] = new StyleSwitchCommand(TexStyle.Display);
        commands["textstyle"] = new StyleSwitchCommand(TexStyle.Text);
        commands["scriptstyle"] = new StyleSwitchCommand(TexStyle.Script);
        commands["scriptscriptstyle"] = new StyleSwitchCommand(TexStyle.ScriptScript);
        commands["dfrac"] = new StyledFractionCommand(TexStyle.Display);
        commands["tfrac"] = new StyledFractionCommand(TexStyle.Text);
        commands["mathbb"] = new BlackboardCommand();
        foreach (var (name, size) in new[] { ("big", 1.2), ("Big", 1.8), ("bigg", 2.4), ("Bigg", 3.0) })
        {
            commands[name] = new BigDelimiterCommand(size, TexAtomType.Ordinary);
            commands[name + "l"] = new BigDelimiterCommand(size, TexAtomType.Opening);
            commands[name + "r"] = new BigDelimiterCommand(size, TexAtomType.Closing);
            commands[name + "m"] = new BigDelimiterCommand(size, TexAtomType.Relation);
        }
    }
}
