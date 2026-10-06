using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BKStudyDesk.Desktop.Files;
using SoHocTap.Api;
using SoHocTap.Core;
using SoHocTap.Library;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Tab Thư viện của trang Môn học: tài liệu chung của môn (repo bk-study-library). Dòng, tên, bộ lọc: Presentation/LibraryTab.cs.
/// Đọc index, tải file chạy nền (LibraryService). Báo kết quả bằng một dòng chữ trong tab (không có thanh trạng thái chung).
/// </summary>
public partial class SubjectsView
{
    private LibraryService? _library;
    private IReadOnlyList<CourseMatch> _libMatches = [];
    private IReadOnlyList<LibraryRow> _libRows = [];
    private bool _libFilterReady;
    private int _libGen;

    private void SetupLibrary(LibraryService library)
    {
        _library = library;
        // Trang được dựng lại mỗi khi dữ liệu đổi: chỉ nghe Changed khi đang hiện, để trang cũ không bị giữ lại.
        void OnChanged() => Dispatcher.UIThread.Post(OnLibraryChanged);
        AttachedToVisualTree += (_, _) => library.Changed += OnChanged;
        DetachedFromVisualTree += (_, _) => library.Changed -= OnChanged;
    }

    /// <summary>Môn đang chọn khớp với môn nào của thư viện. Index chưa đọc thì đọc nền (cache trước).</summary>
    private void UpdateLibraryMatches(SubjectRow s)
    {
        if (_library is not { } lib) return;
        _libMatches = lib.Match(LibraryPresenter.Codes(s, _state.Mybk));
        if (lib.Index is null) _ = lib.RefreshAsync(LibraryRefresh.IfDue);
    }

    private void OnLibraryChanged()
    {
        if (_subject is not { } s) return;
        UpdateLibraryMatches(s);
        _filled.Remove(LibraryTab);
        if (Tabs.SelectedItem == LibraryTab) FillTab();
    }

