using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SoHocTap.Shell;
using SoHocTap.Ui.Controls;

namespace SoHocTap.Ui.Pages;

/// <summary>Mức màu của số liệu: XAML đổi sang brush của theme (DynamicResource), không giữ brush cố định.</summary>
internal enum Tone { Normal, Warn, Bad }
internal sealed record Stat(string Label, string Value, Tone Tone)
{
    public override string ToString() => $"{Label}: {Value}";   // tên cho screen reader đọc
}
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
        var agenda = Agenda.Grid;
        agenda.Columns.Add(Grids.Text(L.T("col.time"), nameof(TimelineItem.Hour), 56, sortPath: nameof(TimelineItem.Time)));
        agenda.Columns.Add(Grids.Text(L.T("col.name"), nameof(TimelineItem.Name), star: true));
        agenda.Columns.Add(Grids.Text(L.T("col.kind"), nameof(TimelineItem.KindName), 80));
        agenda.Columns.Add(Grids.Flex(L.T("col.subject"), nameof(TimelineItem.Subject), 1, 90));
        agenda.Columns.Add(Grids.Right(L.T("col.left"), nameof(TimelineItem.Left), 90, nameof(TimelineItem.Time)));
        agenda.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        Agenda.KeyOf = o => ((TimelineItem)o).Id;
        Grids.Setup<TimelineItem>(agenda, OpenItem, ItemMenu);

        var exams = Exams.Grid;
        exams.HeadersVisibility = DataGridHeadersVisibility.None;
        // Bảng hẹp: tên môn co lại trước, cột "còn" giữ đủ chữ.
        exams.Columns.Add(Grids.Text(L.T("col.subject"), nameof(TimelineItem.Subject), star: true));
        exams.Columns.Add(Grids.Right(L.T("col.left"), nameof(TimelineItem.Left), 100));
        exams.Columns[0].MinWidth = 80;
        Exams.KeyOf = o => ((TimelineItem)o).Id;
        Grids.Setup<TimelineItem>(exams, _ => _main.Go("lich/thi"), e => [new(L.T("common.copy"), () => Grids.Copy($"{e.Name}, {e.When}, {e.Label}", _main))]);

        var news = News.Grid;
        news.HeadersVisibility = DataGridHeadersVisibility.None;
        news.Columns.Add(Grids.Text(L.T("col.title"), nameof(NewsRow.Title), star: true));
        news.Columns.Add(Grids.Right(L.T("col.at"), nameof(NewsRow.Meta), 90));
        news.Columns[0].MinWidth = 80;
        Grids.Setup<NewsRow>(news, n => _host.OpenWeb(n.Url, n.Title), n => [new(L.T("common.copyTitle"), () => Grids.Copy(n.Title, _main), Separator: true)]);
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
        yield return new(L.T("common.copy"), () => Grids.Copy($"{e.Name}, {e.When}", _main), Separator: true);
    }

    public void Refresh()
    {
        var s = _host.State;
        var now = Format.Now;
        var exam = s.Timeline.FirstOrDefault(e => e.Kind == "exam" && e.Time > now);
        var todo7 = s.Upcoming(24 * 7).Count(e => !e.Done && e.Kind is "assign" or "event");
        var quiz14 = s.Upcoming(24 * 14).Count(e => e.Kind == "quiz" && !e.Done && !e.Opens);
        var news = (s.Lms?.Announcements ?? []).Where(a => a.Time > now - 7 * 86400).ToList();
        var examDays = exam is null ? 0 : Format.DayDiff(exam.Time);
        // Chưa có dữ liệu LMS thì ghi "—" thay vì 0: số 0 nghĩa là đã đọc và thật sự không có gì.
        string Count(int n) => s.SyncedAt("lms") is null ? "-" : n.ToString(CultureInfo.InvariantCulture);
        Stats.ItemsSource = new List<Stat>
        {
            new(exam is null ? L.T("home.nextExam") : L.F("home.nextExamOf", exam.Subject), exam is null ? "-" : examDays > 0 ? L.F("format.days", examDays) : L.T("format.group.today"),
                exam is not null && examDays <= 3 ? Tone.Bad : Tone.Warn),
            new(L.T("home.due7"), Count(todo7), todo7 > 0 ? Tone.Warn : Tone.Normal),
            new(L.T("home.quiz14"), Count(quiz14), Tone.Normal),
            new(L.T("home.news7"), Count(news.Count), Tone.Normal),
        };
        var today = Format.Sec(DateTime.Today);
        var week = s.Timeline.Where(e => e.Time >= today && Format.DayDiff(e.Time) <= 7 && e.Kind != "exam"
                                         && (e.Kind == "class" ? Format.DayDiff(e.Time) <= 1 : !e.Done)).ToList();
        // Trống vì chưa lấy được dữ liệu thì nói rõ (đang tải lần đầu / lỗi / chưa đăng nhập), không ghi "không có hạn nộp".
        Agenda.Show(Grids.Grouped(week, nameof(TimelineItem.Day), nameof(TimelineItem.Time)), L.T("home.agendaEmpty"), s, Src.Lms, Src.Mybk);
        Exams.Show(s.Timeline.Where(e => e.Kind == "exam" && e.Time > now - 86400).ToList(), L.T("home.examsEmpty"), s, Src.Mybk);
        // Đủ mọi tin trong 7 ngày, khớp số ở ô "Tin 7 ngày" (trước đây cắt còn 8 tin mà không báo, DESIGN 6b-3).
        News.Show(news.OrderByDescending(a => a.Time).Select(a => new NewsRow(a.Title, Format.Ago(a.Time), a.Url ?? "")).ToList(), L.T("home.newsEmpty"), s, Src.Lms);
    }
}
