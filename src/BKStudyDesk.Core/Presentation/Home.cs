using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>Một việc cần làm: ô ngày, tên, dòng phụ (môn, nhãn, giờ), còn bao lâu và trạng thái nộp; Url mở bằng trình duyệt.</summary>
public sealed record TodoItem(string Id, DateTime Day, string Title, string Sub, string Left, Urgency Level, string State, string? Url)
{
    /// <summary>Cột phải màu Danger (quá hạn, rất gấp) hay Warn (gấp): View gắn class theo hai cờ này.</summary>
    public bool IsDanger => Level is Urgency.Overdue or Urgency.Urgent;
    public bool IsSoon => Level == Urgency.Soon;

    /// <summary>Chữ tháng dưới số ngày trong ô ngày: "th 10", "Oct" (lang home.monthOf).</summary>
    public string Month => L.F("home.monthOf", Day);
}

/// <summary>Một mốc sắp tới (buổi học, thi): ô giờ bắt đầu và kết thúc, tên, dòng phụ (phòng), cột phải (Hôm nay, còn N ngày).</summary>
public sealed record AgendaItem(string Id, DateTime Day, string Start, string End, string Title, string Sub, string Right);

public sealed record NewsItem(string Title, string Sub, string Ago, string? Url);

public sealed record RegItem(string Code, string Name, string When, string Status, bool Open);

/// <summary>Toàn bộ trang Hôm nay. HasLms, HasMybk false thì View nói chưa có dữ liệu nguồn đó thay vì danh sách trống.</summary>
public sealed record HomeModel(bool HasLms, bool HasMybk, IReadOnlyList<TodoItem> Todo, IReadOnlyList<AgendaItem> Upcoming,
    IReadOnlyList<NewsItem> News, IReadOnlyList<RegItem> Registrations);

/// <summary>
/// Trang Hôm nay hiện gì, theo thứ tự nào (thiết kế: design-system/bk). Thuần, không giao diện: View chỉ vẽ HomeModel.
/// Cần làm: hạn chưa xong trong TodoDays ngày tới và hạn quá trong cửa sổ 7 ngày (TimelineItem.Overdue), quá hạn trước.
/// Sắp tới: buổi học hôm nay và ngày mai, thi trong ExamDays ngày. Thông báo: NewsDays ngày, mới nhất trước. Không cắt số mục.
/// Số ngày ở config (app.home.*), không viết cứng.
/// </summary>
public static class HomePresenter
{
    public static int TodoDays => Config.Int("app.home.todoDays", 14);
    public static int ExamDays => Config.Int("app.home.examDays", 14);
    public static int NewsDays => Config.Int("app.home.newsDays", 7);

    /// <summary>Phần MyBK trang cần (lịch học, thi, đợt đăng ký).</summary>
    public sealed record MybkPart(IReadOnlyList<MybkClass> Classes, IReadOnlyList<MybkExam> Exams, IReadOnlyList<MybkRegistration> Registrations);

    public static HomeModel Build(IReadOnlyList<TimelineItem> timeline, LmsData? lms, MybkData? mybk, long now) =>
        Build(timeline, lms is not null, lms?.Announcements ?? [], mybk is null ? null : new MybkPart(mybk.Schedule, mybk.Exams, mybk.Registration ?? []), now);

    public static HomeModel Build(IReadOnlyList<TimelineItem> timeline, bool hasLms, IReadOnlyList<LmsAnnouncement> news, MybkPart? mybk, long now)
    {
        var today = VnTime.ToWall(now).Date;
        var todo = timeline
            .Where(e => e.Kind is "assign" or "quiz" or "event" && !e.Opens && !e.Done && (e.Overdue || (e.Time >= now && e.Time <= now + TodoDays * 86400L)))
            .OrderBy(e => e.GroupRank).ThenBy(e => e.Time)
            .Select(e => new TodoItem(e.Id, VnTime.ToWall(e.Time).Date, e.Name, Sub(e), e.Left, e.Urgency, e.StateText, e.Url))
            .ToList();

        var upcoming = new List<(long Time, AgendaItem Item)>();
        foreach (var c in mybk?.Classes ?? [])
            foreach (var d in Ics.ClassDates(c.Year, c.Weeks ?? [], c.Day, today).Where(d => d == today || d == today.AddDays(1)))
                upcoming.Add((VnTime.FromWall(d.AddMinutes(VnTime.ParseClock(c.Start) ?? 0)),
                    new AgendaItem($"cl-{c.Code}-{d:yyyyMMdd}", d, c.Start, c.End, c.Name, L.F("timeline.room", c.Room),
                        L.T(d == today ? "format.group.today" : "format.group.tomorrow"))));
        foreach (var x in mybk?.Exams ?? [])
        {
            if (VnTime.ParseDate(x.Date) is not { } d || d < today || d > today.AddDays(ExamDays)) continue;
            var time = VnTime.FromWall(d.AddMinutes(VnTime.ParseClock(x.Time) ?? 0));
            var start = VnTime.ParseClock(x.Time) is { } m ? $"{m / 60:00}:{m % 60:00}" : x.Time;
            var sub = x.Minutes is { } min ? L.F("timeline.roomMinutes", x.Room, min) : L.F("timeline.room", x.Room);
            upcoming.Add((time, new AgendaItem($"ex-{x.Code}-{x.Type}", d, start, "", x.Name, sub, Due.LeftText(time, now))));
        }

        var newsItems = news.Where(a => a.Time > now - NewsDays * 86400L).OrderByDescending(a => a.Time)
            .Select(a => new NewsItem(a.Title, a.Subject, Format.Ago(a.Time), a.Url)).ToList();

        var regs = Registrations.Upcoming(mybk?.Registrations ?? [], now).Select(r =>
        {
            var open = Registrations.IsOpen(r, now);
            return new RegItem(r.Code, r.Name, open ? L.F("home.reg.closes", Format.DateTime(r.End)) : L.F("home.reg.opens", Format.DateTime(r.Start)),
                open ? L.T("home.reg.openNow") : Format.Until(r.Start), open);
        }).ToList();

        return new HomeModel(hasLms, mybk is not null, todo, [.. upcoming.OrderBy(u => u.Time).Select(u => u.Item)], newsItems, regs);
    }

    /// <summary>"Giải tích 2, Hạn nộp 23:59"; mốc không rõ giờ thì bỏ giờ.</summary>
    private static string Sub(TimelineItem e)
    {
        var hour = e.Hour;
        var label = hour.Length > 0 ? $"{e.Label} {hour}" : e.Label;
        return e.Subject.Length == 0 ? label : $"{e.Subject}, {label}";
    }
}