    private async Task LoadLibraryAsync(SubjectRow s)
    {
        if (_library is not { } lib) return;
        var gen = ++_libGen;
        var matches = _libMatches;
        _ = lib.RefreshAsync(LibraryRefresh.Tab);   // index đổi thì Changed dựng lại tab
        ContributeButton.IsVisible = ContributeUrl() is not null;
        if (matches.Count == 0) { ShowNoMatch(lib); return; }
        Status(LibraryItems, LibraryEmpty, L.T("library.loading"));
        var multi = matches.Count > 1;
        var rows = new List<LibraryRow>();
        string? error = null;
        var any = false;
        var cleaned = 0;
        try
        {
            foreach (var m in matches)
            {
                var (r, gone) = await lib.CourseAsync(m.Course, LibraryRefresh.Tab);
                cleaned += gone.Count;
                error ??= r.Error;
                if (r.Value?.Items is not { } items) continue;
                any = true;
                rows.AddRange(await Task.Run(() => items.Where(i => !i.Removed).Select(i => LibraryPresenter.Row(m.Course, i, multi, lib.LocalState(m.Course, i))).ToList()));
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Warn($"Thư viện: đọc môn {s.Name}: {e.Message}");
            error ??= e.Message;
        }
        if (gen != _libGen || !ReferenceEquals(_subject, s)) return;   // đã chọn môn khác trong lúc chờ
        if (cleaned > 0) Say(L.F("library.cleaned", cleaned));
        LibraryWebButton.IsVisible = LibraryWeb() is not null;
        LibraryItems.Columns[1].IsVisible = multi;   // cột Lớp: chỉ khi môn khớp nhiều môn của thư viện
        var note = LibraryPresenter.Note(matches, any ? error : null);
        LibraryNote.Text = note;
        LibraryNote.IsVisible = note.Length > 0;
        _libRows = any ? LibraryPresenter.Sort(rows) : [];
        FillFilters();
        if (!any && error is not null) Status(LibraryItems, LibraryEmpty, L.F("library.error", error));
        else ShowLibraryRows();
    }

    /// <summary>Môn không khớp môn nào của thư viện: đang đọc, lỗi, hay nói rõ môn chưa có tài liệu (để nút Đóng góp).</summary>
    private void ShowNoMatch(LibraryService lib)
    {
        _libRows = [];
        FillFilters();
        LibraryNote.IsVisible = LibraryWebButton.IsVisible = false;
        if (lib.Index is null && lib.LastError is { } err) Status(LibraryItems, LibraryEmpty, L.F("library.error", err));
        else if (lib.Index is null) Status(LibraryItems, LibraryEmpty, L.T("library.loading"));
        else Status(LibraryItems, LibraryEmpty, L.T("library.notInLibrary"));
    }

    private void FillFilters()
    {
        _libFilterReady = false;
        Fill(TeacherFilter, LibraryPresenter.Options(_libRows.Select(r => r.Teacher), "library.filter.allTeachers"));
        Fill(TermFilter, LibraryPresenter.Options(_libRows.Select(r => r.Term), "library.filter.allTerms", newestFirst: true));
        Fill(ExamFilter, LibraryPresenter.Options(_libRows.Select(r => r.Exam), "library.filter.allExams"));
        _libFilterReady = true;
    }

    /// <summary>Bộ lọc chỉ hiện khi có giá trị; giữ lựa chọn cũ nếu vẫn còn.</summary>
    private static void Fill(ComboBox box, IReadOnlyList<FilterOption> opts)
    {
        var keep = (box.SelectedItem as FilterOption)?.Value;
        box.IsVisible = opts.Count > 1;
        box.ItemsSource = opts;
        box.SelectedItem = opts.FirstOrDefault(o => o.Value == keep) ?? opts[0];
    }

    private void OnLibraryFilter(object? sender, SelectionChangedEventArgs e)
    {
        if (_libFilterReady) ShowLibraryRows();
    }

    private void ShowLibraryRows()
    {
        static string? Pick(ComboBox b) => b.IsVisible ? (b.SelectedItem as FilterOption)?.Value : null;
        var rows = LibraryPresenter.Filter(_libRows, Pick(TeacherFilter), Pick(TermFilter), Pick(ExamFilter));
        // Cột nhãn tùy chọn: ẩn khi không mục nào của môn có giá trị.
        // Thứ tự cột như SubjectsView.axaml: Tên, Lớp, Học kỳ, Giảng viên, Chương, Kiểm tra, Cỡ, Trên máy.
        var c = LibraryItems.Columns;
        c[2].IsVisible = _libRows.Any(r => r.Term.Length > 0);
        c[3].IsVisible = _libRows.Any(r => r.Teacher.Length > 0);
        c[4].IsVisible = _libRows.Any(r => r.Chapter.Length > 0);
        c[5].IsVisible = _libRows.Any(r => r.Exam.Length > 0);
        c[6].IsVisible = _libRows.Any(r => r.Size > 0);
        var view = new DataGridCollectionView(rows);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(LibraryRow.Group)));
        LibraryItems.ItemsSource = view;
        LibraryItems.IsVisible = rows.Count > 0;
        LibraryEmpty.Text = L.T(_libRows.Count == 0 ? "library.empty" : "library.filteredEmpty");
        LibraryEmpty.IsVisible = rows.Count == 0;
    }

    private async Task RefreshLocalStateAsync()
    {
        if (_library is not { } lib) return;
        var rows = _libRows;
        var fresh = await Task.Run(() => rows.Select(r => r with { State = lib.LocalState(r.Course, r.Item) }).ToList());
        if (!ReferenceEquals(rows, _libRows)) return;
        _libRows = fresh;
        ShowLibraryRows();
    }

    // ------------------------------------------------------------------ lệnh

    private void OnLibraryOpen(object? sender, TappedEventArgs e)
    {
        if (LibraryItems.SelectedItem is LibraryRow r) Run(() => DoAsync(r, LibraryActions.Primary(r.Item, r.State)));
    }

    private void OnLibraryMenu(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout menu) return;
        menu.Items.Clear();
        if (LibraryItems.SelectedItem is not LibraryRow r || _library is not { } lib) return;
        void Add(string header, Action run)
        {
            var i = new MenuItem { Header = header };
            i.Click += (_, _) => run();
            menu.Items.Add(i);
        }
        var primary = LibraryActions.Primary(r.Item, r.State);
        var web = LibraryClient.ItemUrl(lib.Index, r.Course, r.Item);
        Add(LibraryPresenter.Label(primary, r), () => Run(() => DoAsync(r, primary)));
        if (primary != LibraryAction.OpenWeb && web is not null) Add(L.T("library.openWeb"), () => _ = OpenUrl(web.AbsoluteUri));
        if (r.Item.Files is { Count: > 0 } && primary != LibraryAction.Download && r.State != ItemLocal.Downloaded)
            Add(LibraryPresenter.Label(LibraryAction.Download, r), () => Run(() => DoAsync(r, LibraryAction.Download)));
        if (r.State != ItemLocal.None && primary != LibraryAction.Open) Add(L.T("library.openFile"), () => _ = OpenLocal(FirstFile(r)));
        if (r.State != ItemLocal.None) Add("Mở thư mục chứa", () => { if (FirstFile(r) is { } f) _ = Opener.RevealAsync(this, Paths.RelativeToStudy(f)); });
        if (r.Item.Book?.Isbn is { Length: > 0 } isbn) Add(L.F("library.copyIsbn", isbn), () => _ = Copy(isbn));
        if (web is not null) Add(L.T("library.copyLink"), () => _ = Copy(web.AbsoluteUri));
    }

    /// <summary>Lệnh async từ menu, bấm đúp: lỗi không lường trước chỉ ghi log và báo trong tab, không làm sập app.</summary>
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
                if (_library is { } lib && LibraryClient.ItemUrl(lib.Index, r.Course, r.Item) is { } u) await OpenUrl(u.AbsoluteUri);
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

    /// <summary>Tải các file của mục vào Môn học/&lt;môn&gt;/Thư viện/. Trả về đường dẫn, null nếu lỗi (đã báo trong tab).</summary>
    private async Task<IReadOnlyList<string>?> DownloadAsync(LibraryRow r)
    {
        if (_subject is not { } s || _library is not { } lib) return null;
        Say(L.F("library.downloading", r.Title));
        var res = await Task.Run(() => lib.DownloadAsync(r.Course, r.Item, s.Name));
        if (ReferenceEquals(_subject, s)) await RefreshLocalStateAsync();
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
        if (_library is not { } lib) return;
        var files = r.State == ItemLocal.Downloaded ? lib.DownloadedFiles(r.Course, r.Item) : await DownloadAsync(r);
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

    private string? FirstFile(LibraryRow r) => _library?.DownloadedFiles(r.Course, r.Item) is [var f, ..] ? f : null;

    private async Task OpenLocal(string? full)
    {
        if (full is not null && !await Opener.OpenAsync(this, Paths.RelativeToStudy(full))) Say(L.F("common.cantOpen", Path.GetFileName(full)));
    }

    /// <summary>Trang của môn trên web thư viện (môn khớp đầu tiên), không có thì trang chủ thư viện.</summary>
    private Uri? LibraryWeb()
    {
        var index = _library?.Index;
        return _libMatches.Count > 0 && LibraryClient.CourseUrl(index, _libMatches[0].Course) is { } u ? u : LibraryClient.SiteOf(index);
    }

    private Uri? ContributeUrl() => LibraryClient.ContributeUrl(_library?.Index, SoHocTap.Core.Settings.Library.ContributePath);

    private async void OnLibraryWeb(object? sender, RoutedEventArgs e)
    {
        if (LibraryWeb() is { } u) await OpenUrl(u.AbsoluteUri);
    }

    private async void OnContribute(object? sender, RoutedEventArgs e)
    {
        if (ContributeUrl() is { } u) await OpenUrl(u.AbsoluteUri);
    }

    /// <summary>Một dòng báo trong tab (đang tải, đã tải, lỗi); thay bằng dòng mới ở lần sau.</summary>
    private void Say(string text)
    {
        LibraryStatus.Text = text;
        LibraryStatus.IsVisible = text.Length > 0;
    }
}
