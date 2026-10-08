using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SoHocTap.Data;

// Dữ liệu đã normalize mà Sources ghi ra data/lms.json, data/mybk.json (UI chỉ đọc lại để hiển thị, không gửi request).
// Tên field theo JSON (camelCase). Field nào lúc là số lúc là chữ thì để JsonNode. Đọc bằng DataJson (chịu lỗi): số dạng chữ,
// số thực ở chỗ số nguyên, null ở chỗ số đều đọc được, không làm hỏng cả file. Các record này trước nằm ở Ui/Models.cs
// (namespace SoHocTap.Ui); Ui/Models.cs có global using nên code cũ vẫn build.

public sealed record LmsCourse(long Id, string Name, string Subject, string Code, string? Part, string Term, string Teacher, string Url, string Folder);
/// <summary>Done: bài tập đã nộp (Moodle không còn mốc trên lịch hành động), không đếm và không nhắc.</summary>
public sealed record LmsEvent(string Id, long Course, string Subject, string Name, string Kind, long Time, string Label, string? Url, string? Phase = null, bool Done = false, string? State = null);
public sealed record LmsAttempt(long Id, long Finished, double? Grade);
public sealed record LmsQuiz(long Id, long Course, string Subject, string Name, long? Open, long? Close, List<LmsAttempt> Attempts, string Url);
public sealed record LmsNewFile(string Name, string Path, string Subject, long At, bool? Updated);
public sealed record LmsAnnouncement(string Id, string Kind, string Subject, string Forum, string Title, string? Author, long Time, string? Url);
public sealed record LmsGradeItem(string Name, string? Kind, double? Grade, string? Text, double? Max, string? Percent, string? Feedback);
public sealed record LmsGradeBook(long Course, string Subject, string? Part, List<LmsGradeItem> Items);

public sealed record LmsData(long SyncedAt, string? User, string Term, List<LmsCourse> Courses, List<LmsEvent> Events, List<LmsQuiz> Quizzes,
    List<LmsNewFile>? NewFiles, List<LmsAnnouncement>? Announcements, List<LmsGradeBook>? Grades)
{
    /// <summary>Danh sách thiếu (file cũ, field bị null) thành danh sách rỗng, bỏ phần tử null: UI không phải kiểm null từng chỗ.</summary>
    public LmsData Normalize() => this with
    {
        Term = Term ?? "",
        Courses = DataJson.Clean(Courses),
        Events = DataJson.Clean(Events),
        Quizzes = [.. DataJson.Clean(Quizzes).Select(q => q with { Attempts = DataJson.Clean(q.Attempts) })],
        NewFiles = NewFiles is null ? null : DataJson.Clean(NewFiles),
        Announcements = Announcements is null ? null : DataJson.Clean(Announcements),
        Grades = Grades is null ? null : [.. DataJson.Clean(Grades).Select(g => g with { Items = DataJson.Clean(g.Items) })],
    };
}

public sealed record MybkClass(string Code, string Name, string? Group, int Day, List<int> Weeks, string Start, string End, string Room, int Lesson, int Lessons, string? Teacher, int? Year = null);
public sealed record MybkExam(string Code, string Name, string Type, string Date, string Time, int? Minutes, string Room, string? Campus);
public sealed record MybkGradeTerm(string Code, string Name, string GpaTerm, string GpaAll, JsonNode? CreditsTerm, JsonNode? CreditsAll, string? Updated);
public sealed record MybkGrade(string Term, string Code, string Name, double? Credits, double? Score, string? Letter, string? Special, int Result,
    string? Group, string? Note, long? TermId, long? CourseId);
public sealed record MybkBlock(string Id, string Name, string Group, bool Required, double CreditsNeed, double CreditsDone, bool Complete, int Order);
public sealed record MybkCurriculumCourse(string Block, string Code, string Name, double? Credits, double? Score, string? Letter, string? Special,
    int Result, bool Attempted, string? Equiv);
public sealed record MybkCurriculum(string? Program, string? Faculty, string? Year, double? CreditsDone, double? CreditsNeed, JsonNode? Gpa10, JsonNode? Gpa4,
    string? Updated, List<MybkBlock> Blocks, List<MybkCurriculumCourse> Courses);
