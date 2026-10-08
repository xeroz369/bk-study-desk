using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Ui;

namespace SoHocTap.Presentation.Practice;

public sealed record MdIssue(int Line, string Message);

public sealed record MdResult(StudyPack Pack, List<MdIssue> Errors, List<MdIssue> Warnings);

public static partial class StudyMarkdown
{
    [GeneratedRegex(@"^(đề thi thử|de thi thu|exams?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex ExamUnit();
    [GeneratedRegex(@"^(giữ thứ tự|giu thu tu|keep order)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex KeepOrder();
    /// <summary>"nhóm: f1" trong dòng ###: các câu cùng nhóm trong bài dùng chung đề, luôn đi cùng nhau.</summary>
    [GeneratedRegex(@"^(?:nhóm|nhom|group)\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Group();
    [GeneratedRegex(@"^(co|có|yes|true|1|bật|bat)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Yes();
    [GeneratedRegex(@"^\s*([\w-]+)\s*:\s*(.*?)\s*(#.*)?$", RegexOptions.ECMAScript)] private static partial Regex MetaLine();
    [GeneratedRegex(@"^(#{1,4})\s+(.*)$")] private static partial Regex Heading();
    [GeneratedRegex(@"^####\s+(.*)$")] private static partial Regex H4();
    [GeneratedRegex(@"^\s*([A-Za-z]{2,}\d{3,}[A-Za-z]?)\s*(.*)$")] private static partial Regex CourseLine();
    [GeneratedRegex(@"(\d+)\s*phút|(\d+)\s*min", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Minutes();
    [GeneratedRegex(@"đúng\s*:?\s*([+-]?[\d.,/]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex RightScore();
    [GeneratedRegex(@"sai\s*:?\s*([+\-−]?[\d.,/]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex WrongScore();
    [GeneratedRegex(@"^\s*[-*]\s+\[([ xX])\]\s?(.*)$")] private static partial Regex OptionLine();
    [GeneratedRegex(@"^\s*=\s?")] private static partial Regex AnswerLine();
    [GeneratedRegex(@"^\s*>\s?")] private static partial Regex SolutionLine();
    [GeneratedRegex(@"^câu\s*\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex NumberedTitle();
    [GeneratedRegex(@"^(đúng|dung|true|t|sai|false|f)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex TrueFalse();
    [GeneratedRegex(@"^(đúng|dung|true|t)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex TrueWord();
    [GeneratedRegex(@"^([+\-−]?\d[\d.,]*(?:[eE][+-]?\d+)?)\s*(?:(?:±|\+-|\+/-)\s*([\d.,]+(?:[eE][+-]?\d+)?))?\s*(?:\(([^)]+)\))?$")] private static partial Regex NumericAnswer();
    [GeneratedRegex(@"^([+-]?\d+(?:\.\d+)?)/(\d+(?:\.\d+)?)$")] private static partial Regex Fraction();
    [GeneratedRegex(@"<img src="""" alt=""([^""]*)"">")] private static partial Regex EmptyImg();
    [GeneratedRegex(@"^<p>([\s\S]*)</p>$")] private static partial Regex OnePara();
    [GeneratedRegex(@"^(units\[(\d+)\]\.lessons\[(\d+)\]|exams\[(\d+)\])\.questions\[(\d+)\]")] private static partial Regex QuestionPath();
    // Mở rộng v1 (SPEC 2.6): "id: ..." ngay dưới ##, ###; "Câu: a, b" trong phần thông tin của đề.
    [GeneratedRegex(@"^\s*id\s*:\s*(\S+)\s*$", RegexOptions.IgnoreCase)] private static partial Regex IdLine();
    [GeneratedRegex(@"^\s*(?:câu|cau|questions?)\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex RefsLine();

    private sealed record Line(string Text, int No);

    private sealed class Block(int level, string title, int line)
    {
        public int Level { get; } = level;
        public string Title { get; } = title;
        public int LineNo { get; } = line;
        public List<Line> Body { get; } = [];
    }

    /// <summary>
    /// Đọc một file Study Markdown thành gói. <paramref name="images"/>: đường dẫn trong zip (img/a.webp) thành data URI; thiếu ảnh
    /// chỉ là cảnh báo. <paramref name="validate"/>: chạy thêm <see cref="PackValidator"/> như bản TS (nội dung có sẵn của app
    /// tắt đi, vì đề tham chiếu câu ở file bài khác và id có dấu chấm).
    /// </summary>
    public static MdResult Parse(string text, IReadOnlyDictionary<string, string>? images = null, bool validate = true)
    {
        images ??= new Dictionary<string, string>();
        var errors = new List<MdIssue>();
        var warnings = new List<MdIssue>();
        var lines = text.TrimStart((char)0xFEFF).Replace("\r", "").Split('\n');
        var n = 0;

        var meta = new Dictionary<string, string>();
        if (lines[0].Trim() == "---")
        {
            for (n = 1; n < lines.Length && lines[n].Trim() != "---"; n++)
                if (MetaLine().Match(lines[n]) is { Success: true } m) meta[m.Groups[1].Value.ToLowerInvariant()] = m.Groups[2].Value;
            n++;
        }
        else warnings.Add(new MdIssue(1, L.T("md.noFrontMatter")));
        string? Pick(params string[] keys) => keys.Select(k => meta.GetValueOrDefault(k)).FirstOrDefault(v => !string.IsNullOrEmpty(v));

        var missing = new List<string>();
        string Img(string src)
        {
            if (src.StartsWith("data:image/", StringComparison.Ordinal)) return src;
            var key = src.StartsWith("./", StringComparison.Ordinal) ? src[2..] : src;
            if (images.TryGetValue(key, out var uri) && !string.IsNullOrEmpty(uri)) return uri;
            if (!missing.Contains(key)) missing.Add(key);
            return "";
        }
        var missingLabel = L.T("md.missingImageLabel");
        string ToHtml(string md) => EmptyImg().Replace(MdToHtml(md, Img),
            m => $"<i>[{missingLabel}{(m.Groups[1].Value.Length > 0 ? ": " + m.Groups[1].Value : "")}]</i>");

        var courseRaw = Pick("mon", "course") ?? "";
        var course = CourseLine().Match(courseRaw);
        var pack = new StudyPack
        {
            Title = Pick("tieu-de", "title") ?? "",
            Version = Pick("phien-ban", "version") ?? "1.0.0",
            Language = Pick("ngon-ngu", "language") ?? "vi",
            Course = new PackCourse(
                course.Success ? course.Groups[1].Value.ToUpperInvariant() : "",
                course.Success && course.Groups[2].Value.Trim().Length > 0 ? course.Groups[2].Value.Trim() : courseRaw,
                Pick("truong", "school")),
            Authors = (Pick("tac-gia", "authors", "author") ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Select(s => new PackAuthor(s)).ToList(),
            License = Pick("giay-phep", "license"),
            CreatedWith = Pick("tao-boi", "created-with"),
            Verified = Pick("da-kiem", "verified") is { } v ? new PackVerified(v) : null,
            Description = Pick("mo-ta", "description"),
            Sources = Pick("nguon", "sources") is { } src ? src.Split(';').Select(s => new PackSource(s.Trim())).Where(s => s.Title.Length > 0).ToList() : null,
            Settings = new PackSettings(IsYes(Pick("xao-cau", "shuffle-questions")), IsYes(Pick("xao-dap-an", "shuffle-options"))),
            Exams = [],
        };

        var blocks = new List<Block>();
        Block? cur = null;
        for (; n < lines.Length; n++)
        {
            var h = Heading().Match(lines[n]);
            if (h.Success && h.Groups[1].Value.Length <= 3)
            {
                cur = new Block(h.Groups[1].Value.Length, h.Groups[2].Value.Trim(), n + 1);
                blocks.Add(cur);
            }
            else if (cur != null) cur.Body.Add(new Line(lines[n], n + 1));
            else if (lines[n].Trim().Length > 0) warnings.Add(new MdIssue(n + 1, L.T("md.beforeUnit")));
        }

        var lineOf = new Dictionary<PackQuestion, int>(ReferenceEqualityComparer.Instance);
        PackUnit? unit = null;
        PackLesson? lesson = null;
        PackExam? exam = null;
        var inExams = false;
        var lessonIds = new HashSet<string>();
        foreach (var b in blocks)
        {
            if (b.Level == 1)
            {
                inExams = ExamUnit().IsMatch(b.Title);
                unit = inExams ? null : new PackUnit { Title = b.Title };
                if (unit != null) pack.Units.Add(unit);
                lesson = null;
                exam = null;
            }
            else if (b.Level == 2)
            {
                var explicitId = TakeId(b.Body);
                if (inExams)
                {
                    exam = new PackExam { Id = explicitId ?? Slug(b.Title, $"de-{pack.Exams!.Count + 1}"), Title = b.Title, Minutes = 30, Questions = [] };
                    var refs = new List<string>();
                    var infoLines = new List<string>();
                    foreach (var x in b.Body)
                        if (RefsLine().Match(x.Text) is { Success: true } rm) refs.AddRange(rm.Groups[1].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));
                        else infoLines.Add(x.Text);
                    var info = string.Join(" ", infoLines);
                    var min = Minutes().Match(info);
                    var right = RightScore().Match(info);
                    var wrong = WrongScore().Match(info);
                    if (min.Success) exam.Minutes = double.Parse(min.Groups[1].Success ? min.Groups[1].Value : min.Groups[2].Value, CultureInfo.InvariantCulture);
                    if (right.Success || wrong.Success)
                        exam.Scoring = new Scoring(0, Num(right.Success ? right.Groups[1].Value : null) ?? 1, Num(wrong.Success ? wrong.Groups[1].Value : null) ?? 0);
                    if (refs.Count > 0) exam.QuestionRefs = refs;
                    pack.Exams!.Add(exam);
                    lesson = null;
                    continue;
                }
                if (unit is null)
                {
                    errors.Add(new MdIssue(b.LineNo, L.T("md.lessonOutsideUnit")));
                    continue;
                }
                var id = explicitId ?? Slug(b.Title, "bai");
                while (!lessonIds.Add(id)) id += "-2";
                lesson = new PackLesson { Id = id, Title = b.Title, Questions = [] };
                unit.Lessons.Add(lesson);
                // Kiến thức của bài: chữ dưới ## tới ### đầu tiên, chia mục bằng ####.
                var sections = new List<PackSection>();
                string? secTitle = null;
                var secLines = new List<string>();
                void Push()
                {
                    if (string.Concat(secLines).Trim().Length > 0) sections.Add(new PackSection(secTitle, ToHtml(string.Join("\n", secLines))));
                }
                foreach (var x in b.Body)
                {
                    if (H4().Match(x.Text) is { Success: true } h4)
                    {
                        Push();
                        secTitle = h4.Groups[1].Value.Trim();
                        secLines = [];
                    }
                    else secLines.Add(x.Text);
                }
                Push();
                if (sections.Count > 0) lesson.Sections = sections;
            }
            else
            {
                List<PackQuestion>? list = exam?.Questions ?? lesson?.Questions;
                if (list is null)
                {
                    errors.Add(new MdIssue(b.LineNo, L.T("md.questionOutside")));
                    continue;
                }
                var explicitId = TakeId(b.Body);
                var q = ParseQuestion(b, ToHtml, errors);
                if (q is null) continue;
                q.Id = explicitId ?? $"q{list.Count + 1}";
                list.Add(q);
                lineOf[q] = b.LineNo;
            }
        }
        foreach (var x in pack.Exams!)
            if (x.Scoring is { } s) x.Scoring = s with { Count = (x.Questions?.Count ?? 0) + (x.QuestionRefs?.Count ?? 0) };
        if (pack.Exams.Count == 0) pack.Exams = null;

        pack.Id = Pick("id") ?? Slug($"{Pick("truong", "school") ?? "hcmut"}-{pack.Course.Code}-{(pack.Title.Length > 0 ? pack.Title : pack.Units.FirstOrDefault()?.Title ?? "goi")}", "goi-luyen-tap");
        if (pack.Title.Length == 0) pack.Title = pack.Units.Count > 0 ? string.Join(", ", pack.Units.Select(u => u.Title)) : "Gói luyện tập";
        if (pack.Settings is { ShuffleQuestions: false, ShuffleOptions: false }) pack.Settings = null;
        foreach (var k in missing) warnings.Add(new MdIssue(Array.FindIndex(lines, l => l.Contains(k, StringComparison.Ordinal)) + 1, L.F("md.missingImage", k)));

        // Kiểm tra sâu bằng validator chung, đổi đường dẫn lỗi về số dòng khi được.
        if (validate && pack.Course.Code.Length > 0)
        {
            var r = PackValidator.Validate(JsonNode.Parse(pack.ToJson()));
            int LineFor(string path)
            {
                var m = QuestionPath().Match(path);
                if (!m.Success) return 0;
                var owner = m.Groups[4].Success ? pack.Exams?.ElementAtOrDefault(int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture))?.Questions
                    : pack.Units.ElementAtOrDefault(int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))?.Lessons.ElementAtOrDefault(int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture))?.Questions;
                var q = owner?.ElementAtOrDefault(int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture));
                return q != null && lineOf.TryGetValue(q, out var no) ? no : 0;
            }
            // Bỏ những gì bộ đọc Markdown đã báo bằng lời dễ hiểu hơn (tác giả, lời giải trống).
            errors.AddRange(r.Errors.Where(e => !e.Path.StartsWith("authors", StringComparison.Ordinal) && !e.Path.EndsWith(".solution", StringComparison.Ordinal))
                .Select(e => new MdIssue(LineFor(e.Path), e.Message)));
            warnings.AddRange(r.Warnings.Where(w => w.Path is not ("license" or "verified")).Select(w => new MdIssue(LineFor(w.Path), w.Message)));
        }
        return new MdResult(pack, errors, warnings);
    }

    private static bool IsYes(string? v) => v != null && Yes().IsMatch(v.Trim());

    /// <summary>Dòng "id: ..." là dòng có chữ đầu tiên của khối: lấy giá trị và bỏ dòng đó khỏi thân.</summary>
    private static string? TakeId(List<Line> body)
    {
        var i = body.FindIndex(x => x.Text.Trim().Length > 0);
        if (i < 0 || IdLine().Match(body[i].Text) is not { Success: true } m) return null;
        body.RemoveAt(i);
        return m.Groups[1].Value;
    }

    private static double? Num(string? s)
    {
        if (s is null) return null;
        var t = ReplaceFirst(ReplaceFirst(s, "−", "-"), ",", ".");
        var f = Fraction().Match(t);
        double v;
        if (f.Success) v = double.Parse(f.Groups[1].Value, CultureInfo.InvariantCulture) / double.Parse(f.Groups[2].Value, CultureInfo.InvariantCulture);
        else if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return null;
        return double.IsFinite(v) ? v : null;
    }

    private static string ReplaceFirst(string s, string from, string to)
    {
        var i = s.IndexOf(from, StringComparison.Ordinal);
        return i < 0 ? s : s[..i] + to + s[(i + from.Length)..];
    }

    /// <summary>Một khối ###: dòng đề, rồi các dòng - [x] hay một dòng = đáp án, rồi &gt; lời giải.</summary>
    private static PackQuestion? ParseQuestion(Block b, Func<string, string> html, List<MdIssue> errors)
    {
        var parts = Regex.Split(b.Title, @"\s+·\s+").Select(s => s.Trim()).ToList();
        var keepOrder = parts.Any(s => KeepOrder().IsMatch(s));
        var group = parts.Select(s => Group().Match(s)).Where(m => m.Success).Select(m => m.Groups[1].Value.Trim()).FirstOrDefault(g => g.Length > 0);
        var tag = string.Join(" · ", parts.Skip(1).Where(s => !KeepOrder().IsMatch(s) && !Group().IsMatch(s)));
        if (tag.Length == 0) tag = NumberedTitle().IsMatch(parts[0]) || Group().IsMatch(parts[0]) ? "" : parts[0];
        var prompt = new List<string>();
        var options = new List<(string Text, bool Right)>();
        var solution = new List<string>();
        Line? answer = null;
        foreach (var x in b.Body)
        {
            if (OptionLine().Match(x.Text) is { Success: true } opt) options.Add((opt.Groups[2].Value, opt.Groups[1].Value != " "));
            else if (AnswerLine().IsMatch(x.Text) && options.Count == 0 && solution.Count == 0) answer = new Line(AnswerLine().Replace(x.Text, "", 1).Trim(), x.No);
            else if (SolutionLine().IsMatch(x.Text)) solution.Add(SolutionLine().Replace(x.Text, "", 1));
            else if (options.Count == 0 && answer is null && solution.Count == 0) prompt.Add(x.Text);
            else if (x.Text.Trim().Length > 0 && solution.Count == 0) prompt.Add(x.Text);   // chữ sau phương án: giữ vào đề còn hơn mất
        }
        var q = new PackQuestion
        {
            Tag = tag.Length > 0 ? tag : null,
            Prompt = html(string.Join("\n", prompt)),
            Solution = solution.Count > 0 ? html(string.Join("\n", solution)) : "",
            KeepOrder = keepOrder ? true : null,
            Group = group,
        };
        if (string.Concat(prompt).Trim().Length == 0)
        {
            errors.Add(new MdIssue(b.LineNo, L.T("md.noPrompt")));
            return null;
        }
        if (solution.Count == 0) errors.Add(new MdIssue(b.LineNo, L.T("md.noSolution")));
        if (options.Count > 0)
        {
            var right = options.Select((o, i) => o.Right ? i : -1).Where(i => i >= 0).ToArray();
            var opts = options.Select(o => OnePara().Replace(html(o.Text), "$1")).ToList();
            if (right.Length == 0)
            {
                errors.Add(new MdIssue(b.LineNo, L.T("md.noRight")));
                return null;
            }
            q.Options = opts;
            if (right.Length == 1) q.Answer = JsonValue.Create(right[0]);
            else
            {
                q.Type = "multi";
                q.Answers = right;
            }
            return q;
        }
        if (answer is null)
        {
            errors.Add(new MdIssue(b.LineNo, L.T("md.noAnswer")));
            return null;
        }
        var a = answer.Text;
        if (TrueFalse().IsMatch(a))
        {
            q.Type = "truefalse";
            q.Answer = JsonValue.Create(TrueWord().IsMatch(a));
            return q;
        }
        if (NumericAnswer().Match(a) is { Success: true } nm)
        {
            var value = Num(nm.Groups[1].Value);
            var tol = nm.Groups[2].Success ? Num(nm.Groups[2].Value) : null;
            if (value is null)
            {
                errors.Add(new MdIssue(answer.No, L.F("md.badNumber", nm.Groups[1].Value)));
                return null;
            }
            q.Type = "numeric";
            q.Answer = JsonValue.Create(value.Value);
            if (tol is { } t && t != 0) q.Tolerance = t;
            if (nm.Groups[3].Success) q.Unit = nm.Groups[3].Value.Trim();
            return q;
        }
        q.Type = "short";
        q.Accept = a.Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        return q;
    }
}
