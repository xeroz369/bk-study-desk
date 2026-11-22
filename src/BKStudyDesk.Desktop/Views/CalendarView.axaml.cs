using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Presentation;
using SoHocTap.Shell;
using SoHocTap.Sources.Mybk;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

public partial class CalendarView : UserControl, IFillPage, ITabbedPage
{
    private readonly AppState _state = null!;
    private static bool _showClasses, _showDone;
    private static int _offset;

    public CalendarView() => InitializeComponent();

    internal CalendarView(AppState state) : this()
    {
        RowMenus.Attach(Upcoming, OnUpcomingMenu);
        RowMenus.Attach(Week, OnWeekMenu);
        _state = state;
        ShowClasses.IsChecked = _showClasses;
        ShowDone.IsChecked = _showDone;
        ViewBox.ItemsSource = new[] { L.T("calendar.viewGrid"), L.T("calendar.viewList") };
        ViewBox.SelectedIndex = Config.Str("app.calendarView", "grid") == "list" ? 1 : 0;
        WeekGrid.Open = b => { if (b.CustomId is { } id) _ = EditAsync(id); };
        WeekGrid.FillMenu = (menu, b) => FillMenu(menu, b.CustomId);
        ShowUpcoming();
        ShowWeek();
        var exams = WeekPresenter.Exams(state.Timeline, Format.Now);
        Tables.Show(Exams, ExamsEmpty, exams, state.NoDataReason(SourceIds.Mybk, "MyBK") ?? L.T("calendar.examsEmpty"));
    }

    public void SelectTab(int index) => Tabs.SelectedIndex = Math.Clamp(index, 0, Tabs.ItemCount - 1);

    public int Tab { get => Tabs.SelectedIndex; set => SelectTab(value); }

