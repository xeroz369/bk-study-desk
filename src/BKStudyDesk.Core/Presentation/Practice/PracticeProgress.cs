using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SoHocTap.Ui;

namespace SoHocTap.Presentation.Practice;

public enum LessonStatus { NotWritten, NotStarted, InProgress, Done }

public static class LessonStatusText
{
    public static string Label(this LessonStatus s) => L.T(s switch
    {
        LessonStatus.NotWritten => "practice.status.notWritten",
        LessonStatus.NotStarted => "practice.status.notStarted",
        LessonStatus.InProgress => "practice.status.inProgress",
        _ => "practice.status.done",
    });
}

/// <summary>Số liệu của một môn: số bài, bài đã soạn, bài xong, câu đã làm, câu đúng ngay lần đầu.</summary>
public sealed record CourseStats(int Total, int Written, int Done, int Answered, int FirstOk);

/// <summary>Chỗ đọc ghi <c>ket-qua.json</c>. App dùng <see cref="ApiProgressStorage"/>; test dùng bản trong bộ nhớ.</summary>
public interface IProgressStorage
{
    /// <summary>Nội dung JSON; null khi chưa có. Ném lỗi khi không đọc được.</summary>
    Task<string?> ReadAsync(CancellationToken ct);

    Task<bool> WriteAsync(string json, CancellationToken ct);
}

/// <summary>
/// Kết quả luyện tập và lịch ôn (port <c>progress.svelte.ts</c>). Ghi qua <see cref="IProgressStorage"/>:
/// <see cref="Save"/> chỉ đánh dấu còn thay đổi, view gọi <see cref="FlushAsync"/> (gom ghi là việc của view).
/// </summary>
public sealed class PracticeProgress(StudyRegistry study, IProgressStorage storage, TimeProvider? clock = null)
{
    /// <summary>Hộp Leitner: số ngày tới lần ôn sau. Sai về hộp 1 (mai ôn), đúng lên hộp kế.</summary>
    private static readonly int[] Interval = [0, 1, 3, 7, 14, 30];

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>Múi giờ của máy (hiện ngày giờ của kết quả).</summary>
    public TimeZoneInfo Zone => _clock.LocalTimeZone;

    public ProgressState State { get; private set; } = new();

    /// <summary>Lỗi đọc, ghi gần nhất (rỗng khi ổn).</summary>
    public string SaveError { get; private set; } = "";

    /// <summary>Còn thay đổi chưa ghi.</summary>
    public bool Pending { get; private set; }

    /// <summary>Ngày theo giờ máy, YYYY-MM-DD.</summary>
    public string Today(int offsetDays = 0) =>
        TimeZoneInfo.ConvertTime(_clock.GetUtcNow().AddDays(offsetDays), _clock.LocalTimeZone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // new Date().toISOString() của JS.
    private string Now() => _clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Lần đọc gần nhất lỗi (file hỏng, bị khóa): không ghi gì cho tới khi đọc lại được, để không đè mất kết quả cũ.
    /// Câu làm trong lúc đó vẫn giữ trong app và được gộp vào khi đọc lại thành công.
    /// </summary>
    public bool ReadFailed { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await storage.ReadAsync(ct);
            var loaded = ProgressState.Normalize(json is null ? null : JsonNode.Parse(json));
            // Đọc lại sau một lần lỗi: gộp phần đã làm trong app vào, rồi ghi như thường.
            State = ReadFailed && Pending ? ProgressState.Merge(loaded, State) : loaded;
            ReadFailed = false;
            SaveError = "";
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (!ReadFailed) State = new ProgressState();
            ReadFailed = true;
            SaveError = L.T("practice.saveError.read");
        }
    }

    public void Save() => Pending = true;

    /// <summary>
    /// Nạp lại cả sổ tạo bản mới: mang theo câu đã làm ở bản cũ mà chưa ghi được vì file không đọc được (ReadFailed).
    /// </summary>
    public void Absorb(PracticeProgress old)
    {
        if (!old.ReadFailed || !old.Pending) return;
        State = ReadFailed ? old.State : ProgressState.Merge(State, old.State);
        Save();
    }

    /// <summary>Ghi ngay. Ghi lỗi thì vẫn còn Pending để lần sau ghi lại.</summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (ReadFailed)
        {
            SaveError = L.T("practice.saveError.read");
            return;
        }
        State.UpdatedAt = Now();
        bool ok;
        try { ok = await storage.WriteAsync(State.ToJson(), ct); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { ok = false; }
        Pending = !ok;
        SaveError = ok ? "" : L.T("practice.saveError.write");
    }

