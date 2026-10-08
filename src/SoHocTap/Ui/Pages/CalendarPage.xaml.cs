using System.Windows;
using System.Windows.Controls;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Shell;
using SoHocTap.Ui.Controls;

namespace SoHocTap.Ui.Pages;

/// <summary>Một dòng của Thời khóa biểu dạng danh sách. CustomId khác null: sự kiện tự thêm (cột Mã ghi Tự thêm / Học bù).</summary>
internal sealed record ClassRow(int Day, string DayName, string Time, int StartMin, string Name, string Code, string Room, string Lessons, string Teacher, bool InWeek, string? CustomId = null);

public partial class CalendarPage : UserControl, IPage
{
    private readonly AppHost _host;
    private readonly MainWindow _main;
    private readonly TimelineList Upcoming;
    private int _offset;
    private bool _ready;

    internal CalendarPage(AppHost host, MainWindow main)
    {
        InitializeComponent();
        _host = host;
        _main = main;

        Upcoming = new TimelineList(TimelineView.Upcoming, host, main) { Custom = new(EditEvent, CustomMenu) };
        UpcomingHost.Child = Upcoming;

        var week = Week.Grid;
        week.Columns.Add(Grids.Text(L.T("col.time"), nameof(ClassRow.Time), 110, sortPath: nameof(ClassRow.StartMin)));
        week.Columns.Add(Grids.Text(L.T("col.subject"), nameof(ClassRow.Name), star: true));
        week.Columns.Add(Grids.Text(L.T("col.code"), nameof(ClassRow.Code), 90));
        week.Columns.Add(Grids.Text(L.T("col.room"), nameof(ClassRow.Room), 90));
        week.Columns.Add(Grids.Text(L.T("col.lessons"), nameof(ClassRow.Lessons), 70));
        week.Columns.Add(Grids.Flex(L.T("col.teacher"), nameof(ClassRow.Teacher), 1, 100));
        week.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        week.LoadingRow += (_, e) => e.Row.Opacity = e.Row.Item is ClassRow { InWeek: false } ? 0.5 : 1;
        Grids.Setup<ClassRow>(week, null,
            c => c.CustomId is { } id ? CustomMenu(id) : [new(L.T("common.copy"), () => Grids.Copy($"{c.Name}, {c.DayName} {c.Time}, {c.Room}", _main))]);
        // Buổi học MyBK không có gì để mở; chỉ dòng sự kiện tự thêm double-click thì mở form sửa (Enter giữ hành vi cũ của bảng).
        week.MouseDoubleClick += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && ItemsControl.ContainerFromElement(week, d) is DataGridRow { Item: ClassRow { CustomId: { } id } }) EditEvent(id);
        };
        Grid.Copy = b => Grids.Copy(b.Tip, _main);
        Grid.Menu = b => b.CustomId is { } id ? CustomMenu(id) : [new(L.T("common.copy"), () => Grids.Copy(b.Tip, _main))];
        Grid.Open = b => { if (b.CustomId is { } id) EditEvent(id); };
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

    // ------------------------------------------------------------------ sự kiện tự thêm (issue #22)

    private static CustomEventStore Store => CustomEventsData.Store;

    /// <summary>Thứ hai của tuần đang xem ở tab Thời khóa biểu.</summary>
    private DateTime ShownMonday => Core.VnTime.Monday(Core.VnTime.Today).AddDays(_offset * 7.0);

    /// <summary>Sửa, Xóa, Sao chép (dòng nhập nhanh, dán lại vào ô Nhập nhanh được) cho một sự kiện tự thêm.</summary>
    private List<MenuEntry> CustomMenu(string id) => Store.Find(id) is not { } ev ? [] :
    [
        new(L.T("events.edit"), () => EditEvent(id), Primary: true),
        new(L.T("events.delete"), () => DeleteEvent(id)),
        new(L.T("common.copy"), () => Grids.Copy(QuickEntry.Format(ev), _main), Separator: true),
    ];

    private void OnAddEvent(object sender, RoutedEventArgs e)
    {
        // Ngày gợi ý: hôm nay nếu đang xem tuần này, không thì Thứ hai của tuần đang xem.
        var monday = ShownMonday;
        var today = Core.VnTime.Today;
        SaveEvent(EventWindow.Ask(Window.GetWindow(this), null, today >= monday && today < monday.AddDays(7) ? today : monday));
    }

    private void EditEvent(string id)
    {
        if (Store.Find(id) is not { } ev) { Refresh(); return; }
        SaveEvent(EventWindow.Ask(Window.GetWindow(this), ev));
    }

    private void SaveEvent(CustomEvent? ev)
    {
        if (ev is null) return;
        try { Store.Save(ev); }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Core.Log.Warn($"Lưu sự kiện tự thêm: {x.Message}");
            MessageBox.Show(Window.GetWindow(this), L.F("events.saveFailed", x.Message), L.T("events.titleNew"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        _main.Say(L.T("events.saved"));
        _host.State.CustomEventsChanged();
    }

    private void DeleteEvent(string id)
    {
        if (Store.Find(id) is not { } ev) return;
        if (MessageBox.Show(Window.GetWindow(this), L.F("events.confirmDelete", ev.Title), L.T("events.deleteTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try { Store.Remove(id); }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Core.Log.Warn($"Xóa sự kiện tự thêm: {x.Message}");
            MessageBox.Show(Window.GetWindow(this), L.F("events.saveFailed", x.Message), L.T("events.deleteTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        _main.Say(L.T("events.deleted"));
        _host.State.CustomEventsChanged();
    }

    /// <summary>Sự kiện tự thêm trong tuần bắt đầu từ <paramref name="monday"/> (ngày, giờ đọc được).</summary>
    private static List<CustomEvent> CustomInWeek(DateTime monday) =>
        [.. Store.All().Where(e => e.Day is { } d && d >= monday.Date && d < monday.Date.AddDays(7) && e.StartMin is not null)];

    private static string CustomTag(CustomEvent e) => L.T(e.IsMakeup ? "kind.makeup" : "kind.custom");

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
        // Sự kiện tự thêm cũng được xuất (cả những sự kiện đã qua, như buổi học của cả kỳ), nên chưa có MyBK vẫn xuất được.
        var custom = Store.All().Select(x => CustomEvents.ToIcs(x, L.T("kind.makeup"))).OfType<Core.IcsEvent>().ToList();
        if (m is null && custom.Count == 0) { MessageBox.Show(Window.GetWindow(this), L.T("calendar.exportNoData"), L.T("calendar.export").TrimEnd('.'), MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var events = new List<Core.IcsEvent>(custom);
        // Mục không đặt được lên lịch (giờ, ngày đọc không ra) thì không xuất, nhưng báo tên ở hộp thoại, không bỏ im lặng.
        var skipped = new List<string>();
        foreach (var c in (m?.Schedule ?? []).Where(c => c.Day is >= 2 and <= 8 && c.Weeks.Count > 0))
        {
            if (Core.VnTime.ParseClock(c.Start) is not { } s || Core.VnTime.ParseClock(c.End) is not { } en || en <= s) { skipped.Add(c.Name); continue; }
            foreach (var date in Core.Ics.ClassDates(c.Year, c.Weeks, c.Day, Core.VnTime.Today))
            {
                events.Add(new Core.IcsEvent($"cl-{c.Code}-{c.Group}-{date:yyyyMMdd}-{s}", date.AddMinutes(s), date.AddMinutes(en), c.Name, c.Room,
                    string.Join("\n", new[] { c.Code + (c.Group is { } g ? ", " + g : ""), c.Teacher ?? "" }.Where(x => x.Length > 0))));
            }
        }
        foreach (var x in m?.Exams ?? [])
        {
            if (Core.VnTime.ParseDate(x.Date) is not { } d || Core.VnTime.ParseClock(x.Time) is not { } min) { skipped.Add(L.F("timeline.exam", x.Name)); continue; }
            var start = d.AddMinutes(min);
            var type = x.Type == "GK" ? L.T("timeline.examMid") : x.Type == "CK" ? L.T("timeline.examFinal") : "";
            var title = type.Length > 0 ? L.F("timeline.examTyped", type, x.Name) : L.F("timeline.exam", x.Name);
            events.Add(new Core.IcsEvent($"ex-{x.Code}-{x.Type}-{start:yyyyMMddHHmm}", start, start.AddMinutes(x.Minutes ?? 90), title, x.Room, x.Code));
        }
        if (events.Count == 0) { MessageBox.Show(Window.GetWindow(this), L.T("calendar.exportEmpty"), L.T("calendar.export").TrimEnd('.'), MessageBoxButton.OK, MessageBoxImage.Information); return; }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = L.T("calendar.export").TrimEnd('.'),
            Filter = "iCalendar (*.ics)|*.ics",
            FileName = m is null ? "BK Study Desk.ics" : $"BK Study Desk - {m.Term.Name}.ics",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, Core.Ics.Build(m is null ? "BK Study Desk" : $"BK Study Desk, {m.Term.Name}", events, DateTime.UtcNow), new System.Text.UTF8Encoding(false));
            var done = L.F("calendar.exportDone", events.Count, dlg.FileName);
            if (skipped.Count > 0) done += L.F("calendar.exportSkipped", skipped.Count, string.Join(", ", skipped.Distinct()));
            MessageBox.Show(Window.GetWindow(this), done, L.T("calendar.export").TrimEnd('.'), MessageBoxButton.OK, MessageBoxImage.Information);
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
        // Mọi mốc từ đầu hôm nay trở đi, không cắt theo số ngày (DESIGN 6b-3), cộng hạn LMS đã qua mà chưa làm (nhóm "Quá hạn" ở đầu).
        // Buổi học cả kỳ khá nhiều nên để công tắc "Hiện buổi học" quyết định.
        var start = Format.Sec(Format.Today);
        var list = s.Timeline.Where(e => (e.Time >= start || e.Overdue)
                                         && (ShowClasses.IsChecked == true || e.Kind != "class")
                                         && (ShowDone.IsChecked == true || !e.Done || e.Kind == "class")).ToList();
        Upcoming.Show(list, L.T("calendar.upcomingEmpty"), s, Src.Lms, Src.Mybk);
        Exams.Show(s.Timeline.Where(e => e.Kind == "exam").ToList(), L.T("calendar.examsEmpty"), s, Src.Mybk);
        RefreshWeek();
    }

    private void RefreshWeek()
    {
        var monday = ShownMonday;
        var week = Format.IsoWeek(monday);
        WeekText.Text = L.F("calendar.week", week, monday, monday.AddDays(6));
        ThisWeek.IsEnabled = _offset != 0;
        // Giờ đọc chung bằng VnTime.ParseClock ("7g30", "07:30", "9g"): đọc không ra thì không đặt lên lưới mà ghi ở dòng NoSlot.
        static bool Timed(Data.MybkClass c) => Core.VnTime.ParseClock(c.Start) is not null && Core.VnTime.ParseClock(c.End) is not null;
        var all = _host.State.Mybk?.Schedule ?? [];
        // MyBK ghi thứ 0 (hoặc ngoài 2-8) cho môn không có giờ cố định (sinh hoạt sinh viên, thí nghiệm chưa xếp lịch): không phải một ngày.
        var slotted = all.Where(c => c.Day is >= 2 and <= 8).ToList();
        var noSlot = all.Where(c => c.Day is < 2 or > 8 || !Timed(c)).Select(c => c.Name).Distinct().ToList();
        NoSlot.Text = noSlot.Count == 0 ? "" : L.F("calendar.noSlot", string.Join(", ", noSlot));
        NoSlot.Visibility = noSlot.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        var grid = View.SelectedIndex != 1;
        var thisWeek = slotted.Where(c => c.Weeks.Contains(week)).ToList();
        var placeable = thisWeek.Where(Timed).ToList();
        var custom = CustomInWeek(monday);
        WeekEmpty.Text = _host.State.NoDataReason(SourceIds.Mybk, "MyBK") ?? L.T("calendar.weekEmpty");
        WeekEmpty.Visibility = grid && placeable.Count == 0 && custom.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DimNote.Visibility = grid ? Visibility.Collapsed : Visibility.Visible;
        Grid.Visibility = grid ? Visibility.Visible : Visibility.Collapsed;
        Week.Visibility = grid ? Visibility.Collapsed : Visibility.Visible;
        if (grid)
        {
            var courses = _host.State.Courses();
            var blocks = placeable.Select(c => new WeekBlock(c.Day, Core.VnTime.ParseClock(c.Start) ?? 0, Core.VnTime.ParseClock(c.End) ?? 0, c.Name, $"{c.Start}-{c.End}, {c.Room}",
                string.Join("\n", new[] { c.Name, $"{Format.MybkDays.GetValueOrDefault(c.Day)} {c.Start}-{c.End}, {c.Room}", c.Teacher ?? "", c.Code + (c.Group is { } g ? ", " + g : "") }
                    .Where(x => x.Length > 0)), c.Code)).ToList();
            foreach (var e in custom)
            {
                var day = Format.MybkDay(e.Day!.Value);
                var time = AppState.CustomTime(e);
                // Học bù có tên hoặc mã môn trong tiêu đề thì cùng màu với buổi học của môn đó (Key = mã môn).
                var key = e.IsMakeup && CustomEvents.MatchCourse(e.Title, courses) is { } m ? m.Code : "custom:" + e.Title;
                var tip = string.Join("\n", new[] { e.Title, $"{CustomTag(e)}, {Format.MybkDays.GetValueOrDefault(day)} {time}", e.Location, e.Note }.Where(x => x.Length > 0));
                blocks.Add(new WeekBlock(day, e.StartMin ?? 0, e.EndMin ?? 0, e.Title, AppState.CustomLabel(e), tip, key, CustomTag(e), e.Id));
            }
            Grid.Show(monday, blocks);
            return;
        }
        var rows = slotted.Select(c => new ClassRow(c.Day,
            L.F("format.dateLong", Format.MybkDays.GetValueOrDefault(c.Day) ?? L.F("calendar.weekday", c.Day), monday.AddDays(c.Day - 2)),
            $"{c.Start}-{c.End}", Core.VnTime.ParseClock(c.Start) ?? 24 * 60, c.Name, c.Code, c.Room, $"{c.Lesson}-{c.Lesson + c.Lessons - 1}", c.Teacher ?? "", c.Weeks.Contains(week))).ToList();
        foreach (var e in custom)
        {
            var day = Format.MybkDay(e.Day!.Value);
            rows.Add(new ClassRow(day, L.F("format.dateLong", Format.MybkDays.GetValueOrDefault(day) ?? L.F("calendar.weekday", day), e.Day.Value),
                AppState.CustomTime(e), e.StartMin ?? 0, e.Title, CustomTag(e), e.Location, "", e.Note, true, e.Id));
        }
        // Sắp theo phút bắt đầu (số), không theo chữ: "10:00" không được đứng trước "7:00".
        var v = Grids.Grouped(rows, nameof(ClassRow.DayName), nameof(ClassRow.Day));
        v.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(ClassRow.StartMin), System.ComponentModel.ListSortDirection.Ascending));
        Week.Show(v, L.T("calendar.termEmpty"), _host.State, Src.Mybk);
    }
}
