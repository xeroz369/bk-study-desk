using SoHocTap.Ui;

namespace SoHocTap.Presentation.Practice;

/// <summary>Lượt làm câu mở từ đâu; quyết định ghi gì vào kết quả khi trả lời.</summary>
public enum RunMode { Lesson, Review, Due, Mix, Flagged }

/// <summary>Xáo thứ tự câu (giữ nhóm liền nhau) và xáo phương án.</summary>
public sealed record ShufflePrefs(bool Questions, bool Options)
{
    public static readonly ShufflePrefs None = new(false, false);
}

/// <summary>
/// Một lượt làm câu ở chế độ luyện (port phần không đụng giao diện của <c>QuestionCard.svelte</c> và các trang
/// Lesson, Review, TodayReview, MixedPractice, Flagged): sai thì làm lại, đúng thì xong câu. Đáp án luôn theo chỉ số gốc.
/// </summary>
public sealed class QuizSession
{
    private const string Letters = "ABCDEFGH";

    private readonly PracticeProgress _progress;
    private readonly Dictionary<string, int[]> _orders = [];
    private readonly Dictionary<string, Answer> _tried = [];

    public QuizSession(PracticeProgress progress, RunMode mode, IReadOnlyList<Question> questions, ShufflePrefs? shuffle = null, uint seed = 0)
    {
        _progress = progress;
        Mode = mode;
        shuffle ??= ShufflePrefs.None;
        Shown = shuffle.Questions ? ExamPick.ShuffleGroups(questions, Shuffle.Rng(seed)) : [.. questions];
        if (shuffle.Options)
            foreach (var q in Shown)
                if (Shuffle.OptionOrder(q, Shuffle.Rng(Shuffle.SeedFor(seed, q.Id))) is var o && o.Where((v, i) => v != i).Any()) _orders[q.Id] = o;
    }

    public RunMode Mode { get; }

    /// <summary>Câu theo thứ tự hiện (đã xáo nếu bật). Chốt khi mở lượt.</summary>
    public IReadOnlyList<Question> Shown { get; }

    /// <summary>Số câu đã trả lời ít nhất một lần trong lượt này.</summary>
    public int Done => _tried.Count;

    /// <summary>Thứ tự hiện phương án (vị trí hiện thành chỉ số gốc); null khi giữ nguyên.</summary>
    public int[]? Order(Question q) => _orders.GetValueOrDefault(q.Id);

    /// <summary>Lần trả lời gần nhất trong lượt (null khi chưa làm).</summary>
    public Answer? Tried(Question q) => _tried.TryGetValue(q.Id, out var a) ? a : null;

    public bool Solved(Question q) => Tried(q) is { } a && Grade.IsCorrect(q, a);

    /// <summary>Chữ cái của phương án gốc <paramref name="index"/> như người học thấy.</summary>
    public string Letter(Question q, int index)
    {
        var pos = Order(q) is { } o ? Array.IndexOf(o, index) : index;
        return pos >= 0 && pos < Letters.Length ? Letters[pos].ToString() : "?";
    }

    /// <summary>Đáp án đúng theo chữ cái người học thấy (đã tính thứ tự xáo).</summary>
    public string RightText(Question q) => Grade.QType(q) switch
    {
        "multi" => string.Join(", ", (q.Answers ?? []).Select(i => Letter(q, i)).Order(StringComparer.Ordinal)),
        "single" => Letter(q, q.Answer),
        _ => Grade.AnswerText(q),
    };

    /// <summary>
    /// Ghi một lần trả lời; trả về đúng hay sai. Bỏ trống thì không ghi. <paramref name="seconds"/>: thời gian từ lúc câu hiện tới
    /// lần trả lời đầu (chỉ ghi lần đầu).
    /// </summary>
    public bool Answer(Question q, Answer chosen, double? seconds = null)
    {
        if (SoHocTap.Presentation.Practice.Answer.IsBlankValue(chosen)) return false;
        if (!_tried.ContainsKey(q.Id) && seconds is { } s) _progress.RecordTime(q.Fp, s);
        _tried[q.Id] = chosen;
        var ok = Grade.IsCorrect(q, chosen);
        _progress.RecordAnswer(q.Id, chosen, ok);
        if (Mode is RunMode.Review or RunMode.Due) _progress.MarkReviewed(q.Id, ok);
        return ok;
    }

    /// <summary>Dòng trạng thái dưới số câu: gợi ý cách trả lời, đã làm chưa, cờ nghi sai (nối bằng dấu phẩy như bản Svelte).</summary>
    public string Status(Question q)
    {
        var parts = new List<string>();
        switch (Grade.QType(q))
        {
            case "multi": parts.Add(L.T("practice.run.hintMulti")); break;
            case "numeric": parts.Add(string.IsNullOrEmpty(q.Unit) ? L.T("practice.run.hintNumeric") : L.F("practice.run.hintNumericUnit", q.Unit)); break;
            case "short": parts.Add(L.T("practice.run.hintShort")); break;
        }
        if (_progress.Q(q.Id) is { } r) parts.Add(L.T(r.Correct ? "practice.run.doneRight" : "practice.run.doneWrong"));
        if (_progress.Note(q.Fp)?.Flag == true) parts.Add(L.T("practice.run.flagged"));
        return string.Join(", ", parts);
    }
}
