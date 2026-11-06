using System.Text.Json.Nodes;

namespace SoHocTap.Sources.Mybk;

/// <summary>
/// MyBK loads by section, only for what the user is looking at (docs/plans/2026-11-06-on-demand-sync.md, section 4). Each section
/// names the MyBK APIs (keys of sources.mybk.api) it needs and how long its saved copy counts as fresh. The fetch time of each section
/// is kept in mybk.json under "sectionsAt". Pure: no config, no I/O.
/// </summary>
public static class MybkSections
{
    public const string Schedule = "schedule", Exams = "exams", Grades = "grades", Curriculum = "curriculum", Registration = "registration",
        Registered = "registered", Fees = "fees", Social = "social", Decisions = "decisions";

    /// <summary>The registration rounds come from an HTML page, not an API (MybkSource reads it with PageAsync).</summary>
    public const string RegistrationPage = "registrationPage";

    /// <summary>Student id and current term: needed by the other calls, re-read when older than this.</summary>
    public static readonly TimeSpan BasisFresh = TimeSpan.FromHours(12);

    public static readonly string[] BasisApis = ["info", "semesters"];

    /// <summary>Section: APIs it calls, how long it stays fresh.</summary>
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

    /// <summary>
    /// Sections a page (route of MainWindow) and tab show. Pages without MyBK data give none. MyBK page tabs in order: grades, program,
    /// registration, teachers, fees, social work, decisions, services, GPA.
    /// </summary>
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

    /// <summary>When a section was last fetched (unix seconds), from mybk.json "sectionsAt"; null if never.</summary>
    public static long? FetchedAt(JsonObject? saved, string section) =>
        saved?["sectionsAt"]?[section] is JsonValue v && v.TryGetValue<long>(out var t) ? t : null;

    /// <summary>
    /// Saved copy older than the section's freshness, never fetched, or fetched before <paramref name="staleBefore"/> (the sync button
    /// marks every section stale so each reloads when next opened).
    /// </summary>
    public static bool Stale(JsonObject? saved, string section, long now, long staleBefore = 0) =>
        FetchedAt(saved, section) is not { } t || t < staleBefore || now - t >= (long)All[section].Fresh.TotalSeconds;

    /// <summary>
    /// What to fetch: with <paramref name="force"/> every wanted section (the sync button), else only stale ones. Sections that failed
    /// less than <paramref name="retryGap"/> ago are left for later so a broken API is not hit on every page view.
    /// </summary>
    public static List<string> Pick(IEnumerable<string> wanted, JsonObject? saved, long now, bool force,
        IReadOnlyDictionary<string, long>? failedAt = null, long retryGap = 600, long staleBefore = 0) =>
        [.. wanted.Where(All.ContainsKey).Distinct()
            .Where(s => force || Stale(saved, s, now, staleBefore))
            .Where(s => force || failedAt is null || !failedAt.TryGetValue(s, out var f) || now - f >= retryGap)];

    /// <summary>The student id and term in the saved copy are recent enough to call the section APIs without info and semesters.</summary>
    public static bool BasisFreshIn(JsonObject? saved, long now) =>
        saved?["student"]?["id"] is JsonValue && saved["term"]?["code"]?.ToString() is { Length: > 0 }
        && saved["basisAt"] is JsonValue b && b.TryGetValue<long>(out var at) && now - at < (long)BasisFresh.TotalSeconds;
}