    /// <summary>Sau khi nạp nội dung (cần fingerprint): lần đầu có lịch ôn thì dựng lịch từ các câu đã làm.</summary>
    public void SeedSchedule()
    {
        if (State.Srs.Count > 0) return;
        foreach (var (id, r) in State.Questions)
        {
            var fp = study.Question(id)?.Fp;
            if (fp is null || State.Srs.ContainsKey(fp)) continue;
            var ok = r.FirstTry || r.ReviewedOk;
            var day = r.At is { Length: >= 10 } at ? at[..10] : Today();
            State.Srs[fp] = new SrsRecord { Box = ok ? 2 : 1, Due = ok ? Today(Interval[2]) : Today(), Last = day, Seen = r.Tries ?? 1, Wrong = r.FirstTry ? 0 : 1 };
        }
        if (State.Srs.Count > 0) Save();
    }

    public QuestionRecord? Q(string id) => State.Questions.GetValueOrDefault(id);

    public void RecordAnswer(string id, Answer chosen, bool correct)
    {
        var prev = Q(id);
        State.Questions[id] = new QuestionRecord
        {
            Tries = (prev?.Tries ?? 0) + 1,
            FirstTry = prev?.Tries is > 0 ? prev.FirstTry : correct,
            Correct = correct || prev?.Correct == true,
            LastCorrect = correct,
            ReviewedOk = prev?.ReviewedOk == true,
            Chosen = chosen,
            At = Now(),
        };
        if (study.Question(id)?.Fp is { } fp) SrsAnswer(fp, correct);
        Save();
    }

    /// <summary>Chỉ câu trả lời đầu tiên trong ngày mới đổi hộp (làm lại khi luyện thì không); sai sau đó về hộp 1.</summary>
    public void SrsAnswer(string fp, bool correct)
    {
        var day = Today();
        State.Srs.TryGetValue(fp, out var r);
        if (r?.Last == day)
        {
            if (!correct && r.Box > 1)
            {
                r.Box = 1;
                r.Due = Today(1);
            }
            return;
        }
        var box = correct ? Math.Min(5, (r?.Box ?? 1) + 1) : 1;
        State.Srs[fp] = new SrsRecord { Box = box, Due = Today(Interval[box]), Last = day, Seen = (r?.Seen ?? 0) + 1, Wrong = (r?.Wrong ?? 0) + (correct ? 0 : 1) };
        Save();
    }

    /// <summary>Câu tới hạn ôn hôm nay (mỗi fingerprint một câu), khó trước. Lọc theo môn nếu có.</summary>
    public List<Question> DueQuestions(string? courseId = null, int limit = 40)
    {
        var day = Today();
        // Câu trong bài trước (câu vừa có trong bài vừa có trong đề thì hiện kèm bài), rồi câu chỉ có trong đề.
        var exams = study.Exams.Values.Where(x => courseId is null || x.CourseId == courseId);
        var lessonQs = courseId is null
            ? study.LessonQuestions()
            : study.Entries(courseId).SelectMany(e => study.Lessons.TryGetValue(e.Id, out var l) ? l.Questions ?? [] : []);
        var byFp = new Dictionary<string, Question>();
        foreach (var q in lessonQs.Concat(exams.SelectMany(x => x.Questions ?? [])))
            if (q.Fp is { } fp) byFp.TryAdd(fp, q);
        return byFp
            .Select(kv => (Q: kv.Value, R: State.Srs.GetValueOrDefault(kv.Key)))
            .Where(x => x.R != null && string.CompareOrdinal(x.R.Due, day) <= 0)
            .OrderBy(x => x.R!.Box).ThenBy(x => x.R!.Due, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => x.Q)
            .ToList();
    }

    public void RecordTime(string? fp, double seconds)
    {
        if (fp is null || seconds <= 0 || seconds > 3600) return;
        State.Times[fp] = Math.Floor(seconds + 0.5);   // Math.round của JS
        Save();
    }

    /// <summary>Ghi chú còn nội dung (chữ hoặc cờ); ghi chú đã xóa chỉ giữ làm mốc khi gộp.</summary>
    public List<KeyValuePair<string, NoteRecord>> ActiveNotes() =>
        State.Notes.Where(kv => !string.IsNullOrWhiteSpace(kv.Value.Text) || kv.Value.Flag == true).ToList();

