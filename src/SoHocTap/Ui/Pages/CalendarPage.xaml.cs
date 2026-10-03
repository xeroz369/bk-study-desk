using System.Windows;
using System.Windows.Controls;
using SoHocTap.Shell;
using SoHocTap.Ui.Controls;

namespace SoHocTap.Ui.Pages;

internal sealed record ClassRow(int Day, string DayName, string Time, int StartMin, string Name, string Code, string Room, string Lessons, string Teacher, bool InWeek);

public partial class CalendarPage : UserControl, IPage
{
    private readonly AppHost _host;
    private readonly MainWindow _main;
    private int _offset;
    private bool _ready;

    internal CalendarPage(AppHost host, MainWindow main)
    {
        InitializeComponent();
        _host = host;
        _main = main;

        var up = Upcoming.Grid;
        up.Columns.Add(Grids.Text(L.T("col.time"), nameof(TimelineItem.Hour), 56, sortPath: nameof(TimelineItem.Time)));
        up.Columns.Add(Grids.Text(L.T("col.name"), nameof(TimelineItem.Name), star: true));
        up.Columns.Add(Grids.Text(L.T("col.kind"), nameof(TimelineItem.KindName), 90));
        up.Columns.Add(Grids.Flex(L.T("col.detail"), nameof(TimelineItem.Label), 1, 100));
        up.Columns.Add(Grids.Flex(L.T("col.subject"), nameof(TimelineItem.Subject), 1, 100));
        up.Columns.Add(Grids.Right(L.T("col.left"), nameof(TimelineItem.Left), 100, nameof(TimelineItem.Time)));
        up.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        Upcoming.KeyOf = o => ((TimelineItem)o).Id;
        Grids.Setup<TimelineItem>(up, e => { if (e.Url is { } u) _host.OpenWeb(u, e.Name); }, Menu);

        var week = Week.Grid;
        week.Columns.Add(Grids.Text(L.T("col.time"), nameof(ClassRow.Time), 110, sortPath: nameof(ClassRow.StartMin)));
        week.Columns.Add(Grids.Text(L.T("col.subject"), nameof(ClassRow.Name), star: true));
        week.Columns.Add(Grids.Text(L.T("col.code"), nameof(ClassRow.Code), 90));
        week.Columns.Add(Grids.Text(L.T("col.room"), nameof(ClassRow.Room), 90));
        week.Columns.Add(Grids.Text(L.T("col.lessons"), nameof(ClassRow.Lessons), 70));
        week.Columns.Add(Grids.Flex(L.T("col.teacher"), nameof(ClassRow.Teacher), 1, 100));
        week.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        week.LoadingRow += (_, e) => e.Row.Opacity = e.Row.Item is ClassRow { InWeek: false } ? 0.5 : 1;
        Grids.Setup<ClassRow>(week, null, c => [new(L.T("common.copy"), () => Grids.Copy($"{c.Name}, {c.DayName} {c.Time}, {c.Room}", _main))]);
        Grid.Copy = b => Grids.Copy(b.Tip, _main);
        View.ItemsSource = new[] { L.T("calendar.viewGrid"), L.T("calendar.viewList") };
        View.SelectedIndex = Core.Config.Str("app.calendarView", "grid") == "list" ? 1 : 0;
        _ready = true;

        var exams = Exams.Grid;
        exams.Columns.Add(Grids.Text(L.T("col.subject"), nameof(TimelineItem.Subject), star: true));
        exams.Columns.Add(Grids.Text(L.T("col.when"), nameof(TimelineItem.When), 140, sortPath: nameof(TimelineItem.Time)));
        exams.Columns.Add(Grids.Flex(L.T("col.roomDuration"), nameof(TimelineItem.Label), 1, 120));
        exams.Columns.Add(Grids.Right(L.T("col.left"), nameof(TimelineItem.Left), 100, nameof(TimelineItem.Time)));
        exams.LoadingRow += (_, e) => e.Row.Opacity = e.Row.Item is TimelineItem t && t.Time < Format.Now ? 0.5 : 1;
        Exams.KeyOf = o => ((TimelineItem)o).Id;
        Grids.Setup<TimelineItem>(exams, null, e => [new(L.T("common.copy"), () => Grids.Copy($"{e.Name}, {e.When}, {e.Label}", _main))]);
    }

    public string Title => L.T("nav.calendar");
    public string Subtitle => _host.State.Mybk?.Term.Name ?? "";