public sealed record MybkRegistration(string Code, string Name, long Start, long End);
public sealed record MybkComponentItem(string Code, string Name, double? Weight, double? Score, string? Special);
public sealed record MybkComponents(long TermId, long CourseId, string? Status, List<MybkComponentItem> Items);
public sealed record MybkDecision(string? Type, string? Term, string? Reason, string? Status, string? Date);
public sealed record MybkActivity(string Name, string? Date, double Days, bool? Confirmed, bool? Canceled)
{
    /// <summary>Ngày kiểu Việt Nam (MyBK trả yyyy-MM-dd), số ngày dùng dấu phẩy: giống các bảng khác trong app.</summary>
    public string DateText => DateTime.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
        ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : Date ?? "";
    public string DaysText => Days.ToString("0.##", CultureInfo.CurrentCulture);
}
public sealed record MybkSocialWork(double? Days, List<MybkActivity> Activities);
public sealed record MybkFee(string Content, long Amount, long Remaining, string Due);
public sealed record MybkRegistered(string Code, string Name, string ClassGroup, string? Theory, string? Lab, string Round, string Result);
public sealed record MybkStudent(string Name, string Mssv, string? Class);
public sealed record MybkTerm(string Code, string Name);

public sealed record MybkData(long SyncedAt, MybkStudent Student, MybkTerm Term, List<MybkClass> Schedule, List<MybkExam> Exams,
    List<MybkGradeTerm> GradeTerms, List<MybkGrade> Grades, MybkCurriculum? Curriculum, List<MybkRegistration>? Registration,
    List<MybkComponents>? Components, List<MybkDecision>? Decisions, MybkSocialWork? SocialWork, List<MybkFee>? Fees, List<MybkRegistered>? Registered)
{
    public MybkData Normalize() => this with
    {
        Student = Student ?? new MybkStudent("", "", null),
        Term = Term ?? new MybkTerm("", ""),
        Schedule = [.. DataJson.Clean(Schedule).Select(c => c with { Weeks = c.Weeks ?? [] })],
        Exams = DataJson.Clean(Exams),
        GradeTerms = DataJson.Clean(GradeTerms),
        Grades = DataJson.Clean(Grades),
        Curriculum = Curriculum is null ? null : Curriculum with { Blocks = DataJson.Clean(Curriculum.Blocks), Courses = DataJson.Clean(Curriculum.Courses) },
        Registration = Registration is null ? null : DataJson.Clean(Registration),
        Components = Components is null ? null : [.. DataJson.Clean(Components).Select(c => c with { Items = DataJson.Clean(c.Items) })],
        Decisions = Decisions is null ? null : DataJson.Clean(Decisions),
        SocialWork = SocialWork is null ? null : SocialWork with { Activities = DataJson.Clean(SocialWork.Activities) },
        Fees = Fees is null ? null : DataJson.Clean(Fees),
        Registered = Registered is null ? null : DataJson.Clean(Registered),
    };
}

