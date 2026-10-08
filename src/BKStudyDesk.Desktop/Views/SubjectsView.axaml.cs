using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using BKStudyDesk.Desktop.Files;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Files;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Trang Môn học: nối SubjectsPresenter vào danh sách và các tab. Tab chỉ dựng khi mở (đỡ quét thư mục); đọc đĩa chạy nền.
/// Lọc, khớp mốc, dòng phụ, điểm: BKStudyDesk.Core/Presentation/Subjects.cs.
/// </summary>
public partial class SubjectsView : UserControl, IFillPage
{
    private readonly AppState _state = null!;
    private readonly Action<LmsCourse>? _download;
    private readonly Action<string>? _openLibrary;
    private IReadOnlyList<SubjectRow> _rows = [];
    private SubjectRow? _subject;
    private string _root = "", _dir = "";
    private readonly HashSet<TabItem> _filled = [];
    private int _gen;   // lượt đọc đĩa mới nhất; kết quả của lượt cũ (đã chọn môn khác, thư mục khác) thì bỏ

    public SubjectsView() => InitializeComponent();

    /// <param name="previous">Trang cũ khi dựng lại vì dữ liệu mới: giữ môn, tab, thư mục người dùng đang xem.</param>
    /// <param name="openLibrary">Mở trang Thư viện đúng môn này (nút Xem trong Thư viện); null thì ẩn nút.</param>
    internal SubjectsView(AppState state, SubjectsView? previous, Action<LmsCourse>? download, Action<string>? openLibrary) : this()
    {
        _keepDir = previous?._dir;
        if (previous is not null) Tabs.SelectedIndex = previous.Tabs.SelectedIndex;
        _state = state;
        _download = download;
        _openLibrary = openLibrary;
        DownloadButton.IsVisible = download is not null;
        LibraryButton.IsVisible = openLibrary is not null;
        _wanted = previous?.Selected;
        Build(null);
        _ = ScanAsync();
    }

    private string? _wanted;
    private string? _keepDir;   // thư mục đang xem ở trang cũ (dùng một lần khi chọn lại đúng môn đó)

    /// <summary>Mở một tab theo thứ tự (tham số --tab=N khi chụp kiểm tra).</summary>
    public void SelectTab(int index) => Tabs.SelectedIndex = Math.Clamp(index, 0, Tabs.ItemCount - 1);

    /// <summary>Môn đang chọn (MainWindow giữ lại khi dựng lại trang).</summary>
    public string? Selected => _subject?.Name;

