namespace SoHocTap.Core;

/// <summary>
/// Minimum freshness for reminders (user, 06/11/2026): even while the user is away, the data reminders read (LMS deadlines and quizzes,
/// MyBK timetable and exams) is refreshed at least every app.minFreshDays days (default 1, set in Settings). Uses the sessions the
/// app already has; never fills the password while the user is away. If every source that was tried turns out signed out, the user is
/// told once (notification and status bar). Pure decisions here; AppHost runs them.
/// </summary>
public static class KeepFresh
{
    /// <summary>Last refresh is older than <paramref name="days"/> (or there was none). Days below 1 count as 1.</summary>
    public static bool Due(long? last, long now, int days) => now - (last ?? 0) >= Math.Max(1, days) * 86400L;

    /// <summary>The newest of several times (full sync, light refresh); null if none.</summary>
    public static long? Latest(params long?[] times) => times.Where(t => t is > 0).Max();

    /// <summary>The oldest of several section times; null if any is missing (never fetched counts as oldest).</summary>
    public static long? Oldest(params long?[] times) => times.Any(t => t is null or <= 0) ? null : times.Min();

    /// <summary>
    /// Tell the user only when at least one source was due and every due source ended signed out (session expired or not signed in).
    /// One source still working means the reminders have something fresh; the status bar already shows the other.
    /// </summary>
    public static bool AllSignedOut(IReadOnlyCollection<string> due, IReadOnlyCollection<string> signedOut) =>
        due.Count > 0 && due.All(signedOut.Contains);
}
