using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Ui;

namespace SoHocTap.Presentation.Practice;

public sealed record SavedQuizQuestion(string? Type, string Html, string? Mark = null, double? Maxmark = null, string? Status = null);

/// <summary>Một lần làm quiz LMS đã lưu (data/lms-quiz, do LmsSource lưu, ApiRouter đọc kèm mã môn).</summary>
public sealed record SavedQuiz(string File, string Quiz, string Subject, string? Code, long? Course, long Attempt, long? Finished,
    JsonNode? Grade, long? ClosesAt, long? SavedAt, bool? Answers, bool? NoReview, List<SavedQuizQuestion>? Questions)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static List<SavedQuiz> ListFromJson(string json) => JsonSerializer.Deserialize<List<SavedQuiz>>(json, Json) ?? [];
}

/// <param name="LessonId">Id đầy đủ của bài trong sổ (gói.bài), rỗng khi không có câu dùng được.</param>
/// <param name="Usable">Câu biết đáp án đúng.</param>
/// <param name="Unknown">Câu LMS giấu đáp án.</param>
/// <param name="Shareable">Chỉ chia sẻ khi quiz đã đóng với mọi người.</param>
public sealed record QuizInfo(SavedQuiz Quiz, string LessonId, int Usable, int Unknown, bool Shareable);

/// <summary>
/// Quiz LMS đã lưu (trang xem lại của Moodle) thành gói luyện tập, mỗi môn một gói (port <c>lmsquiz.ts</c>).
/// Chỉ dùng những gì trang xem lại hiện; câu giấu đáp án đúng thì bỏ, không bao giờ đoán.
/// </summary>
public static partial class LmsQuiz
{
    /// <summary>Câu ghi lại từ trí nhớ cho quiz không cho xem lại (gói quiz-lms-ghi-mã).</summary>
    public const string RecallPrefix = "quiz-lms-ghi-";

    /// <summary>Chương chứa quiz LMS đã lưu và bài ghi lại câu còn nhớ: dữ liệu khớp theo tên, như UNIT_TITLE của bản TS, không dịch.</summary>
    public const string UnitTitle = "Quiz LMS đã lưu";

    public static bool IsLmsQuizLesson(string lessonId) => lessonId.StartsWith("quiz-lms-", StringComparison.Ordinal);
    public static bool IsRecallLesson(string lessonId) => lessonId.StartsWith(RecallPrefix, StringComparison.Ordinal);

