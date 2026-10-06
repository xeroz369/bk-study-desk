using System.Windows;
using System.Windows.Controls;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Ui.Controls;

namespace SoHocTap.Ui.Pages;

/// <summary>Một buổi học trên thẻ Lịch học: môn, phòng và giờ (dòng phụ), cột phải ghi Hôm nay hay Ngày mai kèm giờ bắt đầu.</summary>
internal sealed record HomeClassRow(string Id, string Name, string Room, string When)
{
    public string Text => $"{Name}, {Room}, {When}";
}
internal sealed record NewsRow(string Title, string Meta, string Url);

/// <summary>Một đợt đăng ký môn trên trang chủ: tên đợt kèm dòng giờ mở hay đóng (đang mở thì ghi giờ đóng), cột phải ghi ngắn.</summary>
internal sealed record RegRow(string Code, string Name, string When, string Left)
{
    public string Text => $"{Name}, {When}";
}

public partial class HomePage : UserControl, IPage
{
    private readonly AppHost _host;
    private readonly MainWindow _main;
    private readonly TimelineList Agenda;

    internal HomePage(AppHost host, MainWindow main)
    {
        InitializeComponent();
        _host = host;
        _main = main;
        Agenda = new TimelineList(TimelineView.Agenda, host, main);
        AgendaHost.Child = Agenda;

        var classes = Classes.Grid;
        classes.HeadersVisibility = DataGridHeadersVisibility.None;
        classes.Columns.Add(Grids.TwoLine(L.T("col.subject"), nameof(HomeClassRow.Name), nameof(HomeClassRow.Room), nameof(HomeClassRow.Text)));
        classes.Columns.Add(Grids.Right(L.T("col.time"), nameof(HomeClassRow.When), 110));
        Classes.KeyOf = o => ((HomeClassRow)o).Id;
        Grids.Setup<HomeClassRow>(classes, _ => _main.Go(Routes.CalendarWeek), c => [new(L.T("common.copy"), () => Grids.Copy(c.Text, _main))]);

        // Lịch thi: môn ở trên, ngày giờ và phòng thi ở dưới (thứ cần nhất vào ngày thi), cột phải ghi còn bao lâu.
        var exams = Exams.Grid;
        exams.HeadersVisibility = DataGridHeadersVisibility.None;
        exams.Columns.Add(Grids.TwoLine(L.T("col.subject"), nameof(TimelineItem.Subject), nameof(TimelineItem.WhenAndLabel), nameof(TimelineItem.Spoken)));
        exams.Columns.Add(Grids.Right(L.T("col.left"), nameof(TimelineItem.Left), 100));
        Exams.KeyOf = o => ((TimelineItem)o).Id;
        Grids.Setup<TimelineItem>(exams, _ => _main.Go(Routes.CalendarExams), e => [new(L.T("common.copy"), () => Grids.Copy($"{e.Name}, {e.When}, {e.Label}", _main))]);

        var regs = Regs.Grid;
        regs.HeadersVisibility = DataGridHeadersVisibility.None;
        // Cột phải chỉ ghi ngắn (Đang mở, còn 12 ngày); ngày giờ là dòng phụ dưới tên để không bị cắt ở thẻ hẹp.
        regs.Columns.Add(Grids.TwoLine(L.T("col.name"), nameof(RegRow.Name), nameof(RegRow.When), nameof(RegRow.Text)));
        regs.Columns.Add(Grids.Right(L.T("col.left"), nameof(RegRow.Left), 100));
        Regs.KeyOf = o => ((RegRow)o).Code;
        Grids.Setup<RegRow>(regs, OpenRegistration, r => [new(L.T("common.copy"), () => Grids.Copy(r.Text, _main))]);

        var news = News.Grid;
        news.HeadersVisibility = DataGridHeadersVisibility.None;
        news.Columns.Add(Grids.Text(L.T("col.title"), nameof(NewsRow.Title), star: true));
        news.Columns.Add(Grids.Right(L.T("col.at"), nameof(NewsRow.Meta), 90));
        news.Columns[0].MinWidth = 80;
        Grids.Setup<NewsRow>(news, n => _host.OpenWeb(n.Url, n.Title), n => [new(L.T("common.copyTitle"), () => Grids.Copy(n.Title, _main), Separator: true)]);
    }

    private static RegRow RegRowOf(Data.MybkRegistration r)
    {
        var open = Data.Registrations.IsOpen(r, Format.Now);
        var when = open ? L.F("home.reg.closes", Format.DateTime(r.End)) : L.F("home.reg.opens", Format.DateTime(r.Start));
        return new RegRow(r.Code, r.Name, when, open ? L.T("home.reg.openNow") : Format.Until(r.Start));
    }

    private void OpenRegistration(RegRow r) => _host.OpenWeb(Config.Str("sources.mybk.registration"), r.Name);


    public string Title => L.T("nav.today");
    public string Subtitle => Format.DateLong(Format.Today);

    public void Refresh()
    {
        var s = _host.State;
        var now = Format.Now;
        var news = (s.Lms?.Announcements ?? []).Where(a => a.Time > now - 7 * 86400).ToList();
        var today = Format.Sec(Format.Today);
        // Việc phải làm: hạn LMS đã qua mà chưa làm không biến mất (nhóm "Quá hạn" đứng đầu, GroupRank). Buổi học ở thẻ Lịch học,
        // thi ở thẻ Lịch thi, đóng hay mở đăng ký ở thẻ Đăng ký môn: không lặp lại ở đây.
        var week = s.Timeline.Where(e => e.Overdue || (e.Time >= today && Format.DayDiff(e.Time) <= 7 && e.Kind is not ("exam" or "class" or "reg") && !e.Done)).ToList();
        // Mọi buổi học của hôm nay (kể cả buổi đã qua, như thời khóa biểu) và ngày mai, giờ VN, theo giờ bắt đầu.
        Classes.Show(s.Timeline.Where(e => e.Kind == "class" && e.Time >= today && Format.DayDiff(e.Time) <= 1).OrderBy(e => e.Time)
            .Select(e => new HomeClassRow(e.Id, e.Name, e.Label, $"{L.T(Format.DayDiff(e.Time) == 0 ? "format.group.today" : "format.group.tomorrow")} {e.Hour}".Trim())).ToList(),
            L.T("home.classesEmpty"), s, Src.Mybk);
        // Trống vì chưa lấy được dữ liệu thì nói rõ (đang tải lần đầu / lỗi / chưa đăng nhập), không ghi "không có hạn nộp".
        Agenda.Show(week, L.T("home.agendaEmpty"), s, Src.Lms, Src.Mybk);
        Exams.Show(s.Timeline.Where(e => e.Kind == "exam" && e.Time > now - 86400).ToList(), L.T("home.examsEmpty"), s, Src.Mybk);
        Regs.Show(Data.Registrations.Upcoming(s.Mybk?.Registration ?? [], now).Select(RegRowOf).ToList(), L.T("home.regEmpty"), s, Src.Mybk);
        // Đủ mọi tin trong 7 ngày, khớp số ở ô "Tin 7 ngày" (trước đây cắt còn 8 tin mà không báo, DESIGN 6b-3).
        News.Show(news.OrderByDescending(a => a.Time).Select(a => new NewsRow(a.Title, Format.Ago(a.Time), a.Url ?? "")).ToList(), L.T("home.newsEmpty"), s, Src.Lms);
    }
}
