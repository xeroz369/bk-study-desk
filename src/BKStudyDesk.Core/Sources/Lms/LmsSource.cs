using System.Text.Json;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Files;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

/// <summary>
/// Source LMS, sync kiểu incremental (docs/archive/DESIGN-1.x.md mục 7):
///  1. Lấy danh sách lớp (1 request). Lớp mới thì đọc hết nội dung.
///  2. Lớp đã biết thì gọi core_course_get_updates_since; không đổi thì bỏ qua, có đổi thì chỉ xử lý mấy mục đã đổi.
///  3. Lớp kỳ trước chỉ check mỗi pastSyncDays ngày. State được ghi ngay sau từng lớp.
/// Output: data/lms.json, data/lms-files.json, data/lms-courses.json, data/lms-course/*.json, data/lms-quiz/*.json.
/// </summary>
public sealed partial class LmsSource : ISource
{
    public string Name => SourceIds.Lms;
    public TimeSpan Interval => TimeSpan.FromHours(Settings.Sync.LmsHours);

    private static string LmsFile => Paths.DataFile("lms.json");
    private static string FilesIndex => Paths.DataFile("lms-files.json");
    private static string CourseState => Paths.DataFile("lms-courses.json");
    private static string GroupsFile => Paths.DataFile("lms-groups.json");
    public static string CourseDir => Paths.DataFile("lms-course");
    public static string QuizDir => Paths.DataFile("lms-quiz");

    private static readonly HashSet<string> ContentUpdates = ["contentfiles", "configuration", "introfiles"];

    /// <summary>Hỏi ở mỗi bước đồng bộ: token và lms.json đều lấy từ cache trong RAM (chỉ đọc lại khi file đổi).</summary>
    public SourceStatus Status()
    {
        var d = LmsStore.Read();
        return new SourceStatus(HasToken, LmsStore.SyncedAt(), d?.User, d?.Term);
    }

    public JsonNode? Data() => JsonStore.Read(LmsFile);

    // ------------------------------------------------------------------ helper

    [GeneratedRegex(@"\)_(.+?)\s*\(")] private static partial Regex TeacherRx();
    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1f]")] private static partial Regex BadCharsRx();

    private static CourseInfo CourseMeta(JsonObject c)
    {
        var full = c["fullname"]!.GetValue<string>();
        var s = Organizer.SubjectOf(full)!;
        var t = TeacherRx().Match(full);
        return new CourseInfo(c["id"]!.GetValue<long>(), full, s.Subject, s.Code, s.Part, Organizer.TermOf(full),
            t.Success ? t.Groups[1].Value.Trim() : "", $"{Site}/course/view.php?id={c["id"]}");
    }

    private static string CurrentTerm(List<CourseInfo> metas)
    {
        var t = Config.Str("sources.lms.term");
        return t.Length > 0 && t != "auto" ? t : metas.Select(m => m.Term).Where(x => x.Length > 0).DefaultIfEmpty("").Max()!;
    }

    private static string SafeName(string name)
    {
        var s = BadCharsRx().Replace(name, "_").Trim().TrimEnd('.');
        return s.Length == 0 ? "_" : s.Length > 150 ? s[..150] : s;
    }
}

/// <summary>Một lớp trên LMS, đã tách tên môn / phần / học kỳ / giảng viên.</summary>
public sealed record CourseInfo(long Id, string Name, string Subject, string Code, string? Part, string Term, string Teacher, string Url)
{
    public string LmsFolder => Path.Combine(
        Part is null ? Path.Combine(Organizer.SubjectsRoot, Subject) : Path.Combine(Organizer.SubjectsRoot, Subject, Part),
        Config.Str("folders.lmsSubfolder"));

    public JsonObject ToJson() => new()
    {
        ["id"] = Id,
        ["name"] = Name,
        ["subject"] = Subject,
        ["code"] = Code,
        ["part"] = Part,
        ["term"] = Term,
        ["teacher"] = Teacher,
        ["url"] = Url,
        ["folder"] = Paths.RelativeToStudy(LmsFolder),
    };

    public static CourseInfo FromJson(JsonObject o) => new(
        o["id"]!.GetValue<long>(), o["name"]?.GetValue<string>() ?? "", o["subject"]!.GetValue<string>(), o["code"]?.GetValue<string>() ?? "",
        o["part"]?.GetValue<string>(), o["term"]?.GetValue<string>() ?? "", o["teacher"]?.GetValue<string>() ?? "", o["url"]?.GetValue<string>() ?? "");
}
