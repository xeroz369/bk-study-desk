using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>
/// Một dòng của bảng Lịch: nhóm (Quá hạn hay ngày), giờ, tên, môn, loại, trạng thái nộp, còn bao lâu; Url mở bằng trình duyệt;
/// CustomId khác null: sự kiện tự thêm (bấm đúp để sửa).
/// </summary>
public sealed record CalendarRow(string Id, long Time, string Group, string Hour, string Name, string Subject, string Kind, string State, string Left,
    bool IsDanger, bool IsSoon, string? Url, string? CustomId = null);

/// <summary>
/// Tab Sắp tới của trang Lịch (cùng quy tắc với bản 1.x, Ui/Pages/CalendarPage): mọi mốc từ đầu hôm nay trở đi, không cắt theo số ngày,
/// cộng hạn LMS đã qua mà chưa làm (nhóm Quá hạn ở đầu); mốc không đọc được ngày xếp cuối. Buổi học (nhiều) và mục đã xong theo hai
/// công tắc của người dùng.
/// </summary>
public static class CalendarPresenter
{
    public static IReadOnlyList<CalendarRow> Build(IReadOnlyList<TimelineItem> timeline, long now, bool showClasses = false, bool showDone = false)
    {
        var today = VnTime.FromWall(VnTime.ToWall(now).Date);
        return [.. timeline
            .Where(e => (e.Time >= today || e.Overdue) && (showClasses || e.Kind != "class") && (showDone || !e.Done || e.Kind == "class"))
            .OrderBy(e => e.GroupRank).ThenBy(e => e.Time)
            .Select(Row)];
    }

    /// <summary>Một mốc thành một dòng bảng (dùng chung với tab Hạn nộp của trang Môn học).</summary>
    public static CalendarRow Row(TimelineItem e) =>
        new(e.Id, e.Time, e.Group, e.Hour, e.Name, e.Subject, e.KindName, e.StateText, e.Left,
            e.Urgency is Urgency.Overdue or Urgency.Urgent, e.Urgency == Urgency.Soon, e.Url, e.CustomId);
}