    public void Open(string arg) => Tabs.SelectedIndex = arg switch { "tuan" => 1, "thi" => 2, _ => 0 };

    private IEnumerable<MenuEntry> Menu(TimelineItem e)
    {
        // "Mở" (mở trên web) đã có sẵn ở đầu menu do Grids.Setup thêm, không lặp lại.
        yield return new(L.T("common.copy"), () => Grids.Copy($"{e.Name}, {e.When}, {e.Label}", _main));
    }

    private void OnFilter(object sender, RoutedEventArgs e) => Refresh();
    private void OnPrevWeek(object sender, RoutedEventArgs e) { _offset--; RefreshWeek(); }
    private void OnNextWeek(object sender, RoutedEventArgs e) { _offset++; RefreshWeek(); }
    private void OnThisWeek(object sender, RoutedEventArgs e) { _offset = 0; RefreshWeek(); }

    /// <summary>
    /// Xuất thời khóa biểu cả kỳ (mỗi buổi một sự kiện) và lịch thi ra file .ics để nhập vào Google Calendar, Outlook, Lịch của Windows.
    /// Chỉ ghi file trên máy, không gửi đi đâu.
    /// </summary>
    private void OnExport(object sender, RoutedEventArgs e)
    {
        var m = _host.State.Mybk;
        if (m is null) { MessageBox.Show(Window.GetWindow(this), L.T("calendar.exportNoData"), L.T("calendar.export").TrimEnd('.'), MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var events = new List<Core.IcsEvent>();
        foreach (var c in m.Schedule.Where(c => c.Day is >= 2 and <= 8 && c.Weeks.Count > 0))
        {
            if (Core.Ics.Minutes(c.Start) is not { } s || Core.Ics.Minutes(c.End) is not { } en || en <= s) continue;
            // Kỳ vắt qua năm mới (tuần 52 rồi tuần 1): tuần đầu kỳ là tuần nhỏ nhất trong nửa sau của năm.
            var first = c.Weeks.Any(w => w >= 27) && c.Weeks.Any(w => w < 27) ? c.Weeks.Where(w => w >= 27).Min() : c.Weeks.Min();
            var year = c.Year ?? DateTime.Today.Year;
            foreach (var w in c.Weeks.Distinct())
            {
                var date = Core.Ics.ClassDate(year, first, w, c.Day);
                events.Add(new Core.IcsEvent($"cl-{c.Code}-{c.Group}-{date:yyyyMMdd}-{s}", date.AddMinutes(s), date.AddMinutes(en), c.Name, c.Room,
                    string.Join("\n", new[] { c.Code + (c.Group is { } g ? ", " + g : ""), c.Teacher ?? "" }.Where(x => x.Length > 0))));
            }
        }
        foreach (var x in m.Exams)
        {
            if (!DateTime.TryParseExact(x.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)) continue;
            var t = System.Text.RegularExpressions.Regex.Match(x.Time ?? "", @"(\d+)g(\d+)");
            if (!t.Success) continue;
            var start = d.AddHours(int.Parse(t.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).AddMinutes(int.Parse(t.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
            var type = x.Type == "GK" ? L.T("timeline.examMid") : x.Type == "CK" ? L.T("timeline.examFinal") : "";
            var title = type.Length > 0 ? L.F("timeline.examTyped", type, x.Name) : L.F("timeline.exam", x.Name);
            events.Add(new Core.IcsEvent($"ex-{x.Code}-{x.Type}-{start:yyyyMMddHHmm}", start, start.AddMinutes(x.Minutes ?? 90), title, x.Room, x.Code));
        }
        if (events.Count == 0) { MessageBox.Show(Window.GetWindow(this), L.T("calendar.exportEmpty"), L.T("calendar.export").TrimEnd('.'), MessageBoxButton.OK, MessageBoxImage.Information); return; }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = L.T("calendar.export").TrimEnd('.'),
            Filter = "iCalendar (*.ics)|*.ics",
            FileName = $"BK Study Desk - {m.Term.Name}.ics",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, Core.Ics.Build($"BK Study Desk, {m.Term.Name}", events, DateTime.UtcNow), new System.Text.UTF8Encoding(false));
            MessageBox.Show(Window.GetWindow(this), L.F("calendar.exportDone", events.Count, dlg.FileName), L.T("calendar.export").TrimEnd('.'), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            Core.Log.Warn($"Xuất lịch: {x.Message}");
            MessageBox.Show(Window.GetWindow(this), L.F("calendar.exportFailed", x.Message), L.T("calendar.export").TrimEnd('.'), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnView(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        Core.Config.Set("app.calendarView", View.SelectedIndex == 1 ? "list" : "grid");
        RefreshWeek();
    }

    public void Refresh()
    {
        var s = _host.State;
        var start = Format.Sec(DateTime.Today);
        var list = s.Timeline.Where(e => e.Time >= start && e.Time < start + 14 * 86400
                                         && (ShowClasses.IsChecked == true || e.Kind != "class")
                                         && (ShowDone.IsChecked == true || !e.Done || e.Kind == "class")).ToList();
        Upcoming.Show(Grids.Grouped(list, nameof(TimelineItem.Day), nameof(TimelineItem.Time)), L.T("calendar.upcomingEmpty"), s, Src.Lms, Src.Mybk);
        Exams.Show(s.Timeline.Where(e => e.Kind == "exam").ToList(), L.T("calendar.examsEmpty"), s, Src.Mybk);
        RefreshWeek();
    }

    private void RefreshWeek()
    {
        var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7) + _offset * 7.0);
        var week = Format.IsoWeek(monday);
        WeekText.Text = L.F("calendar.week", week, monday, monday.AddDays(6));
        ThisWeek.IsEnabled = _offset != 0;
        static int Min(string? hhmm)
        {
            var p = (hhmm ?? "").Split(':');
            return int.TryParse(p[0], out var h) ? h * 60 + (p.Length > 1 && int.TryParse(p[1], out var m) ? m : 0) : 0;
        }
        var all = _host.State.Mybk?.Schedule ?? [];
        // MyBK ghi thứ 0 (hoặc ngoài 2–8) cho môn không có giờ cố định (sinh hoạt sinh viên, thí nghiệm chưa xếp lịch): không phải một ngày.
        var slotted = all.Where(c => c.Day is >= 2 and <= 8).ToList();
        var noSlot = all.Where(c => c.Day is < 2 or > 8).Select(c => c.Name).Distinct().ToList();
        NoSlot.Text = noSlot.Count == 0 ? "" : L.F("calendar.noSlot", string.Join(", ", noSlot));
        NoSlot.Visibility = noSlot.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        var grid = View.SelectedIndex != 1;
        var thisWeek = slotted.Where(c => c.Weeks.Contains(week)).ToList();
        WeekEmpty.Text = _host.State.NoDataReason("mybk", "MyBK") ?? L.T("calendar.weekEmpty");
        WeekEmpty.Visibility = grid && thisWeek.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DimNote.Visibility = grid ? Visibility.Collapsed : Visibility.Visible;
        Grid.Visibility = grid ? Visibility.Visible : Visibility.Collapsed;
        Week.Visibility = grid ? Visibility.Collapsed : Visibility.Visible;
        if (grid)
        {
            Grid.Show(monday, thisWeek.Select(c => new WeekBlock(c.Day, Min(c.Start), Min(c.End), c.Name, $"{c.Start}-{c.End}, {c.Room}",
                string.Join("\n", new[] { c.Name, $"{Format.MybkDays.GetValueOrDefault(c.Day)} {c.Start}-{c.End}, {c.Room}", c.Teacher ?? "", c.Code + (c.Group is { } g ? ", " + g : "") }
                    .Where(x => x.Length > 0)), c.Code)));
            return;
        }
        var rows = slotted.Select(c => new ClassRow(c.Day,
            L.F("format.dateLong", Format.MybkDays.GetValueOrDefault(c.Day) ?? L.F("calendar.weekday", c.Day), monday.AddDays(c.Day - 2)),
            $"{c.Start}-{c.End}", Min(c.Start), c.Name, c.Code, c.Room, $"{c.Lesson}-{c.Lesson + c.Lessons - 1}", c.Teacher ?? "", c.Weeks.Contains(week))).ToList();
        // Sắp theo phút bắt đầu (số), không theo chữ: "10:00" không được đứng trước "7:00".
        var v = Grids.Grouped(rows, nameof(ClassRow.DayName), nameof(ClassRow.Day));
        v.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(ClassRow.StartMin), System.ComponentModel.ListSortDirection.Ascending));
        Week.Show(v, L.T("calendar.termEmpty"), _host.State, Src.Mybk);
    }
}
