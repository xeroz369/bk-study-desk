using System.Text.RegularExpressions;

namespace SoHocTap.Presentation.Practice;

public static partial class StudyMarkdown
{
    [GeneratedRegex(@"<img\b[^>]*?src=""(data:image/[^""]+)""[^>]*?(?:alt=""([^""]*)"")?[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex DataImg();
    [GeneratedRegex(@"<div class=""math"">\s*([\s\S]*?)\s*</div>", RegexOptions.IgnoreCase)] private static partial Regex MathDiv();
    [GeneratedRegex(@"(<div class=""(?:warn|keys|tip|note)"">[\s\S]*?</div>)", RegexOptions.IgnoreCase)] private static partial Regex Callout();
    [GeneratedRegex(@"<p>([\s\S]*?)</p>", RegexOptions.IgnoreCase)] private static partial Regex Para();
    [GeneratedRegex(@"</?(b|strong)>", RegexOptions.IgnoreCase)] private static partial Regex BoldTag();
    [GeneratedRegex(@"</?(i|em)>", RegexOptions.IgnoreCase)] private static partial Regex ItalicTag();
    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)] private static partial Regex BrTag();
    [GeneratedRegex(@"[ \t]+\n")] private static partial Regex TrailingSpace();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex ManyBreaks();
    [GeneratedRegex(@"\s*\n\s*")] private static partial Regex AnyBreak();
    [GeneratedRegex(@"^data:image/(png|jpeg|gif|webp)")] private static partial Regex DataMime();

    /// <summary>HTML của app thành Markdown dễ đọc: thẻ đơn giản thành Markdown, phần còn lại giữ HTML trong dòng.</summary>
    private static string HtmlToMd(string h, Func<string, string> take)
    {
        h = DataImg().Replace(h, m => $"![{m.Groups[2].Value}]({take(m.Groups[1].Value)})");
        h = MathDiv().Replace(h, "\n\n$1\n\n");     // công thức riêng dòng
        h = Callout().Replace(h, "\n\n$1\n\n");     // khối ghi chú giữ dạng HTML
        h = Para().Replace(h, "\n\n$1\n\n");
        h = BoldTag().Replace(h, "**");
        h = ItalicTag().Replace(h, "*");
        h = BrTag().Replace(h, "  \n");
        h = TrailingSpace().Replace(h, m => m.Length >= 3 ? "  \n" : "\n");
        h = ManyBreaks().Replace(h, "\n\n");
        return Text.TrimJs(h);
    }