    private async void OnBkalendar(object? sender, RoutedEventArgs e)
    {
        if (BkalendarExport.Text(_state.Mybk) is not { } text)
        {
            Say((_state.Mybk?.Schedule.Count > 0 ? null : NoteFor(0).Text) ?? L.T("calendar.bkalendarNoData"));
            return;
        }
        if (TopLevel.GetTopLevel(this) is not { Clipboard: { } clipboard } top) return;
        await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, text);
        await top.Launcher.LaunchUriAsync(new Uri(Config.Str("app.bkalendarUrl", "https://bkalendar.github.io/")));
        Say(L.F("calendar.bkalendarDone", text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 2));
    }

    private void Say(string text)
    {
        Note.Text = text;
        Note.IsVisible = text.Length > 0;
    }

    private void ShowUpcoming()
    {
        var rows = CalendarPresenter.Build(_state.Timeline, Format.Now, _showClasses, _showDone);
        Tables.Show(Upcoming, UpcomingEmpty, Tables.Grouped(rows, nameof(CalendarRow.Group)), _state.NoDataReason(SourceIds.Lms, "LMS") ?? L.T("calendar.upcomingEmpty"));
    }

    private void OnFilter(object? sender, RoutedEventArgs e)
    {
        _showClasses = ShowClasses.IsChecked == true;
        _showDone = ShowDone.IsChecked == true;
        ShowUpcoming();
    }

    private async void OnUpcomingOpen(object? sender, TappedEventArgs e)
    {
        if (Upcoming.SelectedItem is not CalendarRow r) return;
        if (r.CustomId is { } id) await EditAsync(id);
        else if (r.Url is { } url) await Files.Links.OpenAsync(this, url, r.Name);
    }

    private void OnUpcomingMenu(object? sender, EventArgs e) => FillMenu(sender as MenuFlyout, (Upcoming.SelectedItem as CalendarRow)?.CustomId);

    private static DateTime Monday => VnTime.Monday(VnTime.Today).AddDays(_offset * 7.0);

    private void ShowWeek()
    {
        var monday = Monday;
        WeekText.Text = WeekPresenter.Title(monday);
        var noSlot = WeekPresenter.NoSlot(_state.Mybk);
        NoSlot.Text = noSlot.Count == 0 ? "" : L.F("calendar.noSlot", string.Join(", ", noSlot));
        NoSlot.IsVisible = noSlot.Count > 0;
        var grid = ViewBox.SelectedIndex != 1;
        DimNote.IsVisible = !grid;
        if (grid)
        {
            Week.IsVisible = false;
            var blocks = WeekGridPresenter.Blocks(_state.Mybk, CustomEventsData.Store.All(), monday, _state.Courses());
            var note = NoteFor(blocks.Count);
            WeekGrid.Show(monday, blocks);
            WeekGrid.IsVisible = note.ShowGrid;
            ShowWeekNote(note.Text);
            return;
        }
        WeekGrid.IsVisible = false;
        var rows = WeekPresenter.Rows(_state.Mybk, CustomEventsData.Store.All(), monday);
        Tables.Show(Week, WeekEmpty, Tables.Grouped(rows, nameof(WeekRow.Group)), "");
        ShowWeekNote(NoteFor(rows.Count).Text);
    }

    private TimetableNote NoteFor(int shown) => WeekPresenter.Note(_state.Mybk, Settings.Sync.MybkEnabled,
        MybkSource.SignedIn && _state.Account is not (AccountNeed.Mybk or AccountNeed.Both), _state.NoDataReason(SourceIds.Mybk, "MyBK"), shown);

    private void ShowWeekNote(string? text)
    {
        WeekEmpty.Text = text ?? "";
        WeekEmpty.IsVisible = text is not null;
    }

    private void OnView(object? sender, SelectionChangedEventArgs e)
    {
        if (_state is null || ViewBox.SelectedIndex < 0) return;
        Config.Set("app.calendarView", ViewBox.SelectedIndex == 1 ? "list" : "grid");
        ShowWeek();
    }

    private void OnPrevWeek(object? sender, RoutedEventArgs e) { _offset--; ShowWeek(); }
    private void OnNextWeek(object? sender, RoutedEventArgs e) { _offset++; ShowWeek(); }
    private void OnThisWeek(object? sender, RoutedEventArgs e)
    {
        _offset = 0;
        WeekGrid.ScrollToNowNext();
        ShowWeek();
    }

    private async void OnWeekOpen(object? sender, TappedEventArgs e)
    {
        if (Week.SelectedItem is WeekRow { CustomId: { } id }) await EditAsync(id);
    }

    private void OnWeekMenu(object? sender, EventArgs e) => FillMenu(sender as MenuFlyout, (Week.SelectedItem as WeekRow)?.CustomId);

    private void FillMenu(MenuFlyout? menu, string? id)
    {
        if (menu is null) return;
        menu.Items.Clear();
        if (id is null || CustomEventsData.Store.Find(id) is not { } ev) return;
        var edit = new MenuItem { Header = L.T("events.edit") };
        edit.Click += async (_, _) => await EditAsync(id);
        var confirm = new MenuItem { Header = L.F("events.confirmDelete", ev.Title) };
        confirm.Click += (_, _) => Delete(id);
        var copy = new MenuItem { Header = L.T("common.copy") };
        copy.Click += async (_, _) => { if (TopLevel.GetTopLevel(this)?.Clipboard is { } c) await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(c, QuickEntry.Format(ev)); };
        menu.Items.Add(edit);
        menu.Items.Add(new MenuItem { Header = L.T("events.delete"), Items = { confirm } });
        menu.Items.Add(copy);
    }

    private async void OnAddEvent(object? sender, RoutedEventArgs e)
    {
        var today = VnTime.Today;
        Save(await Ask(null, today >= Monday && today < Monday.AddDays(7) ? today : Monday));
    }

    private async Task EditAsync(string id)
    {
        if (CustomEventsData.Store.Find(id) is { } ev) Save(await Ask(ev, ev.Day));
    }

    private async Task<CustomEvent?> Ask(CustomEvent? editing, DateTime? date) =>
        TopLevel.GetTopLevel(this) is Window owner ? await new EventWindow(editing, date).ShowDialog<CustomEvent?>(owner) : null;

    private void Save(CustomEvent? ev)
    {
        if (ev is null) return;
        try { CustomEventsData.Store.Save(ev); }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.Warn($"Lưu sự kiện tự thêm: {x.Message}");
            Say(L.F("events.saveFailed", x.Message));
            return;
        }
        _state.CustomEventsChanged();
    }

    private void Delete(string id)
    {
        try { CustomEventsData.Store.Remove(id); }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.Warn($"Xóa sự kiện tự thêm: {x.Message}");
            Say(L.F("events.saveFailed", x.Message));
            return;
        }
        _state.CustomEventsChanged();
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        var m = _state.Mybk;
        var (events, skipped) = WeekPresenter.Ics(m, CustomEventsData.Store.All(), VnTime.Today);
        if (events.Count == 0) { Say(L.T(m is null ? "calendar.exportNoData" : "calendar.exportEmpty")); return; }
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        var name = m is null ? AppInfo.Name : $"{AppInfo.Name} - {m.Term.Name}";
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = L.T("calendar.export").TrimEnd('.'),
            SuggestedFileName = name + ".ics",
            FileTypeChoices = [new FilePickerFileType("iCalendar") { Patterns = ["*.ics"] }],
        });
        if (file?.TryGetLocalPath() is not { } path) return;
        try
        {
            await File.WriteAllTextAsync(path, Ics.Build(m is null ? AppInfo.Name : $"{AppInfo.Name}, {m.Term.Name}", events, DateTime.UtcNow), new System.Text.UTF8Encoding(false));
            Say(L.F("calendar.exportDone", events.Count, path) + (skipped.Count > 0 ? L.F("calendar.exportSkipped", skipped.Count, string.Join(", ", skipped)) : ""));
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Xuất lịch: {x.Message}");
            Say(L.F("calendar.exportFailed", x.Message));
        }
    }
}