/// <summary>
/// Đọc lms.json, mybk.json chịu lỗi. MyBK trả số lúc là số lúc là chữ, có trường null, số thực ở chỗ số nguyên; bản cũ của app
/// cũng có thể ghi thiếu trường. Đọc được thì đọc, giá trị lạ thành mặc định (0, null); chỉ JSON hỏng thật mới là lỗi.
/// </summary>
public static class DataJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new LenientString(),
            new LenientInt(), new LenientNullableInt(),
            new LenientLong(), new LenientNullableLong(),
            new LenientDouble(), new LenientNullableDouble(),
            new LenientBool(), new LenientNullableBool(),
        },
    };

    /// <summary>
    /// Parse <paramref name="json"/>; JSON hỏng hay sai kiểu ở cấp cấu trúc (mảng thay cho object…) thì ném
    /// <see cref="DataReadException"/> kèm JsonException.Path để biết hỏng ở đâu.
    /// </summary>
    public static T? Parse<T>(string json) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(json, Options); }
        catch (JsonException e) { throw new DataReadException($"JSON sai ở {e.Path ?? "?"} (dòng {e.LineNumber + 1}): {e.Message}", e); }
        catch (NotSupportedException e) { throw new DataReadException($"kiểu dữ liệu không hỗ trợ: {e.Message}", e); }
    }

    internal static List<T> Clean<T>(List<T>? list) where T : class => list is null ? [] : [.. list.Where(x => x is not null)];

    // ------------------------------------------------------------------ converter chịu lỗi

    /// <summary>Số ở token hiện tại: số, chữ chứa số ("4.5", "  7 "), true/false; còn lại (null, chữ lạ, object) là null.</summary>
    private static double? Number(ref Utf8JsonReader r)
    {
        switch (r.TokenType)
        {
            case JsonTokenType.Number: return r.GetDouble();
            case JsonTokenType.String:
                return double.TryParse(r.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? d : null;
            case JsonTokenType.True: return 1;
            case JsonTokenType.False: return 0;
            case JsonTokenType.StartObject or JsonTokenType.StartArray: r.Skip(); return null;
            default: return null;
        }
    }

    /// <summary>Số nguyên: đọc thẳng Int64 khi được (id lớn hơn 2^53 không mất chính xác), số thực thì làm tròn.</summary>
    private static long? Integer(ref Utf8JsonReader r)
    {
        if (r.TokenType == JsonTokenType.Number && r.TryGetInt64(out var l)) return l;
        if (r.TokenType == JsonTokenType.String && long.TryParse(r.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out l)) return l;
        return Number(ref r) is { } d && d >= long.MinValue && d <= long.MaxValue ? (long)Math.Round(d) : null;
    }

    private static bool? Boolean(ref Utf8JsonReader r) => r.TokenType switch
    {
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        JsonTokenType.String => r.GetString()?.Trim().ToLowerInvariant() switch { "true" or "1" => true, "false" or "0" => false, _ => null },
        _ => Number(ref r) is { } d ? d != 0 : null,
    };

    /// <summary>Field kiểu string mà nhận được cả số (MyBK trả về lẫn lộn); object/mảng thì bỏ qua.</summary>
    private sealed class LenientString : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            switch (r.TokenType)
            {
                case JsonTokenType.String: return r.GetString();
                case JsonTokenType.Number:
                    return r.TryGetInt64(out var l) ? l.ToString(CultureInfo.InvariantCulture) : r.GetDouble().ToString(CultureInfo.InvariantCulture);
                case JsonTokenType.True: return "true";
                case JsonTokenType.False: return "false";
                case JsonTokenType.StartObject or JsonTokenType.StartArray: r.Skip(); return null;
                default: return null;
            }
        }

        public override void Write(Utf8JsonWriter w, string v, JsonSerializerOptions o) => w.WriteStringValue(v);
    }

    // Kiểu không null nhận null hay giá trị lạ thì về 0/false (HandleNull để serializer gọi converter cả khi gặp null).
    private sealed class LenientInt : JsonConverter<int>
    {
        public override bool HandleNull => true;
        public override int Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Integer(ref r) is { } v && v >= int.MinValue && v <= int.MaxValue ? (int)v : 0;
        public override void Write(Utf8JsonWriter w, int v, JsonSerializerOptions o) => w.WriteNumberValue(v);
    }

    private sealed class LenientNullableInt : JsonConverter<int?>
    {
        public override bool HandleNull => true;
        public override int? Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Integer(ref r) is { } v && v >= int.MinValue && v <= int.MaxValue ? (int)v : null;
        public override void Write(Utf8JsonWriter w, int? v, JsonSerializerOptions o) { if (v is { } x) w.WriteNumberValue(x); else w.WriteNullValue(); }
    }

    private sealed class LenientLong : JsonConverter<long>
    {
        public override bool HandleNull => true;
        public override long Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Integer(ref r) ?? 0;
        public override void Write(Utf8JsonWriter w, long v, JsonSerializerOptions o) => w.WriteNumberValue(v);
    }

    private sealed class LenientNullableLong : JsonConverter<long?>
    {
        public override bool HandleNull => true;
        public override long? Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Integer(ref r);
        public override void Write(Utf8JsonWriter w, long? v, JsonSerializerOptions o) { if (v is { } x) w.WriteNumberValue(x); else w.WriteNullValue(); }
    }

    private sealed class LenientDouble : JsonConverter<double>
    {
        public override bool HandleNull => true;
        public override double Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Number(ref r) ?? 0;
        public override void Write(Utf8JsonWriter w, double v, JsonSerializerOptions o) => w.WriteNumberValue(v);
    }

    private sealed class LenientNullableDouble : JsonConverter<double?>
    {
        public override bool HandleNull => true;
        public override double? Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Number(ref r);
        public override void Write(Utf8JsonWriter w, double? v, JsonSerializerOptions o) { if (v is { } x) w.WriteNumberValue(x); else w.WriteNullValue(); }
    }

    private sealed class LenientBool : JsonConverter<bool>
    {
        public override bool HandleNull => true;
        public override bool Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Boolean(ref r) ?? false;
        public override void Write(Utf8JsonWriter w, bool v, JsonSerializerOptions o) => w.WriteBooleanValue(v);
    }

    private sealed class LenientNullableBool : JsonConverter<bool?>
    {
        public override bool HandleNull => true;
        public override bool? Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Boolean(ref r);
        public override void Write(Utf8JsonWriter w, bool? v, JsonSerializerOptions o) { if (v is { } x) w.WriteBooleanValue(x); else w.WriteNullValue(); }
    }
}
