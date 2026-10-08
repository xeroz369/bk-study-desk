using System.Globalization;

namespace SoHocTap.Presentation.Practice;

/// <summary>Kết quả một lần nộp đề. Ten: điểm quy về thang 10 (điểm âm tính là 0).</summary>
public sealed record ExamResult(int Right, int Wrong, int Blank, double Score, double Max, int Seconds, double Ten);

/// <summary>
/// Một lần thi thử (port phần không đụng giao diện của <c>Exam.svelte</c>): chọn đáp án không báo đúng sai, đếm giờ theo hạn chót
/// (không đếm nhịp, vì máy ngủ hay trang ẩn làm nhịp chậm lại), nộp thì chấm theo luật trừ điểm, ghi lịch ôn, thời gian, lượt thi.
/// Thời gian từng câu là khoảng từ lần chọn trước tới lần chọn câu này.
/// </summary>
public sealed class ExamSession
{
    private readonly PracticeProgress _progress;
    private readonly TimeProvider _clock;
    private readonly Scoring _scoring;
    private readonly DateTimeOffset _started;
    private readonly DateTimeOffset _deadline;
    private readonly Dictionary<string, Answer> _picked = [];
    private readonly Dictionary<string, double> _times = [];
    private readonly Dictionary<string, int[]> _orders = [];
    private DateTimeOffset _lastAt;

    public ExamSession(PracticeProgress progress, Exam exam, IReadOnlyList<Question> questions, Scoring scoring,
        ShufflePrefs? shuffle = null, uint seed = 0, TimeProvider? clock = null)
    {
        _progress = progress;
        _clock = clock ?? TimeProvider.System;
        _scoring = scoring;
        Exam = exam;
        shuffle ??= ShufflePrefs.None;
        // Câu cùng nhóm (dùng chung đề) vẫn đứng liền nhau khi xáo.
        Shown = shuffle.Questions ? ExamPick.ShuffleGroups(questions, Shuffle.Rng(seed)) : [.. questions];
        if (shuffle.Options)
            foreach (var q in Shown)
                if (Shuffle.OptionOrder(q, Shuffle.Rng(Shuffle.SeedFor(seed, q.Id))) is var o && o.Where((v, i) => v != i).Any()) _orders[q.Id] = o;
        _started = _lastAt = _clock.GetUtcNow();
        _deadline = _started.AddMinutes(exam.Minutes);
        Pace = Shown.Count > 0 ? exam.Minutes * 60 / Shown.Count : 0;
    }

    /// <summary>Luật chấm: của đề, không thì của môn, không thì mỗi câu 1 điểm, sai 0.</summary>
    public static Scoring ScoringFor(StudyRegistry study, Exam exam, int count) =>
        exam.Scoring ?? study.Course(exam.CourseId)?.Scoring ?? new Scoring(count, 1, 0);

    /// <summary>Giây thành "mm:ss" (phút không giới hạn 59), như <c>clock</c> của format.ts.</summary>
    public static string Clock(double seconds)
    {
        var s = (int)Math.Max(0, Math.Floor(seconds + 0.5));
        return $"{(s / 60).ToString("00", CultureInfo.InvariantCulture)}:{(s % 60).ToString("00", CultureInfo.InvariantCulture)}";
    }

    public Exam Exam { get; }

    public Scoring Scoring => _scoring;

    public IReadOnlyList<Question> Shown { get; }

    /// <summary>Giây mỗi câu theo đề thật (phút nhân 60 chia số câu).</summary>
    public double Pace { get; }

    public ExamResult? Result { get; private set; }

    public int[]? Order(Question q) => _orders.GetValueOrDefault(q.Id);

    public Answer? Picked(Question q) => _picked.TryGetValue(q.Id, out var a) ? a : null;

    public int PickedCount => _picked.Count;

    /// <summary>Số giây còn lại, tính từ hạn chót.</summary>
    public int Left => Math.Max(0, (int)Math.Round((_deadline - _clock.GetUtcNow()).TotalSeconds, MidpointRounding.AwayFromZero));

    public bool Expired => _clock.GetUtcNow() >= _deadline;

    public double Seconds(Question q) => _times.GetValueOrDefault(q.Id);

    /// <summary>Chọn đáp án; null (hay bỏ trống) là bỏ chọn. Cộng thời gian từ lần chọn trước vào câu này.</summary>
    public void Pick(Question q, Answer? a)
    {
        if (Result != null) return;
        var now = _clock.GetUtcNow();
        _times[q.Id] = Math.Floor(_times.GetValueOrDefault(q.Id) + (now - _lastAt).TotalSeconds + 0.5);   // Math.round của JS
        _lastAt = now;
        if (a is not { } v || Answer.IsBlankValue(v)) _picked.Remove(q.Id);
        else _picked[q.Id] = v;
    }

    /// <summary>Số thứ tự (từ 1, theo thứ tự hiện) của câu làm quá 1,5 lần nhịp đề.</summary>
    public List<int> Slow() => [.. Shown.Select((q, i) => (q, i)).Where(x => Seconds(x.q) > Pace * 1.5).Select(x => x.i + 1)];

    /// <summary>Nộp bài: chấm, ghi lịch ôn (bỏ trống tính là chưa biết), thời gian, lượt thi. Gọi lại thì trả kết quả cũ.</summary>
    public ExamResult Finish()
    {
        if (Result is { } done) return done;
        int right = 0, wrong = 0, blank = 0;
        foreach (var q in Shown)
        {
            var p = Picked(q);
            var ok = !Answer.IsBlankValue(p) && Grade.IsCorrect(q, p);
            if (Answer.IsBlankValue(p)) blank++;
            else if (ok) right++;
            else wrong++;
            if (q.Fp is { } fp) _progress.SrsAnswer(fp, ok);
            if (_times.GetValueOrDefault(q.Id) is > 0 and var t) _progress.RecordTime(q.Fp, t);
        }
        var score = right * _scoring.Right + wrong * _scoring.Wrong;
        var max = Shown.Count * _scoring.Right;
        var seconds = (int)Math.Floor((_clock.GetUtcNow() - _started).TotalSeconds + 0.5);
        _progress.AddExamAttempt(new ExamAttempt
        {
            ExamId = Exam.Id,
            Title = Exam.Title,
            StartedAt = _started.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            Seconds = seconds,
            Answers = new Dictionary<string, Answer>(_picked),
            Times = new Dictionary<string, double>(_times),
            Right = right,
            Wrong = wrong,
            Blank = blank,
            Score = Math.Round(score * 1000, MidpointRounding.AwayFromZero) / 1000,
            Max = max,
        });
        Result = new ExamResult(right, wrong, blank, score, max, seconds, max > 0 ? Math.Max(0, score) / max * 10 : 0);
        return Result;
    }
}
