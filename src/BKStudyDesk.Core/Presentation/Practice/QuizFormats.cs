using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SoHocTap.Ui;

namespace SoHocTap.Presentation.Practice;

public enum QuizFormat { StudyPack, Markdown, MoodleXml, Gift, Aiken }

/// <summary>Kết quả đổi một file quiz: các bài (theo category) và ghi chú những gì bị bỏ.</summary>
public sealed record Converted(List<PackLesson> Lessons, List<string> Notes);

/// <summary>
/// Đổi qua lại giữa gói luyện tập và định dạng quiz của Moodle (port <c>formats.ts</c>; BK-LMS chạy Moodle):
/// Aiken (một đáp án, dễ soạn nhất), GIFT (dạng chữ, đủ loại), Moodle XML (ngân hàng câu hỏi, có ảnh và category).
/// Loại chưa hỗ trợ (ghép cặp, tự luận, cloze, kéo thả) bị bỏ và ghi vào Notes, không lặng lẽ.
/// </summary>
public static partial class QuizFormats
{
    // ------------------------------------------------------------------ nhận dạng

    [GeneratedRegex(@"^```\w*\s*|\s*```$")] private static partial Regex Fence();
    [GeneratedRegex(@"""format""\s*:\s*""studypack/")] private static partial Regex StudyPackFormat();
    [GeneratedRegex(@"<quiz[\s>]", RegexOptions.IgnoreCase)] private static partial Regex QuizTag();
    [GeneratedRegex(@"^---\s*$", RegexOptions.Multiline)] private static partial Regex FrontMatter();
    [GeneratedRegex(@"^###\s+", RegexOptions.Multiline)] private static partial Regex H3();
    [GeneratedRegex(@"^\s*([-*]\s+\[[ xX]\]|=\s|>)", RegexOptions.Multiline)] private static partial Regex AnswerShape();
    [GeneratedRegex(@"^\s*ANSWER\s*:\s*[A-Z]\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)] private static partial Regex AikenAnswer();
    [GeneratedRegex(@"\{[^}]*[=~#]")] private static partial Regex GiftLike();
    [GeneratedRegex(@"\{[^{}]*\}")] private static partial Regex Braces();
    [GeneratedRegex(@"[=~]|\{\s*(T|F|TRUE|FALSE)\s*\}|\{\s*#")] private static partial Regex GiftAnswer();

    public static string Name(QuizFormat f) => f switch
    {
        QuizFormat.StudyPack => "studypack",
        QuizFormat.Markdown => "markdown",
        QuizFormat.MoodleXml => "moodlexml",
        QuizFormat.Gift => "gift",
        _ => "aiken",
    };

    public static QuizFormat? Detect(string raw)
    {
        var t = Fence().Replace(Text.TrimJs(raw.Replace("\r", "")), "");
        if (t.StartsWith('{') && StudyPackFormat().IsMatch(t)) return QuizFormat.StudyPack;
        if (QuizTag().IsMatch(t)) return QuizFormat.MoodleXml;
        // Study Markdown trước GIFT: ngoặc nhọn của TeX (\frac{a}{b}) trông giống khối đáp án GIFT.
        if (FrontMatter().IsMatch(t.Split('\n')[0]) || (H3().IsMatch(t) && AnswerShape().IsMatch(t))) return QuizFormat.Markdown;
        if (AikenAnswer().IsMatch(t) && !GiftLike().IsMatch(t)) return QuizFormat.Aiken;
        if (Braces().IsMatch(t) && GiftAnswer().IsMatch(t)) return QuizFormat.Gift;
        if (t.StartsWith('{')) return QuizFormat.StudyPack;
        return null;
    }

    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    [GeneratedRegex(@"\n{2,}")] private static partial Regex ParaBreak();

    /// <summary>Chữ thường (Aiken, GIFT) thành đoạn HTML, giữ TeX nguyên.</summary>
    private static string Para(string s) =>
        string.Concat(ParaBreak().Split(Text.TrimJs(s)).Select(p => $"<p>{Esc(p).Replace("\n", "<br>")}</p>"));

    private static string Head(string s, int n) => s.Length > n ? s[..n] : s;

    // Number(x) và parseFloat(x) của JS.
    private static double JsNumber(string s)
    {
        var t = Text.TrimJs(s);
        if (t.Length == 0) return 0;
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;
    }

    [GeneratedRegex(@"^\s*[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?")] private static partial Regex FloatPrefix();

    private static double ParseFloat(string? s) =>
        s != null && FloatPrefix().Match(s) is { Success: true } m ? double.Parse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture) : double.NaN;

    // ------------------------------------------------------------------ Aiken

    [GeneratedRegex(@"^\s*([A-H])[.)]\s+(.*)$")] private static partial Regex AikenOption();
    [GeneratedRegex(@"^\s*ANSWER\s*:\s*([A-H])\s*$", RegexOptions.IgnoreCase)] private static partial Regex AikenAnswerLine();

    public static Converted ParseAiken(string raw)
    {
        var notes = new List<string>();
        var questions = new List<PackQuestion>();
        var stem = new List<string>();
        var opts = new List<string>();
        void Flush(string letter)
        {
            var answer = letter[0] - 'A';
            if (stem.Count > 0 && opts.Count >= 2 && answer >= 0 && answer < opts.Count)
                questions.Add(new PackQuestion
                {
                    Prompt = Para(string.Join("\n", stem)),
                    Options = opts.Select(Esc).ToList(),
                    Answer = JsonValue.Create(answer),
                    Solution = $"<p>{L.F("fmt.aikenAnswer", letter)}</p>",
                });
            else notes.Add(L.F("fmt.aikenSkip", Head(string.Join(" ", stem), 40)));
            stem = [];
            opts = [];
        }
        foreach (var line in raw.Replace("\r", "").Split('\n'))
        {
            var opt = AikenOption().Match(line);
            var ans = AikenAnswerLine().Match(line);
            if (ans.Success) Flush(ans.Groups[1].Value.ToUpperInvariant());
            else if (opt.Success && (opts.Count > 0 || stem.Count > 0)) opts.Add(opt.Groups[2].Value);
            else if (line.Trim().Length > 0 && opts.Count == 0) stem.Add(line);
        }
        if (questions.Count > 0) notes.Add(L.T("fmt.aikenNoSolution"));
        return new Converted([new PackLesson { Id = "aiken", Title = L.T("fmt.aikenLesson"), Questions = questions }], notes);
    }

    // ------------------------------------------------------------------ GIFT

    [GeneratedRegex(@"\\([~=#{}:\\])")] private static partial Regex GiftEscaped();
    [GeneratedRegex(@"\n\s*\n")] private static partial Regex BlankLine();
    [GeneratedRegex(@"^\s*//")] private static partial Regex Comment();
    [GeneratedRegex(@"^\$CATEGORY:\s*(.+)$", RegexOptions.Multiline)] private static partial Regex Category();
    [GeneratedRegex(@"^::((?:\\:|[^:])*)::")] private static partial Regex GiftTitle();
    [GeneratedRegex(@"^\s*\[html\]", RegexOptions.IgnoreCase)] private static partial Regex HtmlFlag();
    [GeneratedRegex(@"^\s*\[(html|moodle|plain|markdown)\]", RegexOptions.IgnoreCase)] private static partial Regex FormatFlag();
    [GeneratedRegex(@"(?<!\\)####([\s\S]*)$")] private static partial Regex GeneralFeedback();
    [GeneratedRegex(@"^(T|TRUE|F|FALSE)\b", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)] private static partial Regex TrueFalse();
    [GeneratedRegex(@"(?<!\\)#.*$")] private static partial Regex NumFeedback();
    [GeneratedRegex(@"^%-?[\d.]+%")] private static partial Regex Weight();
    [GeneratedRegex(@"^(-?[\d.]+)\.\.(-?[\d.]+)$")] private static partial Regex Range();
    [GeneratedRegex(@"^(-?[\d.eE+-]+):([\d.eE+-]+)$")] private static partial Regex Tolerance();
    [GeneratedRegex(@"^%(-?[\d.]+)%")] private static partial Regex WeightValue();
    [GeneratedRegex(@"(^|[^\\])#")] private static partial Regex FeedbackMark();

    private static string UnescapeGift(string s) => GiftEscaped().Replace(s, "$1").Replace("\\n", "\n");

    /// <summary>Tách theo ký tự đặc biệt chưa escape (\~ \= giữ nguyên trong mảnh).</summary>
    private static List<string> SplitUnescaped(string s, Func<char, bool> at)
    {
        var output = new List<string>();
        var cur = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                cur.Append(s[i]).Append(s[i + 1]);
                i++;
                continue;
            }
            if (at(s[i]))
            {
                output.Add(cur.ToString());
                cur.Clear().Append(s[i]);
                continue;
            }
            cur.Append(s[i]);
        }
        output.Add(cur.ToString());
        return output;
    }

    public static Converted ParseGift(string raw)
    {
        var notes = new List<string>();
        var byCategory = new List<(string Title, List<PackQuestion> Qs)>();
        var category = L.T("fmt.giftCategory");
        foreach (var rawBlock in BlankLine().Split(raw.Replace("\r", "")))
        {
            var block = Text.TrimJs(string.Join("\n", rawBlock.Split('\n').Where(l => !Comment().IsMatch(l))));
            if (block.Length == 0) continue;
            // $CATEGORY có thể nằm sát câu đầu (không có dòng trống): lấy ra rồi đọc phần còn lại.
            if (Category().Match(block) is { Success: true } cat)
            {
                category = cat.Groups[1].Value.Split('/').Where(x => x.Length > 0 && x != "$course$" && x != "top").LastOrDefault() ?? category;
                var at = block.IndexOf(cat.Value, StringComparison.Ordinal);
                block = Text.TrimJs(block[..at] + block[(at + cat.Value.Length)..]);
                if (block.Length == 0) continue;
            }
            var body = block;
            var title = "";
            if (GiftTitle().Match(body) is { Success: true } t)
            {
                title = UnescapeGift(t.Groups[1].Value);
                body = body[t.Length..];
            }
            var isHtml = HtmlFlag().IsMatch(body);
            body = FormatFlag().Replace(body, "", 1);
            // Khối đáp án {...} đầu tiên chưa escape.
            int open = -1, close = -1;
            for (var i = 0; i < body.Length; i++)
            {
                if (body[i] == '\\')
                {
                    i++;
                    continue;
                }
                if (body[i] == '{' && open < 0) open = i;
                else if (body[i] == '}' && open >= 0)
                {
                    close = i;
                    break;
                }
            }
            if (open < 0 || close < 0)
            {
                notes.Add(L.F("fmt.giftNoBraces", Head(title.Length > 0 ? title : body, 40)));
                continue;
            }
            var before = Text.TrimJs(body[..open]);
            var after = Text.TrimJs(body[(close + 1)..]);
            var stemRaw = UnescapeGift(after.Length > 0 ? $"{before} ____ {after}" : before);
            var prompt = isHtml ? stemRaw : Para(stemRaw);
            var inner = Text.TrimJs(body[(open + 1)..close]);
            var general = "";
            if (GeneralFeedback().Match(inner) is { Success: true } g)
            {
                general = Text.TrimJs(UnescapeGift(g.Groups[1].Value));
                inner = Text.TrimJs(inner[..g.Index]);
            }
            var solution = general.Length > 0 ? (isHtml ? general : Para(general)) : $"<p>{L.T("fmt.giftNoSolution")}</p>";
            void Push(PackQuestion q)
            {
                if (title.Length > 0) q.Tag = title;
                var list = byCategory.FirstOrDefault(c => c.Title == category).Qs;
                if (list is null) byCategory.Add((category, list = []));
                list.Add(q);
            }
            if (inner.Length == 0)
            {
                notes.Add(L.F("fmt.giftEssay", Head(title.Length > 0 ? title : before, 40)));
                continue;
            }
            if (TrueFalse().Match(inner) is { Success: true } tf)
            {
                Push(new PackQuestion { Type = "truefalse", Prompt = prompt, Answer = JsonValue.Create(tf.Groups[1].Value.StartsWith('T') || tf.Groups[1].Value.StartsWith('t')), Solution = solution });
                continue;
            }
            if (inner.StartsWith('#'))
            {
                // {#3.14:0.01}, {#1..2}, {#=3.14:0.01 =%50%3.1:0.1}: lấy đáp án đầu (đủ điểm), bỏ feedback và trọng số.
                var first = SplitUnescaped(inner[1..], c => c == '=').Select(x => Text.TrimJs(x.StartsWith('=') ? x[1..] : x)).FirstOrDefault(x => x.Length > 0) ?? "";
                first = Text.TrimJs(Weight().Replace(NumFeedback().Replace(first, "", 1), "", 1));
                var range = Range().Match(first);
                var tol = Tolerance().Match(first);
                if (range.Success)
                {
                    double a = JsNumber(range.Groups[1].Value), b = JsNumber(range.Groups[2].Value);
                    Push(new PackQuestion { Type = "numeric", Prompt = prompt, Answer = JsonValue.Create((a + b) / 2), Tolerance = Math.Abs(b - a) / 2, Solution = solution });
                }
                else if (tol.Success)
                    Push(new PackQuestion { Type = "numeric", Prompt = prompt, Answer = JsonValue.Create(JsNumber(tol.Groups[1].Value)), Tolerance = JsNumber(tol.Groups[2].Value), Solution = solution });
                else if (double.IsFinite(JsNumber(first)))
                    Push(new PackQuestion { Type = "numeric", Prompt = prompt, Answer = JsonValue.Create(JsNumber(first)), Solution = solution });
                else notes.Add(L.F("fmt.giftBadNumber", Head(inner, 30)));
                continue;
            }
            if (inner.Contains("->", StringComparison.Ordinal))
            {
                notes.Add(L.F("fmt.giftMatching", Head(title.Length > 0 ? title : before, 40)));
                continue;
            }
            var answers = SplitUnescaped(inner, c => c is '=' or '~').Select(Text.TrimJs).Where(x => x.Length > 0).Select(a =>
            {
                var right = a.StartsWith('=');
                var text = Text.TrimJs(a[1..]);
                var w = WeightValue().Match(text);
                var weight = w.Success ? JsNumber(w.Groups[1].Value) : right ? 100 : 0;
                if (w.Success) text = text[w.Length..];
                var fbm = FeedbackMark().Match(text);
                var fb = fbm.Success ? fbm.Index : -1;
                var feedback = fb >= 0 ? Text.TrimJs(UnescapeGift(text[(fb + (text[fb] == '#' ? 1 : 2))..])) : "";
                if (fb >= 0) text = text[..(fb + (text[fb] == '#' ? 0 : 1))];
                return (Text: UnescapeGift(Text.TrimJs(text)), Right: right, Weight: weight, Feedback: feedback);
            }).ToList();
            var wrong = answers.Where(a => !a.Right && !(a.Weight > 0)).ToList();
            if (wrong.Count == 0 && answers.All(a => a.Right))
            {
                Push(new PackQuestion { Type = "short", Prompt = prompt, Accept = [.. answers.Select(a => a.Text)], Solution = solution });
                continue;
            }
            var fbList = string.Concat(answers.Where(a => a.Feedback.Length > 0).Select(a => $"<li>{Esc(a.Text)}: {Esc(a.Feedback)}</li>"));
            var sol = fbList.Length > 0 ? $"{solution}<ul>{fbList}</ul>" : solution;
            var opts = answers.Select(a => isHtml ? a.Text : Esc(a.Text)).ToList();
            var correct = answers.Select((a, i) => a.Weight > 0 ? i : -1).Where(i => i >= 0).ToArray();
            if (correct.Length == 1 && answers.Count(a => a.Right) <= 1)
                Push(new PackQuestion { Prompt = prompt, Options = opts, Answer = JsonValue.Create(correct[0]), Solution = sol });
            else if (correct.Length > 1) Push(new PackQuestion { Type = "multi", Prompt = prompt, Options = opts, Answers = correct, Solution = sol });
            else notes.Add(L.F("fmt.noRight", Head(title.Length > 0 ? title : before, 40)));
        }
        return new Converted(byCategory.Select((c, i) => new PackLesson { Id = $"gift-{i + 1}", Title = c.Title, Questions = c.Qs }).ToList(), notes);
    }

    private static string GiftEsc(string s) => Regex.Replace(s, @"([~=#{}:\\])", @"\$1").Replace("\n", " ");

    // +x.toFixed(5) của JS.
    private static string Fixed5(double v) => SoHocTap.Presentation.Practice.JsNumber.Format(Math.Round(v, 5, MidpointRounding.AwayFromZero));

    public static string ToGift(StudyPack p)
    {
        var output = new List<string> { $"// {p.Title} · Study Pack {p.Id} v{p.Version}" };
        foreach (var u in p.Units)
            foreach (var l in u.Lessons)
            {
                output.Add("");
                output.Add($"$CATEGORY: $course$/top/{p.Course.Name}/{u.Title}/{l.Title}");
                output.Add("");
                var qs = l.Questions ?? [];
                for (var i = 0; i < qs.Count; i++)
                {
                    var q = qs[i];
                    var title = $"::{GiftEsc(q.Tag ?? L.F("fmt.giftQuestionName", l.Title, i + 1))}::[html]{GiftEsc(q.Prompt)}";
                    var fb = $"####{GiftEsc(q.Solution)}";
                    var t = q.Type ?? "single";
                    var opts = q.Options ?? [];
                    var body = "";
                    if (t == "single") body = string.Join(" ", opts.Select((o, j) => $"{(j == (int?)q.AnswerNumber ? "=" : "~")}{GiftEsc(o)}"));
                    else if (t == "multi")
                    {
                        var right = q.Answers ?? [];
                        var wrongN = opts.Count - right.Length;
                        body = string.Join(" ", opts.Select((o, j) =>
                            $"~%{(right.Contains(j) ? Fixed5(100.0 / right.Length) : wrongN != 0 ? Fixed5(-(100.0 / wrongN)) : "0")}%{GiftEsc(o)}"));
                    }
                    else if (t == "truefalse") body = q.AnswerBool == true ? "T" : "F";
                    else if (t == "numeric")
                        body = $"#{(q.AnswerNumber is { } v ? SoHocTap.Presentation.Practice.JsNumber.Format(v) : "undefined")}{(q.Tolerance is { } tol && tol != 0 ? ":" + SoHocTap.Presentation.Practice.JsNumber.Format(tol) : "")}";
                    else if (t == "short") body = string.Join(" ", (q.Accept ?? []).Select(a => "=" + GiftEsc(a)));
                    output.Add($"{title} {{{body} {fb}}}");
                    output.Add("");
                }
            }
        return string.Join("\n", output);
    }

    // ------------------------------------------------------------------ Moodle XML

    [GeneratedRegex(@"<[a-z]", RegexOptions.IgnoreCase)] private static partial Regex AnyTagStart();
    [GeneratedRegex(@"\s+")] private static partial Regex Ws();
    [GeneratedRegex(@"^<p>(.*)</p>$", RegexOptions.Singleline)] private static partial Regex OnePara();
    [GeneratedRegex(@"</?p>")] private static partial Regex PTag();
    [GeneratedRegex(@"true|đúng", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex TrueWord();

    /// <summary>encodeURIComponent của JS (Uri.EscapeDataString escape thêm ! ' ( ) *).</summary>
    private static string EncodeUriComponent(string s)
    {
        var sb = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(s))
        {
            var c = (char)b;
            if (b < 0x80 && (char.IsAsciiLetterOrDigit(c) || "-_.!~*'()".Contains(c))) sb.Append(c);
            else sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    /// <summary>Ảnh trong Moodle XML: &lt;file name="a.png" encoding="base64"&gt; + src="@@PLUGINFILE@@/a.png" thành data URI.</summary>
    private static string WithFiles(XElement? el)
    {
        if (el is null) return "";
        var html = el.Element("text")?.Value ?? "";
        foreach (var f in el.Elements("file"))
        {
            var name = (string?)f.Attribute("name") ?? "";
            var ext = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..].ToLowerInvariant() : name.ToLowerInvariant();
            var mime = ext switch { "jpg" or "jpeg" => "image/jpeg", "gif" => "image/gif", "webp" => "image/webp", "png" => "image/png", _ => "" };
            if (mime.Length == 0) continue;
            var data = $"data:{mime};base64,{Ws().Replace(f.Value, "")}";
            html = html.Replace($"@@PLUGINFILE@@/{EncodeUriComponent(name)}", data).Replace($"@@PLUGINFILE@@/{name}", data);
        }
        return (string?)el.Attribute("format") == "html" || AnyTagStart().IsMatch(html) ? html : Para(html);
    }

    public static Converted ParseMoodleXml(string raw)
    {
        var notes = new List<string>();
        XDocument doc;
        try { doc = XDocument.Parse(raw); }
        catch (XmlException) { return new Converted([], [L.T("fmt.xmlBroken")]); }
        var byCategory = new List<(string Title, List<PackQuestion> Qs)>();
        var category = L.T("fmt.moodleCategory");
        var skipped = new List<(string Type, int N)>();
        foreach (var q in doc.Descendants("quiz").Elements("question"))
        {
            var type = (string?)q.Attribute("type") ?? "";
            if (type == "category")
            {
                var path = q.Descendants("category").Elements("text").FirstOrDefault()?.Value ?? "";
                category = path.Split('/').Where(x => x.Length > 0 && x != "$course$" && x != "$system$" && x != "top" && !x.StartsWith("Default for", StringComparison.Ordinal))
                    .LastOrDefault() ?? category;
                continue;
            }
            var name = Text.TrimJs(q.Element("name")?.Element("text")?.Value ?? "");
            var prompt = WithFiles(q.Element("questiontext"));
            var general = WithFiles(q.Element("generalfeedback"));
            var answers = q.Elements("answer").Select(a => (
                Html: WithFiles(a),
                Fraction: ParseFloat((string?)a.Attribute("fraction") ?? "0"),
                Feedback: WithFiles(a.Element("feedback")),
                Tol: ParseFloat(a.Element("tolerance")?.Value ?? "0"))).ToList();
            var fb = string.Concat(answers.Where(a => Text.TrimJs(Text.PlainText(a.Feedback)).Length > 0)
                .Select(a => $"<li>{PTag().Replace(a.Html, "")}: {PTag().Replace(a.Feedback, "")}</li>"));
            var solution = (Text.TrimJs(Text.PlainText(general)).Length > 0 ? general : $"<p>{L.T("fmt.moodleNoSolution")}</p>") + (fb.Length > 0 ? $"<ul>{fb}</ul>" : "");
            PackQuestion Base() => new() { Tag = name.Length > 0 ? name : null, Prompt = prompt, Solution = solution };
            PackQuestion? item = null;
            if (type == "multichoice")
            {
                var single = Text.TrimJs(q.Element("single")?.Value ?? "true") != "false";
                var options = answers.Select(a => OnePara().Replace(a.Html, "$1")).ToList();
                var right = answers.Select((a, i) => a.Fraction > 0 ? i : -1).Where(i => i >= 0).ToArray();
                if (single && right.Length > 0)
                {
                    var best = 0;
                    for (var i = 1; i < answers.Count; i++) if (answers[i].Fraction > answers[best].Fraction) best = i;
                    item = Base();
                    item.Options = options;
                    item.Answer = JsonValue.Create(best);
                }
                else if (right.Length > 0)
                {
                    item = Base();
                    item.Type = "multi";
                    item.Options = options;
                    item.Answers = right;
                }
            }
            else if (type == "truefalse")
            {
                if (answers.FirstOrDefault(a => a.Fraction > 0) is { Html: not null } t)
                {
                    item = Base();
                    item.Type = "truefalse";
                    item.Answer = JsonValue.Create(TrueWord().IsMatch(t.Html));
                }
            }
            else if (type == "numerical")
            {
                var a = answers.Where(x => x.Fraction >= 100).Select(x => (Found: true, x)).FirstOrDefault();
                var pick = a.Found ? a.x : answers.FirstOrDefault();
                var v = answers.Count > 0 ? ParseFloat(Text.PlainText(pick.Html)) : double.NaN;
                if (double.IsFinite(v))
                {
                    item = Base();
                    item.Type = "numeric";
                    item.Answer = JsonValue.Create(v);
                    if (pick.Tol is var tol && tol != 0 && !double.IsNaN(tol)) item.Tolerance = tol;
                }
            }
            else if (type == "shortanswer")
            {
                var acc = answers.Where(a => a.Fraction >= 100).Select(a => Text.TrimJs(Text.PlainText(a.Html))).Where(x => x.Length > 0).ToArray();
                if (acc.Length > 0)
                {
                    item = Base();
                    item.Type = "short";
                    item.Accept = acc;
                }
            }
            else
            {
                var at = skipped.FindIndex(s => s.Type == type);
                if (at < 0) skipped.Add((type, 1));
                else skipped[at] = (type, skipped[at].N + 1);
                continue;
            }
            if (item is null)
            {
                notes.Add(L.F("fmt.moodleNoRight", name.Length > 0 ? name : type));
                continue;
            }
            var list = byCategory.FirstOrDefault(c => c.Title == category).Qs;
            if (list is null) byCategory.Add((category, list = []));
            list.Add(item);
        }
        foreach (var (t, n) in skipped)
        {
            var key = "fmt.type." + t;
            var label = L.T(key);
            notes.Add(L.F("fmt.moodleSkipped", n, label == key ? t : label));
        }
        return new Converted(byCategory.Select((c, i) => new PackLesson { Id = $"moodle-{i + 1}", Title = c.Title, Questions = c.Qs }).ToList(), notes);
    }

    private static string Cdata(string s) => $"<![CDATA[{s.Replace("]]>", "]]]]><![CDATA[>")}]]>";

    [GeneratedRegex(@"src=""data:image/(png|jpeg|gif|webp);base64,([^""]+)""")] private static partial Regex DataSrc();

