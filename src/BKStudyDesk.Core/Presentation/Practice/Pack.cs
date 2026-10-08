using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SoHocTap.Presentation.Practice;

// Study Pack v1 (port pack.ts): gói luyện tập dùng chung, JSON thuần, spec ở studypack/SPEC.md.

public sealed record PackSource(string Title, string? Pages = null, string? Url = null);

public sealed class PackQuestion
{
    public string? Id { get; set; }
    /// <summary>single (mặc định), multi, truefalse, numeric, short.</summary>
    public string? Type { get; set; }
    public string? Tag { get; set; }
    public string Prompt { get; set; } = "";
    public List<string>? Options { get; set; }
    /// <summary>single: chỉ số đúng; truefalse: true, false; numeric: đáp số.</summary>
    public JsonNode? Answer { get; set; }
    public int[]? Answers { get; set; }
    public double? Tolerance { get; set; }
    public string? Unit { get; set; }
    public string[]? Accept { get; set; }
    public bool? KeepOrder { get; set; }
    public string? Group { get; set; }
    public string Solution { get; set; } = "";
    public int? Difficulty { get; set; }
    public string[]? Skills { get; set; }
    public PackSource? Source { get; set; }

    // JsonValue tạo từ int (bộ đọc Markdown) không đọc được bằng TryGetValue<double>: thử từng kiểu số.
    [System.Text.Json.Serialization.JsonIgnore]
    public double? AnswerNumber => Answer is not JsonValue v || v.GetValueKind() != JsonValueKind.Number ? null
        : v.TryGetValue<double>(out var d) ? d : v.TryGetValue<int>(out var i) ? i : v.TryGetValue<long>(out var l) ? l : v.TryGetValue<JsonElement>(out var e) ? e.GetDouble() : null;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool? AnswerBool => Answer is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;
}

public sealed record PackSection(string? Title, string Body);

public sealed class PackLesson
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public List<PackSource>? Sources { get; set; }
    public List<PackSection>? Sections { get; set; }
    public List<PackQuestion>? Questions { get; set; }
}

public sealed class PackExam
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public double Minutes { get; set; }
    public Scoring? Scoring { get; set; }
    public List<string>? QuestionRefs { get; set; }
    public List<PackQuestion>? Questions { get; set; }
}

public sealed record PackCourse(string Code, string Name, string? School = null);
public sealed record PackAuthor(string Name, string? Contact = null);
public sealed record PackVerified(string Method, string? By = null, string? At = null);
public sealed record PackSettings(bool? ShuffleQuestions = null, bool? ShuffleOptions = null);

public sealed class PackUnit
{
    public string Title { get; set; } = "";
    public List<PackLesson> Lessons { get; set; } = [];
}

public sealed partial class StudyPack
{
    public const string FormatId = "studypack/1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Cách ghi JSON của gói (camelCase, bỏ trường null), dùng chung cho mọi phần của gói.</summary>
    public static JsonSerializerOptions JsonOptions => Json;

    public string Format { get; set; } = FormatId;
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Version { get; set; } = "";
    public string? Language { get; set; }
    public PackCourse Course { get; set; } = new("", "");
    public string? Description { get; set; }
    public List<PackAuthor> Authors { get; set; } = [];
    public string? License { get; set; }
    public string? CreatedWith { get; set; }
    public PackVerified? Verified { get; set; }
    public List<PackSource>? Sources { get; set; }
    /// <summary>Xáo câu, xáo phương án mỗi lần làm (người học vẫn tắt được trong app).</summary>
    public PackSettings? Settings { get; set; }
    public List<PackUnit> Units { get; set; } = [];
    public List<PackExam>? Exams { get; set; }

    /// <summary>Đọc gói từ JSON (chưa kiểm tra: gọi <see cref="PackValidator.Validate"/> trước khi nạp). Sai cú pháp thì null.</summary>
    public static StudyPack? Parse(string json)
    {
        try { return JsonSerializer.Deserialize<StudyPack>(json, Json); }
        catch (JsonException) { return null; }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>Thẻ HTML được phép trong nội dung (app lọc lại lần nữa khi hiển thị).</summary>
    public static readonly HashSet<string> AllowedTags =
    [
        "p", "br", "b", "strong", "i", "em", "u", "sub", "sup", "code", "pre", "span", "div", "ul", "ol", "li",
        "table", "thead", "tbody", "tr", "th", "td", "hr", "blockquote", "small", "mark", "img",
    ];

    /// <summary>Class được giữ (khớp giao diện): công thức, cảnh báo, phím bấm máy tính, mẹo, ghi chú.</summary>
    public static readonly HashSet<string> AllowedClasses = ["math", "warn", "keys", "tip", "note"];

    /// <summary>Ảnh chỉ nhận dạng nhúng sẵn (data URI raster): gói tự chứa, không tải gì từ mạng, không SVG.</summary>
    [GeneratedRegex(@"^data:image/(png|jpeg|gif|webp);base64,[A-Za-z0-9+/=\s]+$")]
    public static partial Regex ImgSrc();
}
