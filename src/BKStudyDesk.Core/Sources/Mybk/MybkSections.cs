using System.Text.Json.Nodes;

namespace SoHocTap.Sources.Mybk;

public static class MybkSections
{
    public const string Schedule = "schedule", Exams = "exams", Grades = "grades", Curriculum = "curriculum", Registration = "registration",
        Registered = "registered", Fees = "fees", Social = "social", Decisions = "decisions";

    public const string RegistrationPage = "registrationPage";

    public static readonly TimeSpan BasisFresh = TimeSpan.FromHours(12);

    public static readonly string[] BasisApis = ["info", "semesters"];

    public static readonly IReadOnlyDictionary<string, (string[] Apis, TimeSpan Fresh)> All = new Dictionary<string, (string[], TimeSpan)>
    {
        [Schedule] = (["schedule"], TimeSpan.FromHours(12)),
        [Exams] = (["exams"], TimeSpan.FromHours(6)),
        [Grades] = (["gradesCourses", "gradesTerms", "components"], TimeSpan.FromHours(6)),
        [Curriculum] = (["curriculumInfo", "curriculum"], TimeSpan.FromHours(24)),
        [Registration] = ([RegistrationPage], TimeSpan.FromHours(24)),
        [Registered] = (["registered"], TimeSpan.FromHours(24)),
        [Fees] = (["fees"], TimeSpan.FromHours(24)),
        [Social] = (["socialWork"], TimeSpan.FromHours(24)),
        [Decisions] = (["decisions"], TimeSpan.FromHours(24)),
    };

    public static IReadOnlyList<string> ForPage(string route, int tab) => route switch
    {
        "hom-nay" => [Schedule, Exams, Registration],
        "lich" => [Schedule, Exams],
        "mon" => [Grades],
        "mybk" => tab switch
        {
            0 => [Grades],
            1 => [Curriculum, Grades],
            2 => [Registration],
            3 => [Schedule, Registered],
            4 => [Fees],
            5 => [Social],
            6 => [Decisions],
            8 => [Grades, Curriculum, Schedule],
            _ => [],
        },
        _ => [],
    };

    public static long? FetchedAt(JsonObject? saved, string section) =>
        saved?["sectionsAt"]?[section] is JsonValue v && v.TryGetValue<long>(out var t) ? t : null;

    public static bool Stale(JsonObject? saved, string section, long now, long staleBefore = 0) =>
        FetchedAt(saved, section) is not { } t || t < staleBefore || now - t >= (long)All[section].Fresh.TotalSeconds;

    public static List<string> Pick(IEnumerable<string> wanted, JsonObject? saved, long now, bool force,
        IReadOnlyDictionary<string, long>? failedAt = null, long retryGap = 600, long staleBefore = 0) =>
        [.. wanted.Where(All.ContainsKey).Distinct()
            .Where(s => force || Stale(saved, s, now, staleBefore))
            .Where(s => force || failedAt is null || !failedAt.TryGetValue(s, out var f) || now - f >= retryGap)];

    public static bool BasisFreshIn(JsonObject? saved, long now) =>
        saved?["student"]?["id"] is JsonValue && saved["term"]?["code"]?.ToString() is { Length: > 0 }
        && saved["basisAt"] is JsonValue b && b.TryGetValue<long>(out var at) && now - at < (long)BasisFresh.TotalSeconds;
}
