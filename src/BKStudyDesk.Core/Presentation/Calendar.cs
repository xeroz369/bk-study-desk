using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public sealed record CalendarRow(string Id, long Time, string Group, string Hour, string Name, string Subject, string Kind, string State, string Left,
    bool IsDanger, bool IsSoon, string? Url, string? CustomId = null);

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

    public static CalendarRow Row(TimelineItem e) =>
        new(e.Id, e.Time, e.Group, e.Hour, e.Name, e.Subject, e.KindName, e.StateText, e.Left,
            e.Urgency is Urgency.Overdue or Urgency.Urgent, e.Urgency == Urgency.Soon, e.Url, e.CustomId);
}
