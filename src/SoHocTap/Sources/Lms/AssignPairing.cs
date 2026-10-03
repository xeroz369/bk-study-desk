namespace SoHocTap.Sources.Lms;

/// <summary>
/// Ghép bài tập (mod_assign_get_assignments) với mốc của nó trên lịch hành động (core_calendar_get_action_events_by_timesort),
/// để mỗi bài chỉ hiện và được nhắc một lần, và suy ra bài nào đã nộp. Hàm thuần, test được.
/// </summary>
public static class AssignPairing
{
    /// <summary>Khung lịch đọc từ LMS: từ 7 ngày trước tới 120 ngày sau.</summary>
    public const long WindowBefore = 7 * 86400, WindowAfter = 120 * 86400;

    /// <summary>Moodle trả tối đa 50 mục mỗi trang, app đọc tối đa 4 trang.</summary>
    public const int PageSize = 50, MaxPages = 4;

    /// <summary>
    /// Đã đọc đủ lịch chưa: không bị cắt ở trang cuối (đọc được ít hơn MaxPages × PageSize mục) và mọi mốc bài tập đều có
    /// "instance" (thiếu thì không ghép được). Chỉ khi đọc đủ mới suy được "bài không còn mốc là đã nộp".
    /// </summary>
    public static bool CalendarComplete(int eventCount, bool assignWithoutInstance) =>
        eventCount < PageSize * MaxPages && !assignWithoutInstance;

    /// <summary>
    /// Mốc nào trên lịch là của bài này: "instance" của mốc trên LMS trường trùng cmid của bài (đo trên dữ liệu thật 03/10/2026),
    /// không phải id như tài liệu Moodle; nên ghép theo cmid trước, id để dự phòng. null = bài không có mốc trên lịch.
    /// </summary>
    public static long? MatchInstance(long assignId, long? cmid, IReadOnlySet<long> eventInstances) =>
        cmid is { } c && eventInstances.Contains(c) ? c : eventInstances.Contains(assignId) ? assignId : null;

    /// <summary>
    /// Bài không có mốc trên lịch mà hạn nằm trong khung đã đọc: Moodle gỡ mốc khi đã nộp (hoặc không phải nộp), xem
    /// mod_assign_core_calendar_provide_event_action. Chỉ suy ra khi lịch đọc đủ; hạn ngoài khung thì không biết.
    /// </summary>
    public static bool InferDone(long due, long now, bool calendarComplete) =>
        calendarComplete && due >= now - WindowBefore && due <= now + WindowAfter;
}