    [GeneratedRegex(@"^.*?(is|are|là)\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex RightPrefix();
    [GeneratedRegex(@"^[\s\S]*?(is|are|là)\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex RightPrefixHtml();
    [GeneratedRegex(@"\s*\(.*\)\s*$")] private static partial Regex UnitSuffix();
    [GeneratedRegex(@"\(([^)]+)\)\s*$")] private static partial Regex UnitText();
    [GeneratedRegex("incorrect|không đúng|sai", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Wrong();
    [GeneratedRegex("partially|một phần", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Partial();
    [GeneratedRegex("correct|đúng", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Right();
    [GeneratedRegex(@"\d{1,2}/\d{1,2}")] private static partial Regex HasDate();
    [GeneratedRegex("[^a-z0-9]+")] private static partial Regex NonSlug();

    private const string Letters = "ABCDEFGH";

    private static string Slug(string s)
    {
        var d = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (c is < (char)0x300 or > (char)0x36F) d.Append(c is 'đ' or 'Đ' ? 'd' : c);
        var output = NonSlug().Replace(d.ToString().ToLowerInvariant(), "-").Trim('-');
        if (output.Length > 40) output = output[..40];
        return output.Length > 0 ? output : "quiz";
    }

    /// <summary>Một câu của trang xem lại Moodle thành câu của gói; null khi trang không hiện đáp án.</summary>
    public static PackQuestion? ParseReview(string html)
    {
        var doc = Html.Parse(html);
        var qtext = Html.FirstByClass(doc, "qtext");
        var prompt = qtext is null ? "" : Text.TrimJs(Html.InnerHtml(qtext));
        if (prompt.Length == 0) return null;
        var rightEl = Html.FirstByClass(doc, "rightanswer");
        var rightText = Text.TrimJs(RightPrefix().Replace(rightEl is null ? "" : Html.TextOf(rightEl), "", 1));
        var feedbackEl = Html.FirstByClass(doc, "generalfeedback");
        var feedback = feedbackEl is null ? "" : Text.TrimJs(Html.InnerHtml(feedbackEl));
        var state = Html.FirstByClass(doc, "state") is { } st ? Text.TrimJs(Html.TextOf(st)) : "";
        var rows = Html.Elements(doc).Where(e => e.Classes.Contains("answer"))
            .SelectMany(a => a.Children.OfType<HtmlElement>().Where(c => c.Tag == "div"))
            .Where(r => Html.Elements(r.Children).Any(x => x.Tag == "input"))
            .Distinct().ToList();
        string you;
        PackQuestion q;
        if (rows.Count > 0)
        {
            HtmlElement? FlexFill(HtmlElement r) => Html.Elements(r.Children).FirstOrDefault(x => x.Classes.Contains("flex-fill"));
            string Label(HtmlElement r) => Text.TrimJs(Html.InnerHtml(FlexFill(r) ?? Html.Elements(r.Children).FirstOrDefault(x => x.Tag == "label") ?? r));
            string RowText(HtmlElement r) => Text.NormText(Html.TextOf(FlexFill(r) ?? r));
            HtmlElement? Input(HtmlElement r) => Html.Elements(r.Children).FirstOrDefault(x => x.Tag == "input");
            var options = rows.Select(Label).ToList();
            var multi = Html.Elements(rows[0].Children).Any(x => x.Tag == "input" && x.Attr("type") == "checkbox");
            var picked = rows.Select((r, i) => Input(r)?.Attr("checked") != null ? i : -1).Where(i => i >= 0).ToList();
            var right = rows.Select((r, i) => r.Classes.Contains("correct") ? i : -1).Where(i => i >= 0).ToHashSet();
            var want = Text.NormText(rightText);
            if (want.Length > 0)
                for (var i = 0; i < rows.Count; i++)
                {
                    var t = RowText(rows[i]);
                    if (t.Length > 0 && (t == want || (multi && want.Contains(t, StringComparison.Ordinal)))) right.Add(i);
                }
            you = string.Join(", ", picked.Select(i => i < Letters.Length ? Letters[i].ToString() : ""));
            var answers = right.Order().ToArray();
            if (answers.Length == 0 || (!multi && answers.Length > 1)) return null;
            q = multi
                ? new PackQuestion { Type = "multi", Prompt = prompt, Options = options, Answers = answers, Solution = "" }
                : new PackQuestion { Prompt = prompt, Options = options, Answer = JsonValue.Create(answers[0]), Solution = "" };
        }
        else
        {
            you = Html.Elements(doc).FirstOrDefault(x => x.Tag == "input" && x.Attr("type") == "text")?.Attr("value") ?? "";
            if (rightText.Length == 0) return null;
            var n = Grade.ParseNumber(UnitSuffix().Replace(rightText, "", 1));
            var unit = UnitText().Match(rightText);
            q = n is { } v
                ? new PackQuestion { Type = "numeric", Prompt = prompt, Answer = JsonValue.Create(v), Unit = unit.Success ? unit.Groups[1].Value : null, Solution = "" }
                : new PackQuestion { Type = "short", Prompt = prompt, Accept = [rightText], Solution = "" };
        }
        var result = Wrong().IsMatch(state) ? L.T("quiz.result.wrong")
            : Partial().IsMatch(state) ? L.T("quiz.result.partial")
            : Right().IsMatch(state) ? L.T("quiz.result.right") : "";
        var solution = string.Concat(
            rightEl is null ? "" : $"<p><b>{L.T("quiz.rightAnswer")}</b> {RightPrefixHtml().Replace(Html.InnerHtml(rightEl), "", 1)}</p>",
            feedback,
            you.Length > 0 ? $"<p><small>{L.F("quiz.youPicked", you, result.Length > 0 ? $" ({result})" : "")}</small></p>" : "");
        q.Solution = solution.Length > 0 ? solution : $"<p>{L.T("quiz.noSolution")}</p>";
        return q;
    }

    /// <summary>Mọi quiz đã lưu thành gói theo mã môn (chương "Quiz LMS đã lưu", mỗi lần làm một bài).</summary>
    public static (List<StudyPack> Packs, List<QuizInfo> Info) QuizPacks(IEnumerable<SavedQuiz> saved, DateTimeOffset now, TimeZoneInfo zone)
    {
        var byCode = new Dictionary<string, StudyPack>();
        var order = new List<string>();
        var info = new List<QuizInfo>();
        string DateText(long? t) => t is { } s && s != 0
            ? TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(s), zone).ToString(L.T("quiz.dateFormat"), CultureInfo.InvariantCulture)
            : "";
        foreach (var s in saved.OrderBy(x => x.Finished ?? 0))
        {
            var code = (s.Code ?? "").ToUpperInvariant();
            var parsed = (s.Questions ?? []).Select(x => ParseReview(x.Html)).ToList();
            var questions = parsed.OfType<PackQuestion>().ToList();
            for (var i = 0; i < questions.Count; i++) questions[i].Id = $"q{i + 1}";
            var local = $"{Slug(s.Quiz)}-{s.Attempt}";
            var packId = Slug($"quiz-lms-{code}");
            var lessonId = code.Length > 0 && questions.Count > 0 ? $"{packId}.{local}" : "";
            info.Add(new QuizInfo(s, lessonId, questions.Count, parsed.Count - questions.Count, s.ClosesAt is { } c && c != 0 && c < now.ToUnixTimeSeconds()));
            if (code.Length == 0 || questions.Count == 0) continue;
            if (!byCode.TryGetValue(code, out var pack))
            {
                byCode[code] = pack = new StudyPack
                {
                    Id = packId,
                    Title = L.F("quiz.packTitle", s.Subject),
                    Version = "1.0.0",
                    Language = "vi",
                    Course = new PackCourse(code, s.Subject, "HCMUT"),
                    Authors = [new PackAuthor(L.T("quiz.author"))],
                    CreatedWith = L.F("quiz.createdWith", SoHocTap.Shell.AppInfo.Name),
                    Verified = new PackVerified(L.T("quiz.verified")),
                    Units = [new PackUnit { Title = UnitTitle }],
                };
                order.Add(code);
            }
            var finished = s.Finished is { } f && f != 0;
            pack.Units[0].Lessons.Add(new PackLesson
            {
                // Thêm ngày khi tên quiz chưa có ngày.
                Id = local,
                Title = finished && !HasDate().IsMatch(s.Quiz) ? $"{s.Quiz} ({DateText(s.Finished)})" : s.Quiz,
                Sources = [new PackSource(L.F("quiz.source", s.Quiz), finished ? L.F("quiz.doneOn", DateText(s.Finished)) : null)],
                Questions = questions,
            });
        }
        return ([.. order.Select(c => byCode[c])], info);
    }
}
