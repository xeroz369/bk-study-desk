using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Ui;

namespace SoHocTap.Presentation.Practice;

public sealed record AuthorOption(string Text, bool Right);

public sealed partial class AuthorForm
{
    public static readonly string[] Types = ["single", "multi", "truefalse", "numeric", "short"];

    public string Type { get; set; } = "single";
    public string Prompt { get; set; } = "";
    public string Solution { get; set; } = "";
    public string Tag { get; set; } = "";
    public bool KeepOrder { get; set; }
    public List<AuthorOption> Options { get; set; } = [];
    public bool Truth { get; set; } = true;
    public string Value { get; set; } = "";
    public string Tolerance { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Accept { get; set; } = "";

    public static string Render(string md) => Html.Sanitize(StudyMarkdown.MdToHtml(md));

    [GeneratedRegex(@"^<p>([\s\S]*)</p>$")] private static partial Regex OnePara();

    public (PackQuestion? Q, string? Error) Build()
    {
        if (Text.TrimJs(Prompt).Length == 0) return (null, L.T("practice.author.err.prompt"));
        if (Text.TrimJs(Solution).Length == 0) return (null, L.T("practice.author.err.solution"));
        var q = new PackQuestion { Tag = Text.TrimJs(Tag) is { Length: > 0 } t ? t : null, Prompt = Render(Prompt), Solution = Render(Solution) };
        switch (Type)
        {
            case "single" or "multi":
                var opts = Options.Where(o => Text.TrimJs(o.Text).Length > 0).ToList();
                if (opts.Count < 2) return (null, L.T("practice.author.err.options"));
                var right = opts.Select((o, i) => o.Right ? i : -1).Where(i => i >= 0).ToArray();
                if (right.Length == 0) return (null, L.T("practice.author.err.right"));
                q.Options = [.. opts.Select(o => OnePara().Replace(Render(o.Text), "$1"))];
                q.KeepOrder = KeepOrder ? true : null;
                if (Type == "single") q.Answer = JsonValue.Create(right[0]);
                else (q.Type, q.Answers) = ("multi", right);
                return (q, null);
            case "truefalse":
                (q.Type, q.Answer) = ("truefalse", JsonValue.Create(Truth));
                return (q, null);
            case "numeric":
                var v = Grade.ParseNumber(Value);
                var tol = Text.TrimJs(Tolerance).Length > 0 ? Grade.ParseNumber(Tolerance) : 0;
                if (v is null) return (null, L.T("practice.author.err.value"));
                if (tol is null or < 0) return (null, L.T("practice.author.err.tolerance"));
                q.Type = "numeric";
                q.Answer = JsonValue.Create(v.Value);
                q.Tolerance = tol != 0 ? tol : null;
                q.Unit = Text.TrimJs(Unit) is { Length: > 0 } u ? u : null;
                return (q, null);
            default:
                var acc = Accept.Split('|').Select(Text.TrimJs).Where(s => s.Length > 0).ToArray();
                if (acc.Length == 0) return (null, L.T("practice.author.err.accept"));
                (q.Type, q.Accept) = ("short", acc);
                return (q, null);
        }
    }
}
