namespace SoHocTap.Sources.Lms;

public static class AssignPairing
{
    public const long WindowBefore = 7 * 86400;

    public const int PageSize = 50, MaxPages = 10;

    public static bool CalendarComplete(int eventCount, bool assignWithoutInstance) =>
        eventCount < PageSize * MaxPages && !assignWithoutInstance;

    public static long? MatchInstance(long assignId, long? cmid, IReadOnlySet<long> eventInstances) =>
        cmid is { } c && eventInstances.Contains(c) ? c : eventInstances.Contains(assignId) ? assignId : null;

    public static bool InferDone(long due, long now, bool calendarComplete) =>
        calendarComplete && due >= now - WindowBefore;
}
