using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SoHocTap.Shell;

namespace SoHocTap.Ui.Pages;

internal sealed record Stat(string Label, string Value, Brush? Brush);
internal sealed record NewsRow(string Title, string Meta, string Url);

public partial class HomePage : UserControl, IPage
{
    private readonly AppHost _host;
    private readonly MainWindow _main;

    internal HomePage(AppHost host, MainWindow main)
    {
        InitializeComponent();
        _host = host;
        _main = main;
        // Lộ trình ôn là một trang trong content\pages (bản riêng); bản public chưa có thì ẩn nút.
        if (!File.Exists(Path.Combine(Core.Paths.Content, "pages", "lo-trinh.js"))) PlanButton.Visibility = Visibility.Collapsed;
        Agenda.Columns.Add(Grids.Text(L.T("col.time"), nameof(TimelineItem.Hour), 56, sortPath: nameof(TimelineItem.Time)));
        Agenda.Columns.Add(Grids.Text(L.T("col.name"), nameof(TimelineItem.Name), star: true));
        Agenda.Columns.Add(Grids.Text(L.T("col.kind"), nameof(TimelineItem.KindName), 80));
        Agenda.Columns.Add(Grids.Text(L.T("col.subject"), nameof(TimelineItem.Subject), 190));
        Agenda.Columns.Add(Grids.Right(L.T("col.left"), nameof(TimelineItem.Left), 90, nameof(TimelineItem.Time)));
        Agenda.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        Grids.Setup<TimelineItem>(Agenda, OpenItem, ItemMenu);

        Exams.Columns.Add(Grids.Text(L.T("col.subject"), nameof(TimelineItem.Subject), star: true));
        Exams.Columns.Add(Grids.Right(L.T("col.left"), nameof(TimelineItem.Left), 100));
        Grids.Setup<TimelineItem>(Exams, _ => _main.Go("lich/thi"), e => [new(L.T("common.copy"), () => Grids.Copy($"{e.Name} · {e.When} · {e.Label}", _main))]);

        News.Columns.Add(Grids.Text(L.T("col.title"), nameof(NewsRow.Title), star: true));
        News.Columns.Add(Grids.Right(L.T("col.at"), nameof(NewsRow.Meta), 90));
        Grids.Setup<NewsRow>(News, n => _host.OpenWeb(n.Url, n.Title), n => [new(L.T("common.copyTitle"), () => Grids.Copy(n.Title, _main), Separator: true)]);
    }

    private void OnPlan(object sender, RoutedEventArgs e) => _main.Go("luyen-tap/trang/lo-trinh");

    public string Title => L.T("nav.today");
    public string Subtitle => Format.DateLong(DateTime.Today);

    private void OpenItem(TimelineItem e)
    {
        if (e.Url is { } u) _host.OpenWeb(u, e.Name);
        else _main.Go("lich");
    }

    private IEnumerable<MenuEntry> ItemMenu(TimelineItem e)
    {
        if (e.Url is { } u) yield return new(L.T("common.openWeb"), () => _host.OpenWeb(u, e.Name));
        if (e.Subject.Length > 0) yield return new(L.T("home.subjectPage"), () => _main.Go("mon/" + e.Subject));
        yield return new(L.T("common.copy"), () => Grids.Copy($"{e.Name} · {e.When}", _main), Separator: true);
    }

    public void Refresh()
    {
        var s = _host.State;
        var now = Format.Now;
        var exam = s.Timeline.FirstOrDefault(e => e.Kind == "exam" && e.Time > now);
        var todo7 = s.Upcoming(24 * 7).Count(e => !e.Done && e.Kind is "assign" or "event");
        var quiz14 = s.Upcoming(24 * 14).Count(e => e.Kind == "quiz" && !e.Done && !e.Opens);
        var news = (s.Lms?.Announcements ?? []).Where(a => a.Time > now - 7 * 86400).ToList();
        var warn = TryFindResource("SystemFillColorCautionBrush") as Brush;
        var bad = TryFindResource("SystemFillColorCriticalBrush") as Brush;
        var normal = TryFindResource("TextFillColorPrimaryBrush") as Brush;
        var examDays = exam is null ? 0 : Format.DayDiff(exam.Time);
        // Chưa có dữ liệu LMS thì ghi "—" thay vì 0: số 0 nghĩa là đã đọc và thật sự không có gì.
        string Count(int n) => s.SyncedAt("lms") is null ? "—" : n.ToString(CultureInfo.InvariantCulture);
        Stats.ItemsSource = new List<Stat>
        {
            new(exam is null ? L.T("home.nextExam") : L.F("home.nextExamOf", exam.Subject), exam is null ? "—" : examDays > 0 ? L.F("format.days", examDays) : L.T("format.group.today"),
                exam is not null && examDays <= 3 ? bad : warn),
            new(L.T("home.due7"), Count(todo7), todo7 > 0 ? warn : normal),
            new(L.T("home.quiz14"), Count(quiz14), normal),
            new(L.T("home.news7"), Count(news.Count), normal),
        };
        var today = Format.Sec(DateTime.Today);
        var week = s.Timeline.Where(e => e.Time >= today && Format.DayDiff(e.Time) <= 7 && e.Kind != "exam"
                                         && (e.Kind == "class" ? Format.DayDiff(e.Time) <= 1 : !e.Done)).ToList();
        Agenda.ItemsSource = Grids.Grouped(week, nameof(TimelineItem.Day), nameof(TimelineItem.Time));
        // Trống vì chưa lấy được dữ liệu thì nói rõ (đang tải lần đầu / lỗi / chưa đăng nhập), không ghi "không có hạn nộp".
        AgendaEmpty.Text = s.NoDataReason("lms", "LMS") ?? s.NoDataReason("mybk", "MyBK") ?? L.T("home.agendaEmpty");
        AgendaEmpty.Visibility = week.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Agenda.Visibility = week.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        var exams = s.Timeline.Where(e => e.Kind == "exam" && e.Time > now - 86400).ToList();
        Exams.ItemsSource = exams;
        ExamsEmpty.Text = s.NoDataReason("mybk", "MyBK") ?? L.T("home.examsEmpty");
        ExamsEmpty.Visibility = exams.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Exams.Visibility = exams.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        News.ItemsSource = news.OrderByDescending(a => a.Time).Take(8).Select(a => new NewsRow(a.Title, Format.Ago(a.Time), a.Url ?? "")).ToList();
        NewsEmpty.Text = s.NoDataReason("lms", "LMS") ?? L.T("home.newsEmpty");
        NewsEmpty.Visibility = news.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
