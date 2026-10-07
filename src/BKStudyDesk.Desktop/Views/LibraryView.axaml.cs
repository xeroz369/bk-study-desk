using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BKStudyDesk.Desktop.Files;
using SoHocTap.Api;
using SoHocTap.Core;
using SoHocTap.Files;
using SoHocTap.Library;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Trang Thư viện: mọi môn của thư viện (repo bk-study-library), không chỉ môn đang học. Danh sách, tìm, lọc: Presentation/LibraryPage.cs;
/// dòng, bộ lọc của bảng tài liệu: Presentation/LibraryTab.cs. Đọc index, tải file chạy nền (LibraryService). Tải về thì lưu vào
/// Môn học/&lt;thư mục môn&gt;/Thư viện/ (môn của người dùng là đúng thư mục môn đó, môn khác theo tên trên thư viện).
/// </summary>
public partial class LibraryView : UserControl, IFillPage
{
    private readonly AppState _state = null!;
    private readonly LibraryService _library = null!;
    private IReadOnlyList<LibraryCourseRow> _courses = [];
    private LibraryCourseRow? _course;
    private IReadOnlyList<LibraryRow> _rows = [];
    private IReadOnlyList<SubjectRow> _subjects = [];
    private string? _wanted;
    private bool _filterReady;
    private int _gen;

    public LibraryView() => InitializeComponent();

    /// <param name="previous">Trang cũ khi dựng lại vì dữ liệu mới: giữ chữ tìm, khoa và môn đang xem.</param>
    internal LibraryView(AppState state, LibraryService library, LibraryView? previous) : this()
    {
        _state = state;
        _library = library;
        _subjects = SubjectsPresenter.Rows(state.Lms, null);   // môn trên LMS ngay; môn chỉ có thư mục trên máy thêm sau khi quét (MineAsync)
        _wanted = previous?._course?.Course.Id;
        Search.Text = previous?.Search.Text ?? "";
        _keepFaculty = (previous?.FacultyFilter.SelectedItem as FilterOption)?.Value;
        // Trang được dựng lại mỗi khi dữ liệu đổi: chỉ nghe Changed khi đang hiện, để trang cũ không bị giữ lại.
        void OnChanged() => Dispatcher.UIThread.Post(BuildList);
        AttachedToVisualTree += (_, _) => library.Changed += OnChanged;
        DetachedFromVisualTree += (_, _) => library.Changed -= OnChanged;
        BuildList();
        _ = MineAsync();
        if (library.Index is null) _ = library.RefreshAsync(LibraryRefresh.IfDue);
    }

    private string? _keepFaculty;

    /// <summary>Mở đúng môn của trang Môn học (nút Xem trong Thư viện): chọn môn thư viện khớp môn đó, xóa chữ tìm đang gõ.</summary>
    public void SelectSubject(string subject)
    {
        _wantedSubject = subject;
        Search.Text = "";
        FacultyFilter.SelectedIndex = 0;
        ShowList();
    }

    private string? _wantedSubject;

