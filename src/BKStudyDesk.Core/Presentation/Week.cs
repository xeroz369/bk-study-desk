using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>
/// Một buổi trong thời khóa biểu tuần. Group là ngày ("Thứ hai 13/10"), Order để nhóm theo thứ tự ngày; InWeek false: môn có trong kỳ
/// mà tuần này không học (View làm nhạt); CustomId khác null: sự kiện tự thêm (bấm đúp để sửa).
/// </summary>
public sealed record WeekRow(int Order, string Group, string Time, int StartMin, string Name, string Code, string Room, string Lessons, string Teacher,
    bool InWeek, string? CustomId = null);

/// <summary>Một buổi thi (MyBK) trong tab Lịch thi; Past: đã thi (View làm nhạt).</summary>
public sealed record ExamRow(string Subject, string When, long Time, string Label, string Left, bool Past);

/// <summary>
/// Tab Thời khóa biểu và Lịch thi của trang Lịch (cùng quy tắc với bản 1.x, Ui/Pages/CalendarPage, dạng danh sách): mọi môn có giờ
/// cố định của kỳ, nhóm theo ngày trong tuần, sắp theo phút bắt đầu; sự kiện tự thêm của tuần xen vào; môn không có giờ cố định ghi riêng.
/// </summary>
public static class WeekPresenter
{
    /// <summary>"Tuần 42 (13/10 - 19/10)".</summary>
    public static string Title(DateTime monday) => L.F("calendar.week", Format.IsoWeek(monday), monday, monday.AddDays(6));

    public static IReadOnlyList<WeekRow> Rows(MybkData? mybk, IEnumerable<CustomEvent> custom, DateTime monday)
    {
        var week = Format.IsoWeek(monday);
        string Day(int day) => L.F("format.dateLong", Format.MybkDays.GetValueOrDefault(day) ?? L.F("calendar.weekday", day), monday.AddDays(day - 2));
        var rows = (mybk?.Schedule ?? []).Where(c => c.Day is >= 2 and <= 8)
            .Select(c => new WeekRow(c.Day, Day(c.Day), $"{c.Start}-{c.End}", VnTime.ParseClock(c.Start) ?? 24 * 60, c.Name, c.Code, c.Room,
                $"{c.Lesson}-{c.Lesson + c.Lessons - 1}", c.Teacher ?? "", c.Weeks.Contains(week)))
            .ToList();
        foreach (var e in custom.Where(e => e.Day is { } d && d >= monday.Date && d < monday.Date.AddDays(7) && e.StartMin is not null))
        {
            var day = Format.MybkDay(e.Day!.Value);
            rows.Add(new WeekRow(day, Day(day), AppState.CustomTime(e), e.StartMin ?? 0, e.Title, L.T(e.IsMakeup ? "kind.makeup" : "kind.custom"),
                e.Location, "", e.Note, true, e.Id));
        }
        // Sắp theo phút bắt đầu (số), không theo chữ: "10:00" không được đứng trước "7:00".
        return [.. rows.OrderBy(r => r.Order).ThenBy(r => r.StartMin)];
    }

    /// <summary>Môn không đặt được lên tuần (MyBK ghi thứ ngoài 2 tới 8 hay giờ đọc không ra): ghi tên riêng, không bỏ im lặng.</summary>
    public static IReadOnlyList<string> NoSlot(MybkData? mybk) =>
        [.. (mybk?.Schedule ?? []).Where(c => c.Day is < 2 or > 8 || VnTime.ParseClock(c.Start) is null || VnTime.ParseClock(c.End) is null)
            .Select(c => c.Name).Distinct()];

    public static IReadOnlyList<ExamRow> Exams(IReadOnlyList<TimelineItem> timeline, long now) =>
        [.. timeline.Where(e => e.Kind == "exam").OrderBy(e => e.Time).Select(e => new ExamRow(e.Subject, e.When, e.Time, e.Label, e.Left, e.Time < now))];

    /// <summary>
    /// Sự kiện để xuất .ics: thời khóa biểu cả kỳ (mỗi buổi một sự kiện), lịch thi, sự kiện tự thêm. Mục không đặt được lên lịch (giờ, ngày
    /// đọc không ra) thì không xuất, trả tên ở Skipped để báo người dùng.
    /// </summary>
    public static (List<IcsEvent> Events, List<string> Skipped) Ics(MybkData? m, IEnumerable<CustomEvent> custom, DateTime today)
    {
        var events = custom.Select(x => CustomEvents.ToIcs(x, L.T("kind.makeup"))).OfType<IcsEvent>().ToList();
        var skipped = new List<string>();
        foreach (var c in (m?.Schedule ?? []).Where(c => c.Day is >= 2 and <= 8 && c.Weeks.Count > 0))
        {
            if (VnTime.ParseClock(c.Start) is not { } s || VnTime.ParseClock(c.End) is not { } en || en <= s) { skipped.Add(c.Name); continue; }
            foreach (var date in Core.Ics.ClassDates(c.Year, c.Weeks, c.Day, today))
                events.Add(new IcsEvent($"cl-{c.Code}-{c.Group}-{date:yyyyMMdd}-{s}", date.AddMinutes(s), date.AddMinutes(en), c.Name, c.Room,
                    string.Join("\n", new[] { c.Code + (c.Group is { } g ? ", " + g : ""), c.Teacher ?? "" }.Where(x => x.Length > 0))));
        }
        foreach (var x in m?.Exams ?? [])
        {
            if (VnTime.ParseDate(x.Date) is not { } d || VnTime.ParseClock(x.Time) is not { } min) { skipped.Add(L.F("timeline.exam", x.Name)); continue; }
            var start = d.AddMinutes(min);
            var type = x.Type == "GK" ? L.T("timeline.examMid") : x.Type == "CK" ? L.T("timeline.examFinal") : "";
            var title = type.Length > 0 ? L.F("timeline.examTyped", type, x.Name) : L.F("timeline.exam", x.Name);
            events.Add(new IcsEvent($"ex-{x.Code}-{x.Type}-{start:yyyyMMddHHmm}", start, start.AddMinutes(x.Minutes ?? 90), title, x.Room, x.Code));
        }
        return (events, [.. skipped.Distinct()]);
    }
}
