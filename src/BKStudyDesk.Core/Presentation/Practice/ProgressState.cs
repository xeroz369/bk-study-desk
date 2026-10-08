using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SoHocTap.Presentation.Practice;

// Kết quả luyện tập, đúng định dạng data/ket-qua.json mà bản 1.x (khung Svelte) đọc ghi: khóa camelCase, thời gian ISO.

public sealed class QuestionRecord
{
    public int? Tries { get; set; }
    public bool FirstTry { get; set; }
    public bool Correct { get; set; }
    public bool LastCorrect { get; set; }
    public bool ReviewedOk { get; set; }
    public Answer? Chosen { get; set; }
    public string? At { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

    public QuestionRecord Copy() => (QuestionRecord)MemberwiseClone();
}

public sealed record LessonVisit(string? OpenedAt);

public sealed class ExamAttempt
{
    public string ExamId { get; set; } = "";
    public string StartedAt { get; set; } = "";
    public double Seconds { get; set; }
    public Dictionary<string, Answer> Answers { get; set; } = [];
    public double Right { get; set; }
    public double Wrong { get; set; }
    public double Blank { get; set; }
    public double Score { get; set; }
    public double Max { get; set; }
    /// <summary>Tiêu đề giữ lại cho đề sinh ra (đề ngẫu nhiên không còn trong sổ sau khi mở lại).</summary>
    public string? Title { get; set; }
    /// <summary>Số giây làm từng câu, theo id câu.</summary>
    public Dictionary<string, double>? Times { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Lịch ôn lặp lại (Leitner) của một câu, theo fingerprint.</summary>
public sealed class SrsRecord
{
    public int Box { get; set; }
    /// <summary>Ngày ôn tiếp theo, YYYY-MM-DD (giờ máy).</summary>
    public string Due { get; set; } = "";
    /// <summary>Ngày làm gần nhất.</summary>
    public string Last { get; set; } = "";
    public int Seen { get; set; }
    public int Wrong { get; set; }
}

public sealed class NoteRecord
{
    public string? Text { get; set; }
    /// <summary>"Nghi đáp án sai".</summary>
    public bool? Flag { get; set; }
    public string At { get; set; } = "";
}

public sealed class ProgressState
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int Version { get; set; } = 1;
    public string? UpdatedAt { get; set; }
    public Dictionary<string, QuestionRecord> Questions { get; set; } = [];
    public Dictionary<string, LessonVisit> Lessons { get; set; } = [];
    public List<ExamAttempt> Exams { get; set; } = [];
    public Dictionary<string, SrsRecord> Srs { get; set; } = [];
    public Dictionary<string, NoteRecord> Notes { get; set; } = [];
    /// <summary>Số giây làm câu gần nhất, theo fingerprint.</summary>
    public Dictionary<string, double> Times { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>
    /// Đọc trạng thái do bản nào ghi cũng được (như <c>normalize</c> của bản TS): thiếu nhóm thì rỗng, nhóm sai kiểu thì bỏ,
    /// không phải object thì trạng thái rỗng. Đọc từng nhóm riêng nên một nhóm hỏng không làm mất nhóm khác.
    /// </summary>
    public static ProgressState Normalize(JsonNode? node)
    {
        var s = new ProgressState();
        if (node is not JsonObject o) return s;
        s.UpdatedAt = o["updatedAt"] is JsonValue u && u.TryGetValue<string>(out var at) ? at : null;
        s.Questions = Map<QuestionRecord>(o["questions"]);
        s.Lessons = Map<LessonVisit>(o["lessons"]);
        s.Exams = o["exams"] is JsonArray exams ? exams.Select(Item<ExamAttempt>).OfType<ExamAttempt>().ToList() : [];
        s.Srs = Map<SrsRecord>(o["srs"]);
        s.Notes = Map<NoteRecord>(o["notes"]);
        s.Times = o["times"] is JsonObject times
            ? times.Where(kv => kv.Value is JsonValue v && v.GetValueKind() == JsonValueKind.Number).ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<double>())
            : [];
        return s;
    }

    // Đọc từng bản ghi: bản ghi hỏng chỉ bỏ bản ghi đó, không kéo cả nhóm về rỗng (lần ghi sau sẽ xóa hết lịch sử).
    private static Dictionary<string, T> Map<T>(JsonNode? n) where T : class =>
        n is JsonObject o
            ? o.Select(kv => (kv.Key, Value: Item<T>(kv.Value))).Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value!)
            : [];

    private static T? Item<T>(JsonNode? n) where T : class
    {
        if (n is not JsonObject) return null;
        try { return n.Deserialize<T>(Json); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException) { return null; }
    }

    public ProgressState Clone() => Normalize(JsonNode.Parse(ToJson()));

    private static bool Before(string? a, string? b) => string.CompareOrdinal(a ?? "", b ?? "") < 0;

    /// <summary>
    /// Gộp hai bản (port <c>merge</c>): câu lấy bản mới hơn nhưng "đúng lần đầu" lấy bản cũ hơn; đề không trùng (đề, giờ bắt đầu);
    /// lịch ôn, ghi chú lấy bản chạm gần nhất; thời gian làm câu bản b thắng.
    /// </summary>
    public static ProgressState Merge(ProgressState a, ProgressState b)
    {
        var output = a.Clone();
        foreach (var (id, rb) in b.Questions)
        {
            if (!output.Questions.TryGetValue(id, out var ra))
            {
                output.Questions[id] = rb;
                continue;
            }
            if (ra.At == rb.At) continue;
            var (older, newer) = Before(ra.At, rb.At) ? (ra, rb) : (rb, ra);
            var merged = newer.Copy();
            merged.FirstTry = older.FirstTry;
            merged.Correct = ra.Correct || rb.Correct;
            merged.Tries = Math.Max(ra.Tries ?? 0, rb.Tries ?? 0);
            merged.ReviewedOk = ra.ReviewedOk || rb.ReviewedOk;
            output.Questions[id] = merged;
        }
        foreach (var (id, lb) in b.Lessons)
            if (!output.Lessons.TryGetValue(id, out var la) || Before(la.OpenedAt, lb.OpenedAt)) output.Lessons[id] = lb;
        static string Key(ExamAttempt x) => x.ExamId + "|" + x.StartedAt;
        var seen = output.Exams.Select(Key).ToHashSet();
        output.Exams.AddRange(b.Exams.Where(x => !seen.Contains(Key(x))));
        output.Exams = output.Exams.OrderBy(x => x.StartedAt, StringComparer.Ordinal).ToList();
        foreach (var (fp, r) in b.Srs)
            if (!output.Srs.TryGetValue(fp, out var have) || Before(have.Last, r.Last)) output.Srs[fp] = r;
        foreach (var (fp, r) in b.Notes)
            if (!output.Notes.TryGetValue(fp, out var have) || Before(have.At, r.At)) output.Notes[fp] = r;
        foreach (var (fp, t) in b.Times) output.Times[fp] = t;
        return output;
    }
}