    /// <summary>Môn ở trang Môn học (cả môn chỉ có thư mục trên máy) khớp môn nào của thư viện; quét thư mục chạy nền.</summary>
    private async Task MineAsync()
    {
        System.Text.Json.Nodes.JsonArray? scan = null;
        try { scan = await Documents.ListSubjectsAsync(); }
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Warn($"Thư viện: quét thư mục môn: {e.Message}"); }
        _subjects = SubjectsPresenter.Rows(_state.Lms, scan);
        BuildList();
    }

    // ------------------------------------------------------------------ danh sách môn

    private void BuildList()
    {
        // Khớp lại mỗi lần dựng: danh mục thư viện có thể vừa tải xong sau khi danh sách môn đã có.
        _courses = LibraryPagePresenter.Courses(_library.Index, _subjects.Select(s => (s.Name, _library.Match(LibraryPresenter.Codes(s, _state.Mybk)))));
        _filterReady = false;
        var faculties = LibraryPagePresenter.Faculties(_library.Index);
        var keep = (FacultyFilter.SelectedItem as FilterOption)?.Value ?? _keepFaculty;
        _keepFaculty = null;
        FacultyFilter.ItemsSource = faculties;
        FacultyFilter.SelectedItem = faculties.FirstOrDefault(f => f.Value == keep) ?? faculties[0];
        FacultyFilter.IsVisible = faculties.Count > 2;
        _filterReady = true;
        ShowList();
    }

    private void OnSearch(object? sender, TextChangedEventArgs e) => ShowList();

    private void OnFaculty(object? sender, SelectionChangedEventArgs e)
    {
        if (_filterReady) ShowList();
    }

    private void ShowList()
    {
        var rows = LibraryPagePresenter.Filter(_courses, Search.Text ?? "", (FacultyFilter.SelectedItem as FilterOption)?.Value);
        Courses.ItemsSource = rows;
        var state = _library.Index is null
            ? _library.LastError is { } err ? L.F("library.pageError", err) : L.T("library.loading")
            : rows.Count == 0 ? L.T("library.noCourse") : null;
        ListEmpty.Text = state ?? "";
        ListState.IsVisible = state is not null;
        RetryButton.IsVisible = _library.Index is null && _library.LastError is not null;
        var keepId = _wanted ?? _course?.Course.Id;
        Courses.SelectedItem = (_wantedSubject is { } subject ? rows.FirstOrDefault(r => r.Mine && NameMatch.Same(r.Folder, subject)) : null)
            ?? rows.FirstOrDefault(r => r.Course.Id == keepId) ?? rows.FirstOrDefault();
        _wanted = _wantedSubject = null;
        DetailCard.IsVisible = Courses.SelectedItem is not null;
    }

    private void OnRetry(object? sender, RoutedEventArgs e) => _ = _library.RefreshAsync(LibraryRefresh.Tab);

    private void OnPick(object? sender, SelectionChangedEventArgs e)
    {
        if (Courses.SelectedItem is not LibraryCourseRow c || ReferenceEquals(c, _course)) return;
        var same = _course?.Course.Id == c.Course.Id;
        _course = c;
        CourseName.Text = c.Title;
        CourseMeta.Text = string.Join(", ", new[] { c.Sub, L.F("library.saveTo", $"{Config.Str("folders.subjects")}/{c.Folder}") });
        WebButton.IsVisible = LibraryClient.CourseUrl(_library.Index, c.Course) is not null;
        ContributeButton.IsVisible = ContributeUrl() is not null;
        if (!same) Say("");
        // Danh sách dựng lại (quét xong thư mục môn, index đổi) chọn lại đúng môn đang đọc dở: chờ lượt đang chạy, không gọi lượt mới.
        // Lượt mới lúc đó bị LibraryClient chặn (vừa thử xong) nên trả về rỗng, và vì là lượt mới nhất nên đè mất kết quả thật.
        if (same && _loading is { IsCompleted: false }) return;
        _loading = LoadItemsAsync(c);
    }

    private Task? _loading;

    // ------------------------------------------------------------------ tài liệu của môn

    private async Task LoadItemsAsync(LibraryCourseRow c)
    {
        var gen = ++_gen;
        Status(L.T("library.loading"));
        IReadOnlyList<LibraryRow> rows = [];
        string? error = null;
        var cleaned = 0;
        try
        {
            var (r, gone) = await _library.CourseAsync(c.Course, LibraryRefresh.Tab);
            // Rỗng mà không lỗi: một lượt khác vừa gọi môn này (LibraryClient chặn gọi trùng) và chưa có bản lưu. Đọc lại bản lưu tới khi
            // lượt kia ghi xong, tối đa library.timeoutSeconds (lượt kia cũng hết giờ trong khoảng đó); không kết luận là môn trống.
            var until = DateTime.UtcNow.AddSeconds(Config.Int("library.timeoutSeconds", 30));
            while (r.Value is null && r.Error is null && gen == _gen && DateTime.UtcNow < until)
            {
                await Task.Delay(Config.Int("library.gapMs", 300));
                (r, gone) = await _library.CourseAsync(c.Course, LibraryRefresh.CacheOnly);
            }
            cleaned = gone.Count;
            error = r.Value is null && r.Error is null ? L.F("library.timeout", Config.Int("library.timeoutSeconds", 30)) : r.Error;
            if (r.Value?.Items is { } items)
                rows = await Task.Run(() => items.Where(i => !i.Removed).Select(i => LibraryPresenter.Row(c.Course, i, false, _library.LocalState(c.Course, i))).ToList());
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Warn($"Thư viện: đọc môn {c.Course.Code}: {e.Message}");
            error = e.Message;
        }
        if (gen != _gen) return;   // đã chọn môn khác trong lúc chờ
        if (cleaned > 0) Say(L.F("library.cleaned", cleaned));
        var any = rows.Count > 0 || error is null;
        var note = LibraryPresenter.Note(c.ReplacementOf is null ? [] : [new CourseMatch(c.Course, c.ReplacementOf)], any ? error : null);
        Note.Text = note;
        Note.IsVisible = note.Length > 0;
        _rows = LibraryPresenter.Sort(rows);
        FillFilters();
        if (!any && error is { Length: > 0 }) Status(L.F("library.pageError", error));
        else if (_rows.Count == 0) Status(L.T("library.notInLibrary"));
        else ShowRows();
    }

    private void Status(string text)
    {
        Items.IsVisible = false;
        ItemsEmpty.Text = text;
        ItemsEmpty.IsVisible = true;
    }

    private void FillFilters()
    {
        _itemFilterReady = false;
        Fill(TeacherFilter, LibraryPresenter.Options(_rows.Select(r => r.Teacher), "library.filter.allTeachers"));
        Fill(TermFilter, LibraryPresenter.Options(_rows.Select(r => r.Term), "library.filter.allTerms", newestFirst: true));
        Fill(ExamFilter, LibraryPresenter.Options(_rows.Select(r => r.Exam), "library.filter.allExams"));
        _itemFilterReady = true;
    }

    private bool _itemFilterReady;

    /// <summary>Bộ lọc chỉ hiện khi có giá trị; giữ lựa chọn cũ nếu vẫn còn.</summary>
    private static void Fill(ComboBox box, IReadOnlyList<FilterOption> opts)
    {
        var keep = (box.SelectedItem as FilterOption)?.Value;
        box.IsVisible = opts.Count > 1;
        box.ItemsSource = opts;
        box.SelectedItem = opts.FirstOrDefault(o => o.Value == keep) ?? opts[0];
    }

    private void OnItemFilter(object? sender, SelectionChangedEventArgs e)
    {
        if (_itemFilterReady) ShowRows();
    }

    private void ShowRows()
    {
        static string? Pick(ComboBox b) => b.IsVisible ? (b.SelectedItem as FilterOption)?.Value : null;
        var rows = LibraryPresenter.Filter(_rows, Pick(TeacherFilter), Pick(TermFilter), Pick(ExamFilter));
        // Cột nhãn tùy chọn: ẩn khi không mục nào của môn có giá trị. Thứ tự cột như LibraryView.axaml.
        var c = Items.Columns;
        c[1].IsVisible = _rows.Any(r => r.Term.Length > 0);
        c[2].IsVisible = _rows.Any(r => r.Teacher.Length > 0);
        c[3].IsVisible = _rows.Any(r => r.Chapter.Length > 0);
        c[4].IsVisible = _rows.Any(r => r.Exam.Length > 0);
        c[5].IsVisible = _rows.Any(r => r.Size > 0);
        Tables.Show(Items, ItemsEmpty, Tables.Grouped(rows, nameof(LibraryRow.Group)), L.T("library.filteredEmpty"));
    }

    private async Task RefreshLocalStateAsync()
    {
        var rows = _rows;
        var fresh = await Task.Run(() => rows.Select(r => r with { State = _library.LocalState(r.Course, r.Item) }).ToList());
        if (!ReferenceEquals(rows, _rows)) return;
        _rows = fresh;
        ShowRows();
    }

    // ------------------------------------------------------------------ lệnh

    private void OnItemOpen(object? sender, TappedEventArgs e)
    {
        if (Items.SelectedItem is LibraryRow r) Run(() => DoAsync(r, LibraryActions.Primary(r.Item, r.State)));
    }

    private void OnItemMenu(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout menu) return;
        menu.Items.Clear();
        if (Items.SelectedItem is not LibraryRow r) return;
        void Add(string header, Action run)
        {
            var i = new MenuItem { Header = header };
            i.Click += (_, _) => run();
            menu.Items.Add(i);
        }
        var primary = LibraryActions.Primary(r.Item, r.State);
        var web = LibraryClient.ItemUrl(_library.Index, r.Course, r.Item);
        Add(LibraryPresenter.Label(primary, r), () => Run(() => DoAsync(r, primary)));
        if (primary != LibraryAction.OpenWeb && web is not null) Add(L.T("library.openWeb"), () => _ = OpenUrl(web.AbsoluteUri));
        if (r.Item.Files is { Count: > 0 } && primary != LibraryAction.Download && r.State != ItemLocal.Downloaded)
            Add(LibraryPresenter.Label(LibraryAction.Download, r), () => Run(() => DoAsync(r, LibraryAction.Download)));
        if (r.State != ItemLocal.None && primary != LibraryAction.Open) Add(L.T("library.openFile"), () => _ = OpenLocal(FirstFile(r)));
        if (r.State != ItemLocal.None) Add(L.T("files.reveal"), () => { if (FirstFile(r) is { } f) _ = Opener.RevealAsync(this, Paths.RelativeToStudy(f)); });
        if (r.Item.Book?.Isbn is { Length: > 0 } isbn) Add(L.F("library.copyIsbn", isbn), () => _ = Copy(isbn));
        if (web is not null) Add(L.T("library.copyLink"), () => _ = Copy(web.AbsoluteUri));
    }

    /// <summary>Lệnh async từ menu, bấm đúp: lỗi không lường trước chỉ ghi log và báo trên trang, không làm sập app.</summary>
    private async void Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Warn($"Thư viện: {e.Message}");
            Say(L.F("library.failed.other", e.Message));
        }
    }

    private async Task DoAsync(LibraryRow r, LibraryAction action)
    {
        switch (action)
        {
            case LibraryAction.OpenWeb:
                if (LibraryClient.ItemUrl(_library.Index, r.Course, r.Item) is { } u) await OpenUrl(u.AbsoluteUri);
                break;
            case LibraryAction.Open when r.State == ItemLocal.Downloaded:
                await OpenLocal(FirstFile(r));
                break;
            case LibraryAction.Open:
                if (await DownloadAsync(r) is [var file, ..]) await OpenLocal(file);
                break;
            case LibraryAction.Download:
                // Tải lần đầu file đọc được (PDF, Markdown) thì mở luôn; tải bản mới thì chỉ báo.
                if (await DownloadAsync(r) is [var first, ..] && r.State == ItemLocal.None && LibraryActions.OpensAfterDownload(r.Item)) await OpenLocal(first);
                break;
            case LibraryAction.Install:
                await InstallAsync(r);
                break;
        }
    }

    /// <summary>Tải các file của mục vào Môn học/&lt;thư mục môn&gt;/Thư viện/. Trả về đường dẫn, null nếu lỗi (đã báo trên trang).</summary>
    private async Task<IReadOnlyList<string>?> DownloadAsync(LibraryRow r)
    {
        if (_course is not { } c) return null;
        Say(L.F("library.downloading", r.Title));
        var res = await Task.Run(() => _library.DownloadAsync(r.Course, r.Item, c.Folder));
        if (ReferenceEquals(_course, c)) await RefreshLocalStateAsync();
        if (res.Ok)
        {
            Say(L.F("library.downloaded", r.Title));
            return res.Paths;
        }
        Say(res.Error switch
        {
            DownloadError.Network => L.F("library.failed.network", r.Title, res.Detail ?? ""),
            DownloadError.Mismatch => L.F("library.failed.mismatch", r.Title),
            DownloadError.TooLarge => L.F("library.failed.tooLarge", r.Title),
            _ => L.F("library.failed.other", r.Title),
        });
        return null;
    }

    /// <summary>
    /// Gói quiz: tải (nếu chưa có bản mới nhất) rồi đi đúng đường Nhập gói của Luyện tập: gói .json mới thì cài thẳng (PackImport);
    /// .md, .zip hay cập nhật gói đã cài thì chép đường dẫn, mở thư mục chứa để người dùng chọn Nhập gói trong Luyện tập.
    /// </summary>
    private async Task InstallAsync(LibraryRow r)
    {
        var files = r.State == ItemLocal.Downloaded ? _library.DownloadedFiles(r.Course, r.Item) : await DownloadAsync(r);
        if (files is not { Count: > 0 }) return;
        var path = files.FirstOrDefault(f => Path.GetExtension(f).ToLowerInvariant() is ".json" or ".md" or ".zip") ?? files[0];
        var h = await Task.Run(() => QuizPackHandOff.Decide(path, ApiRouter.PackInstalled));
        switch (h.Kind)
        {
            case HandOffKind.Install:
                try { await Task.Run(() => ApiRouter.InstallPack(h.Pack!)); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Say(L.F("library.installInvalid", Path.GetFileName(path), e.Message));
                    return;
                }
                Say(L.F("library.installed", r.Title));
                break;
            case HandOffKind.PracticeImport:
                await Copy(path);
                await Opener.RevealAsync(this, Paths.RelativeToStudy(path));
                Say(L.F("library.installManual", r.Title));
                break;
            default:
                Say(L.F("library.installInvalid", Path.GetFileName(path), h.Error ?? ""));
                break;
        }
    }

    private string? FirstFile(LibraryRow r) => _library.DownloadedFiles(r.Course, r.Item) is [var f, ..] ? f : null;

    private async Task OpenLocal(string? full)
    {
        if (full is not null && !await Opener.OpenAsync(this, Paths.RelativeToStudy(full))) Say(L.F("common.cantOpen", Path.GetFileName(full)));
    }

    private Uri? ContributeUrl() => LibraryClient.ContributeUrl(_library.Index, SoHocTap.Core.Settings.Library.ContributePath);

    private async void OnWeb(object? sender, RoutedEventArgs e)
    {
        if (_course is { } c && (LibraryClient.CourseUrl(_library.Index, c.Course) ?? LibraryClient.SiteOf(_library.Index)) is { } u) await OpenUrl(u.AbsoluteUri);
    }

    private async void OnContribute(object? sender, RoutedEventArgs e)
    {
        if (ContributeUrl() is { } u) await OpenUrl(u.AbsoluteUri);
    }

    private Task OpenUrl(string url) => Links.OpenAsync(this, url, _course?.Title ?? "");

    private async Task Copy(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } c) await c.SetTextAsync(text);
    }

    /// <summary>Một dòng báo trên trang (đang tải, đã tải, lỗi); thay bằng dòng mới ở lần sau.</summary>
    private void Say(string text)
    {
        StatusLine.Text = text;
        StatusLine.IsVisible = text.Length > 0;
    }

    // ------------------------------------------------------------------ bố cục co giãn

    /// <summary>Hai cột khi đủ chỗ (như trang Môn học); không thì danh sách lên trên, thấp lại.</summary>
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
