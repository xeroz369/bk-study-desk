using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SoHocTap.Core;

namespace SoHocTap.Ui;

// Dữ liệu đã normalize mà Sources ghi ra data/lms.json, data/mybk.json (UI chỉ đọc lại để hiển thị, không gửi request).
// Tên field theo JSON (camelCase). Field nào lúc là số lúc là chữ thì để JsonNode.

public sealed record LmsCourse(long Id, string Name, string Subject, string Code, string? Part, string Term, string Teacher, string Url, string Folder);
public sealed record LmsEvent(string Id, long Course, string Subject, string Name, string Kind, long Time, string Label, string? Url, string? Phase = null);
public sealed record LmsAttempt(long Id, long Finished, double? Grade);
public sealed record LmsQuiz(long Id, long Course, string Subject, string Name, long? Open, long? Close, List<LmsAttempt> Attempts, string Url);
public sealed record LmsNewFile(string Name, string Path, string Subject, long At, bool? Updated);
public sealed record LmsAnnouncement(string Id, string Kind, string Subject, string Forum, string Title, string? Author, long Time, string? Url);
public sealed record LmsGradeItem(string Name, string? Kind, double? Grade, string? Text, double? Max, string? Percent, string? Feedback);
public sealed record LmsGradeBook(long Course, string Subject, string? Part, List<LmsGradeItem> Items);

public sealed record LmsData(long SyncedAt, string? User, string Term, List<LmsCourse> Courses, List<LmsEvent> Events, List<LmsQuiz> Quizzes,
    List<LmsNewFile>? NewFiles, List<LmsAnnouncement>? Announcements, List<LmsGradeBook>? Grades);

public sealed record MybkClass(string Code, string Name, string? Group, int Day, List<int> Weeks, string Start, string End, string Room, int Lesson, int Lessons, string? Teacher);
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
    public string DateText => DateTime.TryParseExact(Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
        ? d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) : Date ?? "";
    public string DaysText => Days.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
}
public sealed record MybkSocialWork(double? Days, List<MybkActivity> Activities);
public sealed record MybkFee(string Content, long Amount, long Remaining, string Due);
public sealed record MybkRegistered(string Code, string Name, string ClassGroup, string? Theory, string? Lab, string Round, string Result);
public sealed record MybkStudent(string Name, string Mssv, string? Class);
public sealed record MybkTerm(string Code, string Name);

public sealed record MybkData(long SyncedAt, MybkStudent Student, MybkTerm Term, List<MybkClass> Schedule, List<MybkExam> Exams,
    List<MybkGradeTerm> GradeTerms, List<MybkGrade> Grades, MybkCurriculum? Curriculum, List<MybkRegistration>? Registration,
    List<MybkComponents>? Components, List<MybkDecision>? Decisions, MybkSocialWork? SocialWork, List<MybkFee>? Fees, List<MybkRegistered>? Registered);

public static class DataFiles
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new LenientString() },
    };

    public static T? Read<T>(string name) where T : class
    {
        var path = Paths.DataFile(name);
        try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) : null; }
        catch (Exception e) when (e is IOException or JsonException or NotSupportedException)
        {
            Log.Warn($"Không đọc được {name}: {e.Message}");
            return null;
        }
    }

    public static string Text(this JsonNode? n) => n is JsonValue v ? v.ToString() : "";

    /// <summary>Field kiểu string mà nhận được cả số (MyBK trả về lẫn lộn).</summary>
    private sealed class LenientString : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => r.TokenType switch
        {
            JsonTokenType.String => r.GetString(),
            JsonTokenType.Number => r.TryGetInt64(out var l) ? l.ToString(System.Globalization.CultureInfo.InvariantCulture) : r.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            _ => null,
        };

        public override void Write(Utf8JsonWriter w, string v, JsonSerializerOptions o) => w.WriteStringValue(v);
    }
}