    public NoteRecord? Note(string? fp) => fp is null ? null : State.Notes.GetValueOrDefault(fp);

    /// <summary>Sửa ghi chú (null là giữ nguyên). Ghi chú xóa vẫn để lại bản rỗng có giờ mới, để gộp với bản cũ không làm nó hiện lại.</summary>
    public void SetNote(string? fp, string? text = null, bool? flag = null)
    {
        if (fp is null) return;
        var old = Note(fp);
        State.Notes[fp] = new NoteRecord { Text = text ?? old?.Text, Flag = flag ?? old?.Flag, At = Now() };
        Save();
    }

    /// <summary>Gói cập nhật làm đổi id câu: chuyển kết quả từ id cũ sang id mới. oldIds là mọi id gói từng có.</summary>
    public int MigrateIds(IReadOnlyDictionary<string, string> map, IEnumerable<string>? oldIds = null)
    {
        // Chuyển từ bản chụp: id có thể đổi chéo (q1 cũ thành q2 trong khi câu mới nhận q1).
        var snap = new Dictionary<string, QuestionRecord>(State.Questions);
        var targets = map.Values.ToHashSet();
        var moved = 0;
        foreach (var (from, to) in map)
        {
            if (from == to || !snap.TryGetValue(from, out var rec)) continue;
            State.Questions[to] = rec;
            if (!targets.Contains(from)) State.Questions.Remove(from);
            moved++;
        }
        // Id cũ không câu nào giữ (đã xóa, hoặc id nay thuộc câu khác): bỏ kết quả.
        foreach (var id in oldIds ?? [])
            if (!targets.Contains(id) && snap.TryGetValue(id, out var s) && State.Questions.TryGetValue(id, out var now) && ReferenceEquals(now, s))
            {
                State.Questions.Remove(id);
                moved++;
            }
        if (moved > 0) Save();
        return moved;
    }

    public void OpenLesson(string id)
    {
        State.Lessons[id] = new LessonVisit(Now());
        Save();
    }

    public void AddExamAttempt(ExamAttempt a)
    {
        State.Exams.Add(a);
        Save();
    }

    /// <summary>Câu cần ôn: sai ở lần đầu và chưa làm lại đúng trong mục Ôn câu sai.</summary>
    public bool NeedsReview(string id) => Q(id) is { FirstTry: false, ReviewedOk: false };

    public void MarkReviewed(string id, bool ok)
    {
        if (Q(id) is { } r && ok)
        {
            r.ReviewedOk = true;
            Save();
        }
    }

    public LessonStatus Status(string lessonId)
    {
        if (!study.Lessons.TryGetValue(lessonId, out var l)) return LessonStatus.NotWritten;
        var qs = l.Questions ?? [];
        if (qs.Count == 0) return State.Lessons.ContainsKey(lessonId) ? LessonStatus.Done : LessonStatus.NotStarted;
        if (!qs.Any(q => Q(q.Id) != null)) return LessonStatus.NotStarted;
        return qs.All(q => Q(q.Id)?.Correct == true) ? LessonStatus.Done : LessonStatus.InProgress;
    }

    public int ReviewCount() => study.LessonQuestions().Count(q => NeedsReview(q.Id));

    public CourseStats CourseStats(string courseId)
    {
        var es = study.Entries(courseId);
        var recs = es.SelectMany(e => study.Lessons.TryGetValue(e.Id, out var l) ? l.Questions ?? [] : []).Select(q => Q(q.Id)).OfType<QuestionRecord>().ToList();
        return new CourseStats(es.Count, es.Count(e => study.Lessons.ContainsKey(e.Id)), es.Count(e => Status(e.Id) == LessonStatus.Done),
            recs.Count, recs.Count(r => r.FirstTry));
    }

    /// <summary>Bài kế tiếp: theo thứ tự ưu tiên môn, bài đầu tiên đã soạn mà chưa xong.</summary>
    public Entry? NextLesson()
    {
        var order = study.Manifest.Priority ?? study.Manifest.Courses.Select(c => c.Id).ToList();
        foreach (var cid in order)
            if (study.Entries(cid).FirstOrDefault(x => study.Lessons.ContainsKey(x.Id) && Status(x.Id) != LessonStatus.Done) is { } e) return e;
        return null;
    }
}