    /// <summary>Ảnh data URI thành &lt;file&gt; + @@PLUGINFILE@@ (ngân hàng câu hỏi Moodle không nhận data URI).</summary>
    private static string MoodleText(string tag, string html, ref int n)
    {
        var files = new List<string>();
        var counter = n;
        var output = DataSrc().Replace(html, m =>
        {
            var ext = m.Groups[1].Value == "jpeg" ? "jpg" : m.Groups[1].Value;
            var name = $"img{++counter}.{ext}";
            files.Add($"<file name=\"{name}\" path=\"/\" encoding=\"base64\">{Ws().Replace(m.Groups[2].Value, "")}</file>");
            return $"src=\"@@PLUGINFILE@@/{name}\"";
        });
        n = counter;
        return $"<{tag} format=\"html\"><text>{Cdata(output)}</text>{string.Concat(files)}</{tag}>";
    }

    public static string ToMoodleXml(StudyPack p)
    {
        var n = 0;
        var output = new List<string> { "<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "<quiz>" };
        foreach (var u in p.Units)
            foreach (var l in u.Lessons)
            {
                output.Add($"<question type=\"category\"><category><text>$course$/top/{Esc(p.Course.Name)}/{Esc(u.Title)}/{Esc(l.Title)}</text></category></question>");
                var qs = l.Questions ?? [];
                for (var i = 0; i < qs.Count; i++)
                {
                    var q = qs[i];
                    var head = $"<name><text>{Esc(q.Tag ?? L.F("fmt.questionName", l.Title, i + 1))}</text></name>{MoodleText("questiontext", q.Prompt, ref n)}{MoodleText("generalfeedback", q.Solution, ref n)}<defaultgrade>1</defaultgrade><penalty>0.3333333</penalty><hidden>0</hidden>";
                    var t = q.Type ?? "single";
                    if (t is "single" or "multi")
                    {
                        var right = t == "multi" ? q.Answers ?? [] : [q.AnswerNumber is { } a ? (int)a : -1];
                        var opts = q.Options ?? [];
                        var wrongN = opts.Count - right.Length;
                        // Nhiều đáp án: đúng chia đều 100%, sai trừ đều 100% (chọn bừa hết thì 0 điểm), như cách Moodle chấm.
                        var ans = new StringBuilder();
                        for (var j = 0; j < opts.Count; j++)
                        {
                            var f = right.Contains(j) ? (t == "multi" ? Fixed5(100.0 / right.Length) : "100")
                                : t == "multi" && wrongN != 0 ? Fixed5(-(100.0 / wrongN)) : "0";
                            var x = MoodleText("x", opts[j], ref n);
                            var body = x["<x format=\"html\">".Length..^"</x>".Length];
                            ans.Append(CultureInfo.InvariantCulture, $"<answer fraction=\"{f}\" format=\"html\">{body}<feedback format=\"html\"><text></text></feedback></answer>");
                        }
                        output.Add($"<question type=\"multichoice\">{head}<single>{(t == "single" ? "true" : "false")}</single><shuffleanswers>true</shuffleanswers><answernumbering>ABCD</answernumbering>{ans}</question>");
                    }
                    else if (t == "truefalse")
                    {
                        var yes = q.AnswerBool == true;
                        output.Add($"<question type=\"truefalse\">{head}<answer fraction=\"{(yes ? 100 : 0)}\" format=\"moodle_auto_format\"><text>true</text></answer><answer fraction=\"{(yes ? 0 : 100)}\" format=\"moodle_auto_format\"><text>false</text></answer></question>");
                    }
                    else if (t == "numeric")
                    {
                        var v = q.AnswerNumber is { } a ? SoHocTap.Presentation.Practice.JsNumber.Format(a) : "undefined";
                        output.Add($"<question type=\"numerical\">{head}<answer fraction=\"100\" format=\"moodle_auto_format\"><text>{v}</text><tolerance>{SoHocTap.Presentation.Practice.JsNumber.Format(q.Tolerance ?? 0)}</tolerance></answer></question>");
                    }
                    else if (t == "short")
                    {
                        var acc = string.Concat((q.Accept ?? []).Select(a => $"<answer fraction=\"100\" format=\"moodle_auto_format\"><text>{Esc(a)}</text></answer>"));
                        output.Add($"<question type=\"shortanswer\">{head}<usecase>0</usecase>{acc}</question>");
                    }
                }
            }
        output.Add("</quiz>");
        return string.Join("\n", output);
    }
}
