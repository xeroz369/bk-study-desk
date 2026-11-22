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

public sealed record CourseStats(int Total, int Written, int Done, int Answered, int FirstOk);

public interface IProgressStorage
{
    Task<string?> ReadAsync(CancellationToken ct);

    Task<bool> WriteAsync(string json, CancellationToken ct);
}

public sealed class PracticeProgress(StudyRegistry study, IProgressStorage storage, TimeProvider? clock = null)
{
    private static readonly int[] Interval = [0, 1, 3, 7, 14, 30];

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public TimeZoneInfo Zone => _clock.LocalTimeZone;

    public ProgressState State { get; private set; } = new();

    public string SaveError { get; private set; } = "";

    public bool Pending { get; private set; }

    public string Today(int offsetDays = 0) =>
        TimeZoneInfo.ConvertTime(_clock.GetUtcNow().AddDays(offsetDays), _clock.LocalTimeZone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private string Now() => _clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public bool ReadFailed { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await storage.ReadAsync(ct);
            var loaded = ProgressState.Normalize(json is null ? null : JsonNode.Parse(json));
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

    public void Absorb(PracticeProgress old)
    {
        if (!old.ReadFailed || !old.Pending) return;
        State = ReadFailed ? old.State : ProgressState.Merge(State, old.State);
        Save();
    }

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

    public List<Question> DueQuestions(string? courseId = null, int limit = 40)
    {
        var day = Today();
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
        State.Times[fp] = Math.Floor(seconds + 0.5);
        Save();
    }

    public List<KeyValuePair<string, NoteRecord>> ActiveNotes() =>
        State.Notes.Where(kv => !string.IsNullOrWhiteSpace(kv.Value.Text) || kv.Value.Flag == true).ToList();

    public NoteRecord? Note(string? fp) => fp is null ? null : State.Notes.GetValueOrDefault(fp);

    public void SetNote(string? fp, string? text = null, bool? flag = null)
    {
        if (fp is null) return;
        var old = Note(fp);
        State.Notes[fp] = new NoteRecord { Text = text ?? old?.Text, Flag = flag ?? old?.Flag, At = Now() };
        Save();
    }

    public int MigrateIds(IReadOnlyDictionary<string, string> map, IEnumerable<string>? oldIds = null)
    {
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
        State.Lessons[id] = new LessonVisit(Now(), Scroll(id));
        Save();
    }

    public double? Scroll(string id) => State.Lessons.GetValueOrDefault(id)?.Scroll;

    public void SetScroll(string id, double fraction)
    {
        if (State.Lessons.GetValueOrDefault(id) is not { } v) return;
        var f = Math.Round(Math.Clamp(fraction, 0, 1), 3);
        if (v.Scroll is { } old && Math.Abs(old - f) < 0.001) return;
        State.Lessons[id] = v with { Scroll = f };
        Save();
    }

    public void AddExamAttempt(ExamAttempt a)
    {
        State.Exams.Add(a);
        Save();
    }

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

    public Entry? NextLesson()
    {
        var last = State.Lessons.Where(kv => kv.Value.OpenedAt != null).OrderByDescending(kv => kv.Value.OpenedAt, StringComparer.Ordinal)
            .Select(kv => kv.Key).FirstOrDefault();
        if (last != null && study.Lessons.ContainsKey(last) && Status(last) != LessonStatus.Done && study.Entry(last) is { } recent) return recent;
        var order = study.Manifest.Priority ?? study.Manifest.Courses.Select(c => c.Id).ToList();
        foreach (var cid in order)
            if (study.Entries(cid).FirstOrDefault(x => study.Lessons.ContainsKey(x.Id) && Status(x.Id) != LessonStatus.Done) is { } e) return e;
        return null;
    }
}
