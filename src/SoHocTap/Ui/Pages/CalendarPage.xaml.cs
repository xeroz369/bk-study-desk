using System.Windows;
using System.Windows.Controls;
using SoHocTap.Shell;

namespace SoHocTap.Ui.Pages;

internal sealed record ClassRow(int Day, string DayName, string Time, int StartMin, string Name, string Code, string Room, string Lessons, string Teacher, bool InWeek);

public partial class CalendarPage : UserControl, IPage
{
    private readonly AppHost _host;
    private int _offset;

    internal CalendarPage(AppHost host)
    {
        InitializeComponent();
        _host = host;

        Upcoming.Columns.Add(Grids.Text(L.T("col.time"), nameof(TimelineItem.Hour), 56, sortPath: nameof(TimelineItem.Time)));
        Upcoming.Columns.Add(Grids.Text(L.T("col.name"), nameof(TimelineItem.Name), star: true));
        Upcoming.Columns.Add(Grids.Text(L.T("col.kind"), nameof(TimelineItem.KindName), 90));
        Upcoming.Columns.Add(Grids.Text(L.T("col.detail"), nameof(TimelineItem.Label), 200));
        Upcoming.Columns.Add(Grids.Text(L.T("col.subject"), nameof(TimelineItem.Subject), 200));
        Upcoming.Columns.Add(Grids.Right(L.T("col.left"), nameof(TimelineItem.Left), 100, nameof(TimelineItem.Time)));
        Upcoming.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        Grids.Setup<TimelineItem>(Upcoming, e => { if (e.Url is { } u) _host.OpenWeb(u, e.Name); }, Menu);

        Week.Columns.Add(Grids.Text(L.T("col.time"), nameof(ClassRow.Time), 110, sortPath: nameof(ClassRow.StartMin)));
        Week.Columns.Add(Grids.Text(L.T("col.subject"), nameof(ClassRow.Name), star: true));
        Week.Columns.Add(Grids.Text(L.T("col.code"), nameof(ClassRow.Code), 90));
        Week.Columns.Add(Grids.Text(L.T("col.room"), nameof(ClassRow.Room), 90));
        Week.Columns.Add(Grids.Text(L.T("col.lessons"), nameof(ClassRow.Lessons), 70));
        Week.Columns.Add(Grids.Text(L.T("col.teacher"), nameof(ClassRow.Teacher), 200));
        Week.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        Week.LoadingRow += (_, e) => e.Row.Opacity = e.Row.Item is ClassRow { InWeek: false } ? 0.5 : 1;
        Grids.Setup<ClassRow>(Week, null, c => [new(L.T("common.copy"), () => Grids.Copy($"{c.Name} · {c.DayName} {c.Time} · {c.Room}"))]);

        Exams.Columns.Add(Grids.Text(L.T("col.subject"), nameof(TimelineItem.Subject), star: true));
        Exams.Columns.Add(Grids.Text(L.T("col.when"), nameof(TimelineItem.When), 140, sortPath: nameof(TimelineItem.Time)));
        Exams.Columns.Add(Grids.Text(L.T("col.roomDuration"), nameof(TimelineItem.Label), 220));
        Exams.Columns.Add(Grids.Right(L.T("col.left"), nameof(TimelineItem.Left), 100, nameof(TimelineItem.Time)));
        Exams.LoadingRow += (_, e) => e.Row.Opacity = e.Row.Item is TimelineItem t && t.Time < Format.Now ? 0.5 : 1;
        Grids.Setup<TimelineItem>(Exams, null, e => [new(L.T("common.copy"), () => Grids.Copy($"{e.Name} · {e.When} · {e.Label}"))]);
    }

    public string Title => L.T("nav.calendar");
    public string Subtitle => _host.State.Mybk?.Term.Name ?? "";

    public void Open(string arg) => Tabs.SelectedIndex = arg switch { "tuan" => 1, "thi" => 2, _ => 0 };

    private IEnumerable<MenuEntry> Menu(TimelineItem e)
    {
        if (e.Url is { } u) yield return new(L.T("common.openWeb"), () => _host.OpenWeb(u, e.Name), Primary: true);
        yield return new(L.T("common.copy"), () => Grids.Copy($"{e.Name} · {e.When} · {e.Label}"), Separator: true);
    }

    private void OnFilter(object sender, RoutedEventArgs e) => Refresh();
    private void OnPrevWeek(object sender, RoutedEventArgs e) { _offset--; RefreshWeek(); }
    private void OnNextWeek(object sender, RoutedEventArgs e) { _offset++; RefreshWeek(); }
    private void OnThisWeek(object sender, RoutedEventArgs e) { _offset = 0; RefreshWeek(); }

    public void Refresh()
    {
        var s = _host.State;
        var start = Format.Sec(DateTime.Today);
        var list = s.Timeline.Where(e => e.Time >= start && e.Time < start + 14 * 86400
                                         && (ShowClasses.IsChecked == true || e.Kind != "class")
                                         && (ShowDone.IsChecked == true || !e.Done || e.Kind == "class")).ToList();
        Upcoming.ItemsSource = Grids.Grouped(list, nameof(TimelineItem.Day), nameof(TimelineItem.Time));
        Exams.ItemsSource = s.Timeline.Where(e => e.Kind == "exam").ToList();
        RefreshWeek();
    }

    private void RefreshWeek()
    {
        var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7) + _offset * 7);
        var week = Format.IsoWeek(monday);
        WeekText.Text = L.F("calendar.week", week, monday, monday.AddDays(6));
        ThisWeek.IsEnabled = _offset != 0;
        int Min(string hhmm) { var p = (hhmm ?? "0:0").Split(':'); return int.Parse(p[0]) * 60 + (p.Length > 1 ? int.Parse(p[1]) : 0); }
        var rows = (_host.State.Mybk?.Schedule ?? []).Select(c => new ClassRow(c.Day,
            L.F("format.dateLong", Format.MybkDays.GetValueOrDefault(c.Day) ?? L.F("calendar.weekday", c.Day), monday.AddDays(c.Day - 2)),
            $"{c.Start}–{c.End}", Min(c.Start), c.Name, c.Code, c.Room, $"{c.Lesson}–{c.Lesson + c.Lessons - 1}", c.Teacher ?? "", c.Weeks.Contains(week))).ToList();
        var v = Grids.Grouped(rows, nameof(ClassRow.DayName), nameof(ClassRow.Day));
        v.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(ClassRow.StartMin), System.ComponentModel.ListSortDirection.Ascending));
        Week.ItemsSource = v;
    }
}
