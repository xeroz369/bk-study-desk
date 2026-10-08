using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Ui;

namespace SoHocTap.Presentation.Practice;

public sealed record PackIssue(string Path, string Message);

public sealed record PackStats(int Lessons, int Questions, int Exams);

/// <summary>Kết quả kiểm tra gói: lỗi thì app không nạp, cảnh báo thì vẫn nạp nhưng nên sửa.</summary>
public sealed record PackReport(bool Ok, IReadOnlyList<PackIssue> Errors, IReadOnlyList<PackIssue> Warnings, PackStats Stats);

/// <summary>
/// Kiểm tra một gói Study Pack v1 đã parse (port <c>validatePack</c> của <c>pack.ts</c>, cùng đường dẫn lỗi).
/// Đầu vào là JsonNode chứ không phải kiểu đã đọc, vì gói đến từ người khác: phải bắt được cả kiểu sai, thiếu khóa.
/// Câu thông báo ở lang (nhóm <c>pack.*</c>).
/// </summary>
public static partial class PackValidator
{
    private const long MaxBytes = 20 * 1024 * 1024;
    private const int MaxImageChars = 1536 * 1024;
    private static readonly string[] QuestionTypes = ["single", "multi", "truefalse", "numeric", "short"];

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,63}$")] private static partial Regex IdRe();
    [GeneratedRegex(@"^\d+\.\d+\.\d+$")] private static partial Regex VersionRe();
    [GeneratedRegex(@"<\s*(script|style|iframe|object|embed|form|input|link|meta|svg|math|base)\b", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)] private static partial Regex Forbidden();
    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)] private static partial Regex Img();
    [GeneratedRegex(@"\ssrc\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)] private static partial Regex Src();
    [GeneratedRegex(@"\son\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)] private static partial Regex EventAttr();
    [GeneratedRegex("javascript:", RegexOptions.IgnoreCase)] private static partial Regex JsUrl();
    // [\f\b\t\v\x00-\x08\x0e-\x1f] của JS: trong lớp ký tự, \b là backspace (0x08).
    [GeneratedRegex(@"[\x00-\x09\x0b\x0c\x0e-\x1f]")] private static partial Regex Control();
    [GeneratedRegex(@"\r[a-z]")] private static partial Regex CrLetter();
    [GeneratedRegex(@"\n(eq|abla|u\b|ot\b|ewline|exists|leq|geq|i\b|cong|parallel|mid\b)", RegexOptions.ECMAScript)] private static partial Regex LfLetter();
    [GeneratedRegex(@"<\/?([a-z][a-z0-9]*)\b", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)] private static partial Regex AnyTag();

    public static PackReport Validate(JsonNode? input, long rawSize = 0)
    {
        var v = new Run();
        if (rawSize > MaxBytes) v.Err("", L.F("pack.tooLarge", Math.Round(rawSize / 1024.0), MaxBytes / 1024 / 1024));
        if (input is not JsonObject p)
            return new PackReport(false, [new PackIssue("", L.T("pack.notObject"))], v.Warnings, new PackStats(0, 0, 0));
        v.Pack(p);
        return new PackReport(v.Errors.Count == 0, v.Errors, v.Warnings, new PackStats(v.Lessons, v.Questions, v.Exams));
    }

    /// <summary>Tổng số câu hỏi trong gói (danh sách gói đã cài).</summary>
    public static int CountQuestions(StudyPack p) =>
        p.Units.Sum(u => u.Lessons.Sum(l => l.Questions?.Count ?? 0)) + (p.Exams ?? []).Sum(x => x.Questions?.Count ?? 0);

    private sealed class Run
    {
        public readonly List<PackIssue> Errors = [];
        public readonly List<PackIssue> Warnings = [];
        public int Lessons, Questions, Exams;
        private readonly HashSet<string> _allQ = [];
        private readonly HashSet<string> _lessonIds = [];

        public void Err(string path, string message) => Errors.Add(new PackIssue(path, message));
        private void Warn(string path, string message) => Warnings.Add(new PackIssue(path, message));

        // undefined của JS là khóa không có; null của JSON là có khóa (TS coi là "đã ghi").
        private static bool Has(JsonObject o, string key) => o.ContainsKey(key);

        private static bool IsString(JsonNode? n, out string s)
        {
            s = "";
            if (n is JsonValue v && v.GetValueKind() == JsonValueKind.String) { s = v.GetValue<string>(); return true; }
            return false;
        }

        private static bool IsNumber(JsonNode? n, out double d)
        {
            d = 0;
            return n is JsonValue v && v.GetValueKind() == JsonValueKind.Number && v.TryGetValue(out d);
        }

        private static bool IsInteger(JsonNode? n, out long i)
        {
            i = 0;
            if (!IsNumber(n, out var d) || d != Math.Floor(d) || double.IsInfinity(d)) return false;
            i = (long)d;
            return true;
        }

        private static string JsString(JsonNode? n) => n is null ? "null" : IsString(n, out var s) ? s : n.ToJsonString();

        private bool Str(JsonObject o, string key, string path, bool required = true)
        {
            var n = o[key];
            if (IsString(n, out var s) && Text.TrimJs(s).Length > 0) return true;
            if (required || Has(o, key)) Err(path, L.T("pack.nonEmpty"));
            return false;
        }

        private bool StrNode(JsonNode? n, string path)
        {
            if (IsString(n, out var s) && Text.TrimJs(s).Length > 0) return true;
            Err(path, L.T("pack.nonEmpty"));
            return false;
        }

        private void TextField(JsonObject o, string key, string path, bool required = true)
        {
            if (Str(o, key, path, required)) CheckText(o[key]!.GetValue<string>(), path);
        }

        private void TextNode(JsonNode? n, string path)
        {
            if (StrNode(n, path)) CheckText(n!.GetValue<string>(), path);
        }

        public void Pack(JsonObject p)
        {
            if (!(IsString(p["format"], out var f) && f == StudyPack.FormatId)) Err("format", L.F("pack.format", StudyPack.FormatId));
            if (!(IsString(p["id"], out var id) && IdRe().IsMatch(id))) Err("id", L.T("pack.id"));
            Str(p, "title", "title");
            if (!(IsString(p["version"], out var ver) && VersionRe().IsMatch(ver))) Err("version", L.T("pack.version"));
            if (p["course"] is not JsonObject course) Err("course", L.T("pack.course"));
            else
            {
                Str(course, "code", "course.code");
                Str(course, "name", "course.name");
            }
            if (p["authors"] is not JsonArray authors || authors.Count == 0) Err("authors", L.T("pack.authors"));
            else
                for (var i = 0; i < authors.Count; i++)
                    if (authors[i] is JsonObject a) Str(a, "name", $"authors[{i}].name");
                    else Err($"authors[{i}]", L.T("pack.author"));
            if (!Has(p, "license")) Warn("license", L.T("pack.license"));
            if (!Has(p, "verified")) Warn("verified", L.T("pack.verified"));
            Sources(p, "sources", "sources");

            if (p["units"] is not JsonArray units || units.Count == 0) Err("units", L.T("pack.units"));
            else
                for (var ui = 0; ui < units.Count; ui++) Unit(units[ui], $"units[{ui}]");

            if (Has(p, "exams"))
            {
                if (p["exams"] is not JsonArray exams) Err("exams", L.T("pack.exams"));
                else
                    for (var xi = 0; xi < exams.Count; xi++) Exam(exams[xi], $"exams[{xi}]");
            }
            if (Questions == 0) Warn("", L.T("pack.noQuestions"));
        }

        private void Sources(JsonObject o, string key, string path)
        {
            if (!Has(o, key)) return;
            SourceList(o[key], path);
        }

        private void SourceList(JsonNode? n, string path)
        {
            if (n is not JsonArray arr)
            {
                Err(path, L.T("pack.sources"));
                return;
            }
            for (var i = 0; i < arr.Count; i++)
                if (arr[i] is JsonObject s) Str(s, "title", $"{path}[{i}].title");
                else Err($"{path}[{i}]", L.T("pack.source"));
        }

        private void Unit(JsonNode? n, string up)
        {
            if (n is not JsonObject u)
            {
                Err(up, L.T("pack.unit"));
                return;
            }
            Str(u, "title", $"{up}.title");
            if (u["lessons"] is not JsonArray lessons || lessons.Count == 0)
            {
                Err($"{up}.lessons", L.T("pack.lessons"));
                return;
            }
            for (var li = 0; li < lessons.Count; li++) Lesson(lessons[li], $"{up}.lessons[{li}]");
        }

        private void Lesson(JsonNode? n, string lp)
        {
            if (n is not JsonObject l)
            {
                Err(lp, L.T("pack.lessonObject"));
                return;
            }
            Lessons++;
            if (!(IsString(l["id"], out var id) && IdRe().IsMatch(id))) Err($"{lp}.id", L.T("pack.lessonId"));
            else if (!_lessonIds.Add(id)) Err($"{lp}.id", L.F("pack.duplicateLesson", id));
            Str(l, "title", $"{lp}.title");
            Sources(l, "sources", $"{lp}.sources");
            if (Has(l, "sections"))
            {
                if (l["sections"] is not JsonArray sections) Err($"{lp}.sections", L.T("pack.sections"));
                else
                    for (var si = 0; si < sections.Count; si++)
                    {
                        if (sections[si] is not JsonObject s)
                        {
                            Err($"{lp}.sections[{si}]", L.T("pack.section"));
                            continue;
                        }
                        if (Has(s, "title")) TextField(s, "title", $"{lp}.sections[{si}].title");
                        TextField(s, "body", $"{lp}.sections[{si}].body");
                    }
            }
            if (Has(l, "questions"))
            {
                if (l["questions"] is not JsonArray qs) Err($"{lp}.questions", L.T("pack.questions"));
                else
                    for (var qi = 0; qi < qs.Count; qi++) Question(qs[qi], $"{lp}.questions[{qi}]", JsString(l["id"]), qi);
            }
            if (l["sections"] is null && l["questions"] is null) Warn(lp, L.T("pack.emptyLesson"));
        }

        private void Exam(JsonNode? n, string xp)
        {
            if (n is not JsonObject x)
            {
                Err(xp, L.T("pack.examObject"));
                return;
            }
            Exams++;
            if (!(IsString(x["id"], out var id) && IdRe().IsMatch(id))) Err($"{xp}.id", L.T("pack.examId"));
            else if (_lessonIds.Contains(id)) Err($"{xp}.id", L.T("pack.examIdClash"));
            Str(x, "title", $"{xp}.title");
            if (!(IsNumber(x["minutes"], out var m) && m > 0 && m <= 600)) Err($"{xp}.minutes", L.T("pack.minutes"));
            if (Has(x, "scoring"))
            {
                if (x["scoring"] is not JsonObject s || !IsNumber(s["count"], out _) || !IsNumber(s["right"], out _) || !IsNumber(s["wrong"], out _))
                    Err($"{xp}.scoring", L.T("pack.scoring"));
            }
            if (x["questions"] is JsonArray qs)
                for (var qi = 0; qi < qs.Count; qi++) Question(qs[qi], $"{xp}.questions[{qi}]", JsString(x["id"]), qi);
            if (Has(x, "questionRefs"))
            {
                if (x["questionRefs"] is not JsonArray refs) Err($"{xp}.questionRefs", L.T("pack.refs"));
                else
                    for (var ri = 0; ri < refs.Count; ri++)
                        if (!(IsString(refs[ri], out var r) && _allQ.Contains(r)))
                            Err($"{xp}.questionRefs[{ri}]", L.F("pack.refMissing", JsString(refs[ri])));
            }
            if (x["questions"] is null && x["questionRefs"] is null) Err(xp, L.T("pack.examEmpty"));
        }

        private void Question(JsonNode? n, string path, string owner, int i)
        {
            if (n is not JsonObject q)
            {
                Err(path, L.T("pack.questionObject"));
                return;
            }
            Questions++;
            var type = q["type"] is null ? "single" : JsString(q["type"]);
            if (!QuestionTypes.Contains(type)) Err($"{path}.type", L.F("pack.type", string.Join(", ", QuestionTypes)));
            var qidNode = Has(q, "id") ? q["id"] : JsonValue.Create($"q{i + 1}");
            if (!(IsString(qidNode, out var qid) && IdRe().IsMatch(qid))) Err($"{path}.id", L.T("pack.questionId"));
            else if (!_allQ.Add($"{owner}.{qid}")) Err($"{path}.id", L.F("pack.duplicateId", qid));
            TextField(q, "prompt", $"{path}.prompt");
            TextField(q, "solution", $"{path}.solution");
            if (Has(q, "tag")) Str(q, "tag", $"{path}.tag");
            if (Has(q, "group")) Str(q, "group", $"{path}.group");
            if (type is "single" or "multi")
            {
                if (q["options"] is not JsonArray opts || opts.Count < 2 || opts.Count > 8) Err($"{path}.options", L.T("pack.options"));
                else
                {
                    var count = opts.Count;
                    for (var k = 0; k < count; k++) TextNode(opts[k], $"{path}.options[{k}]");
                    var norm = opts.Select(o => Text.TrimJs(Text.CollapseJs(JsString(o), " "))).ToList();
                    if (norm.Distinct().Count() != norm.Count) Warn($"{path}.options", L.T("pack.optionsDuplicate"));
                    bool Idx(JsonNode? v) => IsInteger(v, out var x) && x >= 0 && x < count;
                    if (type == "single" && !Idx(q["answer"])) Err($"{path}.answer", L.F("pack.answerIndex", count - 1));
                    if (type == "multi")
                    {
                        if (q["answers"] is not JsonArray ans || ans.Count == 0 || !ans.All(Idx)) Err($"{path}.answers", L.F("pack.answers", count - 1));
                        else if (ans.Select(a => a!.GetValue<double>()).Distinct().Count() != ans.Count) Err($"{path}.answers", L.T("pack.answersRepeat"));
                    }
                }
            }
            else if (type == "truefalse")
            {
                if (q["answer"] is not JsonValue b || b.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False)) Err($"{path}.answer", L.T("pack.trueFalse"));
            }
            else if (type == "numeric")
            {
                var isNum = IsNumber(q["answer"], out var ansNum) && double.IsFinite(ansNum);
                if (!isNum) Err($"{path}.answer", L.T("pack.numericAnswer"));
                if (Has(q, "tolerance") && !(IsNumber(q["tolerance"], out var tol) && tol >= 0)) Err($"{path}.tolerance", L.T("pack.tolerance"));
                if (!Has(q, "tolerance") && IsNumber(q["answer"], out var a2) && a2 != Math.Floor(a2)) Warn($"{path}.tolerance", L.T("pack.toleranceMissing"));
                if (Has(q, "unit")) Str(q, "unit", $"{path}.unit");
            }
            else if (type == "short")
            {
                if (q["accept"] is not JsonArray acc || acc.Count == 0 || !acc.All(x => IsString(x, out var s) && Text.TrimJs(s).Length > 0))
                    Err($"{path}.accept", L.T("pack.accept"));
            }
            if (Has(q, "difficulty") && !(IsInteger(q["difficulty"], out var d) && d is >= 1 and <= 3)) Err($"{path}.difficulty", L.T("pack.difficulty"));
            if (Has(q, "source")) SourceList(new JsonArray(q["source"]?.DeepClone()), $"{path}.source");
        }

        /// <summary>Một đoạn nội dung: HTML lạ, script, lỗi escape TeX trong JSON.</summary>
        private void CheckText(string s, string path)
        {
            if (Forbidden().IsMatch(s)) Err(path, L.T("pack.text.forbiddenTag"));
            foreach (Match m in Img().Matches(s))
            {
                var src = Src().Match(m.Value) is { Success: true } sm ? sm.Groups[1].Value : "";
                if (!StudyPack.ImgSrc().IsMatch(src))
                {
                    Err(path, L.T("pack.text.image"));
                    break;
                }
                if (src.Length > MaxImageChars) Warn(path, L.F("pack.text.bigImage", Math.Round(src.Length / 1024.0)));
            }
            if (EventAttr().IsMatch(s) || JsUrl().IsMatch(s)) Err(path, L.T("pack.text.events"));
            // JSON hiểu "\f", "\b", "\t", "\v" là ký tự điều khiển: dấu hiệu quên viết "\\frac", "\\beta", "\\times".
            if (Control().Match(s) is { Success: true } ctl) Err(path, L.F("pack.text.control", (int)ctl.Value[0]));
            // "\right", "\rho" thành CR + "ight"; "\neq", "\nabla" thành xuống dòng + "eq": cũng là quên escape.
            if (CrLetter().IsMatch(s)) Err(path, L.T("pack.text.cr"));
            if (LfLetter().IsMatch(s)) Warn(path, L.T("pack.text.lf"));
            // Chỉ tính là thẻ khi sau "<" là chữ cái: "f(0)<0", "x<10" là toán, không phải thẻ.
            foreach (Match m in AnyTag().Matches(s))
                if (!StudyPack.AllowedTags.Contains(m.Groups[1].Value.ToLowerInvariant()))
                {
                    Warn(path, L.F("pack.text.tag", m.Groups[1].Value));
                    break;
                }
            int opens = Count(s, @"\("), closes = Count(s, @"\)");
            if (opens != closes) Warn(path, L.F("pack.text.inline", opens, closes));
            if (Count(s, @"\[") != Count(s, @"\]")) Warn(path, L.T("pack.text.display"));
        }

        private static int Count(string s, string what)
        {
            var n = 0;
            for (var i = s.IndexOf(what, StringComparison.Ordinal); i >= 0; i = s.IndexOf(what, i + what.Length, StringComparison.Ordinal)) n++;
            return n;
        }
    }
}