    /// <summary>
    /// Gói thành file .md và ảnh (đường dẫn img/1.webp... thành data URI), port <c>writeMarkdown</c>. Mở rộng: ghi dòng id khi id
    /// khác id tự đặt, và dòng "Câu:" cho câu đề lấy từ bài (bản TS chép câu vào đề).
    /// </summary>
    public static (string Md, Dictionary<string, string> Images) Write(StudyPack p)
    {
        var images = new Dictionary<string, string>();
        var seen = new Dictionary<string, string>();
        string Take(string src)
        {
            if (seen.TryGetValue(src, out var hit)) return hit;
            var m = DataMime().Match(src);
            var ext = m.Success ? m.Groups[1].Value.Replace("jpeg", "jpg") : "png";
            var path = $"img/{seen.Count + 1}.{ext}";
            seen[src] = path;
            images[path] = src;
            return path;
        }
        string Md(string h) => HtmlToMd(h, Take);
        string One(string h) => AnyBreak().Replace(Md(h), " ");   // phương án phải trên một dòng
        var output = new List<string>
        {
            "---",
            $"id: {p.Id}",
            $"tieu-de: {p.Title}",
            $"mon: {p.Course.Code} {p.Course.Name}",
        };
        if (!string.IsNullOrEmpty(p.Course.School)) output.Add($"truong: {p.Course.School}");
        output.Add($"tac-gia: {string.Join(", ", p.Authors.Select(a => a.Name))}");
        output.Add($"phien-ban: {p.Version}");
        if (!string.IsNullOrEmpty(p.License)) output.Add($"giay-phep: {p.License}");
        if (!string.IsNullOrEmpty(p.CreatedWith)) output.Add($"tao-boi: {p.CreatedWith}");
        if (p.Verified != null) output.Add($"da-kiem: {p.Verified.Method}");
        if (p.Settings?.ShuffleQuestions == true) output.Add("xao-cau: co");
        if (p.Settings?.ShuffleOptions == true) output.Add("xao-dap-an: co");
        output.Add("---");
        output.Add("");

        void Question(PackQuestion q, int i)
        {
            List<string> head = [$"Câu {i + 1}"];
            if (!string.IsNullOrEmpty(q.Tag)) head.Add(q.Tag);
            if (q.KeepOrder == true) head.Add("giữ thứ tự");
            if (!string.IsNullOrEmpty(q.Group)) head.Add($"nhóm: {q.Group}");
            output.Add("### " + string.Join(" · ", head));
            if (q.Id is { } id && id != $"q{i + 1}") output.Add($"id: {id}");
            output.Add(Md(q.Prompt));
            var t = q.Type ?? "single";
            if (t is "single" or "multi")
            {
                var right = t == "multi" ? q.Answers ?? [] : [q.AnswerNumber is { } a ? (int)a : -1];
                var opts = q.Options ?? [];
                for (var j = 0; j < opts.Count; j++) output.Add($"- [{(right.Contains(j) ? "x" : " ")}] {One(opts[j])}");
            }
            else if (t == "truefalse") output.Add($"= {(q.AnswerBool == true ? "Đúng" : "Sai")}");
            else if (t == "numeric")
                output.Add($"= {(q.AnswerNumber is { } v ? JsNumber.Format(v) : "undefined")}{(q.Tolerance is { } tol && tol != 0 ? " ± " + JsNumber.Format(tol) : "")}{(string.IsNullOrEmpty(q.Unit) ? "" : $" ({q.Unit})")}");
            else output.Add($"= {string.Join(" | ", q.Accept ?? [])}");
            output.AddRange(Md(q.Solution).Split('\n').Select(l => l.Length > 0 ? $"> {l}" : ">"));
            output.Add("");
        }

        var lessonIds = new HashSet<string>();
        foreach (var u in p.Units)
        {
            output.Add($"# {u.Title}");
            output.Add("");
            foreach (var l in u.Lessons)
            {
                output.Add($"## {l.Title}");
                // Id bài bộ đọc sẽ tự đặt: slug của tên, trùng thì thêm "-2". Khác thì ghi dòng id.
                var auto = Slug(l.Title, "bai");
                while (lessonIds.Contains(auto)) auto += "-2";
                if (l.Id != auto) output.Add($"id: {l.Id}");
                lessonIds.Add(l.Id);
                output.Add("");
                foreach (var s in l.Sections ?? [])
                {
                    if (!string.IsNullOrEmpty(s.Title)) output.Add($"#### {s.Title}");
                    output.Add(Md(s.Body));
                    output.Add("");
                }
                var qs = l.Questions ?? [];
                for (var i = 0; i < qs.Count; i++) Question(qs[i], i);
            }
        }
        if (p.Exams is { Count: > 0 } exams)
        {
            output.Add("# Đề thi thử");
            output.Add("");
            for (var k = 0; k < exams.Count; k++)
            {
                var x = exams[k];
                var sc = x.Scoring is { } s ? $" · Đúng: {Fixed4(s.Right)} · Sai: {Fixed4(s.Wrong)}" : "";
                output.Add($"## {x.Title}");
                if (x.Id != Slug(x.Title, $"de-{k + 1}")) output.Add($"id: {x.Id}");
                output.Add($"Thời gian: {JsNumber.Format(x.Minutes)} phút{sc}");
                if (x.QuestionRefs is { Count: > 0 } refs) output.Add($"Câu: {string.Join(", ", refs)}");
                output.Add("");
                var qs = x.Questions ?? [];
                for (var i = 0; i < qs.Count; i++) Question(qs[i], i);
            }
        }
        return (ManyBreaks().Replace(string.Join("\n", output), "\n\n"), images);
    }

    // +x.toFixed(4) của JS: làm tròn 4 chữ số rồi bỏ số 0 thừa.
    private static string Fixed4(double v) => JsNumber.Format(Math.Round(v, 4, MidpointRounding.AwayFromZero));
}