    /// <summary>Quét thư mục môn trên máy (có cache theo mtime) rồi dựng lại danh sách có số tài liệu.</summary>
    private async Task ScanAsync()
    {
        try { Build(await Documents.ListSubjectsAsync()); }
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Warn($"Quét thư mục môn: {e.Message}"); }
    }

    private void Build(JsonArray? scan)
    {
        _wanted ??= _subject?.Name;
        _rows = SubjectsPresenter.Rows(_state.Lms, scan);
        ShowList();
    }

    private void OnFilter(object? sender, TextChangedEventArgs e) => ShowList();

    private void ShowList()
    {
        var rows = SubjectsPresenter.Filter(_rows, Filter.Text ?? "");
        Subjects.ItemsSource = rows;
        ListEmpty.IsVisible = rows.Count == 0;
        ListEmpty.Text = L.T(_rows.Count == 0 ? "subjects.listEmpty" : "subjects.noMatch");
        var keep = _wanted ?? _subject?.Name;
        Subjects.SelectedItem = rows.FirstOrDefault(r => NameMatch.Same(r.Name, keep)) ?? rows.FirstOrDefault();
        _wanted = null;
        DetailCard.IsVisible = Subjects.SelectedItem is not null;
    }

    private void OnPick(object? sender, SelectionChangedEventArgs e)
    {
        if (Subjects.SelectedItem is not SubjectRow s) return;
        var changed = _subject?.Name != s.Name;
        _subject = s;
        SubjectName.Text = s.Name;
        SubjectMeta.Text = SubjectsPresenter.Meta(s, _state.Timeline, _state.Lms, Format.Now);
        LmsButton.IsEnabled = DownloadButton.IsEnabled = s.OnLms;
        _root = Config.Str("folders.subjects") + "/" + s.Name;
        if (changed) _dir = _keepDir is { } keep && keep.StartsWith(_root, StringComparison.OrdinalIgnoreCase) ? keep : _root;
        _keepDir = null;
        _filled.Clear();
        DetailCard.IsVisible = true;
        FillTab();
    }

    private void OnTab(object? sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, Tabs)) FillTab();   // SelectionChanged của bảng bên trong cũng nổi bọt lên đây
    }

    /// <summary>Dựng tab đang chọn nếu chưa dựng cho môn này.</summary>
    private void FillTab()
    {
        if (_subject is not { } s || Tabs.SelectedItem is not TabItem tab || !_filled.Add(tab)) return;
        if (tab == FilesTab)
        {
            if (RecentToggle.IsChecked == true) _ = LoadRecentAsync(s);
            else _ = LoadDirAsync();
        }
        else if (tab == DueTab)
            Tables.Show(Due, DueEmpty, SubjectsPresenter.Due(_state.Timeline, s, Format.Now), L.T("subjects.dueEmpty"));
        else if (tab == NewsTab)
            Tables.Show(News, NewsEmpty, SubjectsPresenter.News(_state.Lms, s), L.T(s.OnLms ? "subjects.newsEmpty" : "subjects.notOnLms"));
        else if (tab == GradesTab)
            Tables.Show(Grades, GradesEmpty, SubjectsPresenter.Grades(_state.Lms, s), L.T(s.OnLms ? "subjects.gradesEmpty" : "subjects.notOnLms"));
    }

    private static void Status(DataGrid grid, TextBlock empty, string text)
    {
        grid.IsVisible = false;
        empty.Text = text;
        empty.IsVisible = true;
    }

    // ------------------------------------------------------------------ tab Tài liệu

    /// <summary>Một tầng thư mục, đọc nền (ổ mạng, USB chậm không làm đứng giao diện); quá 150 ms thì hiện "đang đọc".</summary>
    private async Task LoadDirAsync()
    {
        var gen = ++_gen;
        var dir = _dir;
        PathText.Text = dir;
        UpButton.IsEnabled = !dir.Equals(_root, StringComparison.OrdinalIgnoreCase);
        var task = Task.Run(() => Documents.ListDir(dir));
        if (await Task.WhenAny(task, Task.Delay(150)) != task && gen == _gen) Status(Files, FilesEmpty, L.T("subjects.scanning"));
        JsonObject d;
        try { d = await task; }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Warn($"Đọc thư mục {dir}: {e.Message}");
            if (gen == _gen) Status(Files, FilesEmpty, L.T("subjects.scanFailed"));
            return;
        }
        if (gen != _gen) return;
        if (d["missing"] is not null) { Status(Files, FilesEmpty, L.T("subjects.filesEmpty")); return; }
        Tables.Show(Files, FilesEmpty, SubjectsPresenter.FileRows(d), L.T("subjects.folderEmpty"));
    }

    private async Task LoadRecentAsync(SubjectRow s)
    {
        var gen = ++_gen;
        Status(Recent, FilesEmpty, L.T("subjects.scanning"));
        try
        {
            var recent = await Documents.SubjectFilesAsync(s.Name, 200);
            if (gen == _gen) Tables.Show(Recent, FilesEmpty, SubjectsPresenter.RecentRows(recent), L.T("subjects.recentEmpty"));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Warn($"Quét tài liệu {s.Name}: {e.Message}");
            if (gen == _gen) Status(Recent, FilesEmpty, L.T("subjects.scanFailed"));
        }
    }

    private void OnRecentToggle(object? sender, RoutedEventArgs e)
    {
        var recent = RecentToggle.IsChecked == true;
        Recent.IsVisible = recent;
        Files.IsVisible = UpButton.IsVisible = PathText.IsVisible = !recent;
        _filled.Remove(FilesTab);
        FillTab();
    }

    private void OnUp(object? sender, RoutedEventArgs e)
    {
        if (_dir.Equals(_root, StringComparison.OrdinalIgnoreCase)) return;
        _dir = _dir[.._dir.LastIndexOf('/')];
        _ = LoadDirAsync();
    }

    private async void OnOpenFolder(object? sender, RoutedEventArgs e) => await Opener.OpenAsync(this, _dir);

    private async void OnFileOpen(object? sender, TappedEventArgs e)
    {
        if (Files.SelectedItem is not FileRow f) return;
        if (f.Dir) { _dir += "/" + f.Name; _ = LoadDirAsync(); }
        else await Opener.OpenAsync(this, _dir + "/" + f.Name);
    }

    /// <summary>Enter mở, Backspace lên thư mục cha (như trình quản lý file).</summary>
    private void OnFilesKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Back) { OnUp(sender, e); e.Handled = true; }
        else if (e.Key == Key.Enter) { OnFileOpen(sender, null!); e.Handled = true; }
    }

    private async void OnRecentOpen(object? sender, TappedEventArgs e)
    {
        if (Recent.SelectedItem is RecentRow r) await Opener.OpenAsync(this, r.Path);
    }

    private void OnFileMenu(object? sender, EventArgs e)
    {
        if (sender is MenuFlyout m) Fill(m, Files.SelectedItem is FileRow f ? (f.Name, _dir + "/" + f.Name) : null);
    }

    private void OnRecentMenu(object? sender, EventArgs e)
    {
        if (sender is MenuFlyout m) Fill(m, Recent.SelectedItem is RecentRow r ? (r.Name, r.Path) : null);
    }

    /// <summary>Menu chuột phải của một file: mở thư mục chứa, sao chép tên, sao chép đường dẫn đầy đủ.</summary>
    private void Fill(MenuFlyout menu, (string Name, string Rel)? file)
    {
        menu.Items.Clear();
        if (file is not { } f) return;
        MenuItem Item(string header, Func<Task> run)
        {
            var i = new MenuItem { Header = header };
            i.Click += async (_, _) => await run();
            return i;
        }
        menu.Items.Add(Item(L.T("files.reveal"), () => Opener.RevealAsync(this, f.Rel)));
        menu.Items.Add(Item(L.T("common.copyName"), () => Copy(f.Name)));
        menu.Items.Add(Item(L.T("common.copyPath"), () => Copy(Paths.StudyPath(f.Rel) ?? f.Rel)));
    }

    private async Task Copy(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } c) await c.SetTextAsync(text);
    }

    // ------------------------------------------------------------------ mở trên web, tải tài liệu

    private async void OnDueOpen(object? sender, TappedEventArgs e)
    {
        if (Due.SelectedItem is CalendarRow { Url: { } url }) await OpenUrl(url);
    }

    private async void OnNewsOpen(object? sender, TappedEventArgs e)
    {
        if (News.SelectedItem is SubjectNews { Url: { } url }) await OpenUrl(url);
    }

    private void OnOpenLms(object? sender, RoutedEventArgs e) => PickCourse(LmsButton, c => _ = OpenUrl(c.Url));

    private void OnDownload(object? sender, RoutedEventArgs e) => PickCourse(DownloadButton, c => _download?.Invoke(c));

    private void OnOpenLibrary(object? sender, RoutedEventArgs e)
    {
        if (_subject is { } s) _openLibrary?.Invoke(s.Name);
    }

    /// <summary>Môn một lớp thì làm luôn; nhiều lớp (lý thuyết, thí nghiệm, kỳ khác) thì hiện menu chọn lớp dưới nút.</summary>
    private void PickCourse(Button button, Action<LmsCourse> run)
    {
        if (_subject is not { OnLms: true } s) return;
        var courses = s.Courses.OrderByDescending(c => c.Term).ToList();
        if (courses.Count == 1) { run(courses[0]); return; }
        var menu = new MenuFlyout();
        foreach (var c in courses)
        {
            var item = new MenuItem { Header = $"{c.Term}, {c.Part ?? L.T("common.theory")}, {c.Teacher}" };
            item.Click += (_, _) => run(c);
            menu.Items.Add(item);
        }
        menu.ShowAt(button);
    }

    private Task OpenUrl(string url) => Links.OpenAsync(this, url, _subject?.Name ?? "");

    // ------------------------------------------------------------------ bố cục co giãn

    /// <summary>Hai cột khi đủ chỗ cho danh sách (ListMinWidth) và chi tiết (DetailMinWidth); không thì danh sách lên trên, thấp lại.</summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var listMin = (double)this.FindResource("ListMinWidth")!;
        var two = e.NewSize.Width >= listMin + 24 + (double)this.FindResource("DetailMinWidth")!;
        var cols = Columns.ColumnDefinitions;
        cols[0].MaxWidth = two ? (double)this.FindResource("ListMaxWidth")! : double.PositiveInfinity;
        cols[1].Width = new GridLength(two ? 24 : 0);
        cols[2].Width = two ? new GridLength(3, GridUnitType.Star) : new GridLength(0);
        Columns.RowDefinitions[0].Height = two ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        Columns.RowDefinitions[1].Height = two ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        ListCard.MaxHeight = two ? double.PositiveInfinity : (double)this.FindResource("ListStackedMaxHeight")!;
        Grid.SetColumn(DetailCard, two ? 2 : 0);
        Grid.SetRow(DetailCard, two ? 0 : 1);
        DetailCard.Margin = two ? default : new Thickness(0, 16, 0, 0);
    }
}
