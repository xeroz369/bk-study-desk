using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>Một dòng của bảng Lịch: nhóm (Quá hạn hay ngày), giờ, tên, môn, loại, trạng thái nộp, còn bao lâu; Url mở bằng trình duyệt.</summary>
public sealed record CalendarRow(string Id, long Time, string Group, string Hour, string Name, string Subject, string Kind, string State, string Left,
    bool IsDanger, bool IsSoon, string? Url);

/// <summary>
/// Bảng Lịch hiện gì (design-system/bk mục 4, Bảng): mọi mốc chưa xong từ hôm nay trở đi, không giới hạn số ngày, và hạn quá trong
/// cửa sổ 7 ngày (đứng đầu, nhóm Quá hạn). Buổi học không vào bảng này (có thời khóa biểu riêng); quiz mở không tính là hạn.
/// </summary>
public static class CalendarPresenter
{
    public static IReadOnlyList<CalendarRow> Build(IReadOnlyList<TimelineItem> timeline, long now)
    {
        var today = VnTime.FromWall(VnTime.ToWall(now).Date);
        return [.. timeline
            .Where(e => e.Kind != "class" && !e.Opens && !e.Done && !e.Undated && (e.Overdue || e.Time >= today))
            .OrderBy(e => e.GroupRank).ThenBy(e => e.Time)
            .Select(Row)];
    }

    /// <summary>Một mốc thành một dòng bảng (dùng chung với tab Hạn nộp của trang Môn học).</summary>
    public static CalendarRow Row(TimelineItem e) =>
        new(e.Id, e.Time, e.Overdue ? L.T("format.group.overdue") : e.Day, e.Hour, e.Name, e.Subject, e.KindName,
            e.StateText, e.Left, e.Urgency is Urgency.Overdue or Urgency.Urgent, e.Urgency == Urgency.Soon, e.Url);
}
