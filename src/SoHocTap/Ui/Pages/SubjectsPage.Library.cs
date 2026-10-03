using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SoHocTap.Api;
using SoHocTap.Core;
using SoHocTap.Files;
using SoHocTap.Library;
using SoHocTap.Shell;
using SoHocTap.Ui.Controls;

namespace SoHocTap.Ui.Pages;

/// <summary>Một mục thư viện trên bảng. Group là nhãn loại (nhóm kiểu Explorer), GroupOrder giữ thứ tự nhóm.</summary>
internal sealed record LibraryRow(CourseRef Course, LibraryItem Item, string Group, int GroupOrder, string Title, string Edition, string Term,
    string Teacher, string Chapter, string Exam, long Size, ItemLocal State)
{
    public string SizeText => Size > 0 ? Format.Size(Size) : "";
    public string Local => State switch
    {
        ItemLocal.Downloaded => L.T("library.local.downloaded"),
        ItemLocal.Outdated => L.T("library.local.outdated"),
        _ => "",
    };
    public override string ToString() => Title;   // tên cho screen reader đọc
}

internal sealed record FilterOption(string? Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Tab Thư viện của trang Môn học: tài liệu chung của môn (repo bk-study-library), nhóm theo loại, lọc theo giảng viên, học kỳ,
/// loại kiểm tra (chỉ hiện bộ lọc có giá trị). Chỉ lọc trong môn đang chọn nên không cần tìm kiếm mờ (search-core.js của thư viện
/// chỉ cần khi tìm khắp thư viện). Mọi việc đọc đĩa, gọi mạng chạy nền (LibraryService).
/// </summary>
public partial class SubjectsPage
{
    private const int LibraryTabIndex = 6;
    private static readonly StringComparer ViOrder = StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), true);

    private List<LibraryRow> _libRows = [];
    private IReadOnlyList<CourseMatch> _libMatches = [];
    private bool _libFilterReady;
    private int _libGen;
    private DataGridColumn? _editionColumn;

    private void SetupLibrary()
    {
        var g = LibraryItems.Grid;
        g.Columns.Add(Grids.Text(L.T("col.name"), nameof(LibraryRow.Title), star: true));
        _editionColumn = Grids.Flex(L.T("library.col.course"), nameof(LibraryRow.Edition), 0.6, 80);
        g.Columns.Add(_editionColumn);
        g.Columns.Add(Grids.Flex(L.T("col.term"), nameof(LibraryRow.Term), 0.5, 70));
        g.Columns.Add(Grids.Flex(L.T("col.teacher"), nameof(LibraryRow.Teacher), 0.9, 90));
        g.Columns.Add(Grids.Flex(L.T("library.col.chapter"), nameof(LibraryRow.Chapter), 0.5, 70));
        g.Columns.Add(Grids.Flex(L.T("library.col.exam"), nameof(LibraryRow.Exam), 0.5, 70));
        g.Columns.Add(Grids.Right(L.T("col.size"), nameof(LibraryRow.SizeText), 80, nameof(LibraryRow.Size)));
        g.Columns.Add(Grids.Flex(L.T("library.col.local"), nameof(LibraryRow.Local), 0.6, 80));
        g.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        LibraryItems.KeyOf = o => o is LibraryRow r ? r.Course.Id + "/" + r.Item.Id : null;
        // Menu tự có lệnh chính (khác nhau theo loại mục), nên không dùng lệnh "Mở" chung của Grids.
        Grids.Setup<LibraryRow>(g, r => Run(() => DoAsync(r, PrimaryOf(r))), LibraryMenu, _ => true);
        _host.Library.Changed += () => Dispatcher.InvokeAsync(OnLibraryChanged);
    }

    /// <summary>Mã của môn: mã lớp LMS (phần trước "_") và mã trong thời khóa biểu, đăng ký môn trên MyBK (cùng tên môn).</summary>
    private IEnumerable<string?> LibraryCodes(SubjectRow s)
    {
        foreach (var c in s.Courses) yield return LibraryMatch.LmsCode(c.Code);
        var mybk = _host.State.Mybk;
        foreach (var c in mybk?.Schedule ?? [])
            if (NameMatch.Same(c.Name, s.Name)) yield return c.Code;
        foreach (var r in mybk?.Registered ?? [])
            if (NameMatch.Same(r.Name, s.Name)) yield return r.Code;
    }

    /// <summary>Tab chỉ hiện khi đã bật thư viện và môn khớp ít nhất một môn của thư viện. Index chưa đọc thì đọc nền (cache trước).</summary>
    private void UpdateLibraryTab(SubjectRow? s)
    {
        var lib = _host.Library;
        _libMatches = s is not null && LibraryService.Enabled ? lib.Match(LibraryCodes(s)) : [];
        var show = _libMatches.Count > 0;
        LibraryTab.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show && Tabs.SelectedIndex == LibraryTabIndex) Tabs.SelectedIndex = 0;
        if (s is not null && LibraryService.Enabled && lib.Index is null) _ = lib.RefreshAsync(LibraryRefresh.IfDue);
    }

    private void OnLibraryChanged()
    {
        var before = _libMatches;
        UpdateLibraryTab(_subject);
        // Index mới (hoặc vừa tắt/bật): tab đang mở thì dựng lại; tab khác thì lần mở sau tự dựng.
        if (!before.SequenceEqual(_libMatches) || Tabs.SelectedIndex == LibraryTabIndex)
        {
            _filled.Remove(LibraryTabIndex);
            if (Tabs.SelectedIndex == LibraryTabIndex) FillTab();
        }
    }

    private async Task LoadLibraryAsync(SubjectRow s)
    {
        var gen = ++_libGen;
        var matches = _libMatches;
        if (matches.Count == 0) return;
        _ = _host.Library.RefreshAsync(LibraryRefresh.Tab);   // index đổi thì Changed dựng lại tab
        if (_libRows.Count == 0 || !ReferenceEquals(_libShownFor, s)) LibraryItems.ShowStatus(DataState.Loading, L.T("library.loading"));
        var multi = matches.Count > 1;
        var rows = new List<LibraryRow>();
        string? error = null;
        var any = false;
        var cleaned = 0;
        try
        {
            foreach (var m in matches)
            {
                var (r, gone) = await _host.Library.CourseAsync(m.Course, LibraryRefresh.Tab);
                cleaned += gone.Count;
                error ??= r.Error;
                if (r.Value?.Items is not { } items) continue;
                any = true;
                rows.AddRange(await Task.Run(() => items.Where(i => !i.Removed).Select(i => Row(m.Course, i, multi)).ToList()));
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Warn($"Thư viện: đọc môn {s.Name}: {e.Message}");
            error ??= e.Message;
        }
        if (gen != _libGen || !ReferenceEquals(_subject, s)) return;   // đã chọn môn khác trong lúc chờ
        if (cleaned > 0) _main.Say(L.F("library.cleaned", cleaned));
        ContributeButton.Visibility = LibraryClient.ContributeUrl(_host.Library.Index) is null ? Visibility.Collapsed : Visibility.Visible;
        if (_editionColumn is not null) _editionColumn.Visibility = multi ? Visibility.Visible : Visibility.Collapsed;
        ShowLibraryNote(matches, any ? error : null);
        if (!any)
        {
            _libRows = [];
            _libShownFor = s;
            FillFilters();
            if (error is not null) LibraryItems.ShowStatus(DataState.Error, L.F("library.error", error));
            else LibraryItems.Show(null, L.T("library.empty"));
            return;
        }
        _libRows = [.. rows.OrderBy(r => r.GroupOrder).ThenByDescending(r => r.Term, StringComparer.Ordinal).ThenBy(r => r.Title, ViOrder)];
        _libShownFor = s;
        FillFilters();
        ShowLibraryRows();
    }

    private SubjectRow? _libShownFor;

    /// <summary>Dựng một dòng: chạy trên thread pool (LocalState đọc manifest và kiểm file trên đĩa).</summary>
    private LibraryRow Row(CourseRef course, LibraryItem i, bool multi)
    {
        var type = LibraryTypes.GroupOf(i.Type);
        var chapter = i.Chapter is { Length: > 0 } ch ? L.F("library.chapter", ch) : i.Lab is { Length: > 0 } lab ? L.F("library.lab", lab) : "";
        var exam = i.ExamKind is { Length: > 0 } k ? LibraryTypes.ExamKinds.Contains(k) ? L.T("library.exam." + k) : k : "";
        var edition = multi ? course.Edition is { } y ? L.F("library.edition", y) : course.Id : "";
        return new LibraryRow(course, i, L.T("library.type." + type), LibraryTypes.Order(type), BookTitle(i) ?? i.Title ?? i.Id, edition, i.Term ?? "", i.Teacher ?? "",
            chapter, exam, (i.Files ?? []).Sum(f => f.Size), _host.Library.LocalState(course, i));
    }

    /// <summary>Sách tham khảo: "Tên sách (tác giả, năm, NXB)" để nhận ra sách mà không cần mở web.</summary>
    private static string? BookTitle(LibraryItem i)
    {
        if (!i.IsBook || i.Book is not { } b) return null;
        var bits = new List<string>();
        if (b.Authors is { Count: > 0 } a) bits.Add(string.Join(", ", a));
        if (b.Year is { } y) bits.Add(y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(b.Publisher)) bits.Add(b.Publisher!);
        var title = i.Title ?? b.Title ?? i.Id;
        return bits.Count == 0 ? title : $"{title} ({string.Join(", ", bits)})";
    }

    /// <summary>Ghi chú trên bảng: môn đã ngừng có môn thay thế, mã dùng chung cho nhiều môn, đang xem bản cache vì lỗi mạng.</summary>
    private void ShowLibraryNote(IReadOnlyList<CourseMatch> matches, string? staleError)
    {
        var notes = new List<string>();
        foreach (var m in matches.Where(m => m.ReplacementOf is not null))
            notes.Add(L.F("library.replacement", m.ReplacementOf!.Code, m.Course.Code));
        foreach (var g in matches.Where(m => m.ReplacementOf is null).GroupBy(m => LibraryMatch.Key(m.Course.Code)).Where(g => g.Count() > 1))
            notes.Add(L.F("library.sharedCode", g.First().Course.Code, g.Count()));
        if (staleError is not null) notes.Add(L.F("library.stale", staleError));
        LibraryNote.Text = string.Join(" ", notes);
        LibraryNote.Visibility = notes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FillFilters()
    {
        _libFilterReady = false;
        Fill(TeacherFilter, _libRows.Select(r => r.Teacher), "library.filter.allTeachers");
        Fill(TermFilter, _libRows.Select(r => r.Term), "library.filter.allTerms", newestFirst: true);
        Fill(ExamFilter, _libRows.Select(r => r.Exam), "library.filter.allExams");
        _libFilterReady = true;
    }

    /// <summary>Bộ lọc chỉ hiện khi có giá trị; giữ lựa chọn cũ nếu vẫn còn.</summary>
    private static void Fill(ComboBox box, IEnumerable<string> values, string allKey, bool newestFirst = false)
    {
        var keep = (box.SelectedItem as FilterOption)?.Value;
        var distinct = values.Where(v => v.Length > 0).Distinct(StringComparer.Ordinal);
        var opts = (newestFirst ? distinct.OrderByDescending(v => v, StringComparer.Ordinal) : distinct.Order(ViOrder)).Select(v => new FilterOption(v, v)).ToList();
        box.Visibility = opts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        opts.Insert(0, new FilterOption(null, L.T(allKey)));
        box.ItemsSource = opts;
        box.SelectedItem = opts.FirstOrDefault(o => o.Value == keep) ?? opts[0];
    }

    private void OnLibraryFilter(object sender, SelectionChangedEventArgs e)
    {
        if (_libFilterReady) ShowLibraryRows();
    }

    private void ShowLibraryRows()
    {
        static string? Pick(ComboBox b) => b.Visibility == Visibility.Visible ? (b.SelectedItem as FilterOption)?.Value : null;
        string? teacher = Pick(TeacherFilter), term = Pick(TermFilter), exam = Pick(ExamFilter);
        var rows = _libRows.Where(r => (teacher is null || r.Teacher == teacher) && (term is null || r.Term == term) && (exam is null || r.Exam == exam)).ToList();
        LibraryItems.Show(rows.Count == 0 ? rows : Grids.Grouped(rows, nameof(LibraryRow.Group)),
            L.T(_libRows.Count == 0 ? "library.empty" : "library.filteredEmpty"));
    }

    /// <summary>Tải xong hay cài xong: cập nhật cột Trên máy (đọc manifest trên thread pool).</summary>
    private async Task RefreshLocalStateAsync()
    {
        var rows = _libRows;
        var fresh = await Task.Run(() => rows.Select(r => r with { State = _host.Library.LocalState(r.Course, r.Item) }).ToList());
        if (!ReferenceEquals(rows, _libRows)) return;
        _libRows = fresh;
        ShowLibraryRows();
    }

    // ------------------------------------------------------------------ lệnh

    private static LibraryAction PrimaryOf(LibraryRow r) => LibraryActions.Primary(r.Item, r.State);

    private static string Label(LibraryAction a, LibraryRow r) => a switch
    {
        LibraryAction.OpenWeb => L.T("library.openWeb"),
        LibraryAction.Install => L.T("library.install"),
        LibraryAction.Open => L.T("library.openFile"),
        _ => L.T(r.State == ItemLocal.Outdated ? "library.downloadNew" : "library.download"),
    };

    private IEnumerable<MenuEntry> LibraryMenu(LibraryRow r)
    {
        var primary = PrimaryOf(r);
        var web = LibraryClient.ItemUrl(_host.Library.Index, r.Course, r.Item);
        var hasFiles = r.Item.Files is { Count: > 0 };
        yield return new(Label(primary, r), () => Run(() => DoAsync(r, primary)), Primary: true);
        if (primary != LibraryAction.OpenWeb && web is not null) yield return new(L.T("library.openWeb"), () => Links.Open(web.AbsoluteUri));
        if (hasFiles && primary != LibraryAction.Download && r.State != ItemLocal.Downloaded)
            yield return new(Label(LibraryAction.Download, r), () => Run(() => DoAsync(r, LibraryAction.Download)));
        if (r.State != ItemLocal.None && primary != LibraryAction.Open)
            yield return new(L.T("library.openFile"), () => OpenLocal(FirstFile(r)));
        if (r.State != ItemLocal.None)
            yield return new(L.T("common.showInExplorer"), () => Reveal(FirstFile(r)));
        if (r.Item.Book?.Isbn is { Length: > 0 } isbn) yield return new(L.F("library.copyIsbn", isbn), () => Grids.Copy(isbn, _main), Separator: web is null);
        if (web is not null) yield return new(L.T("library.copyLink"), () => Grids.Copy(web.AbsoluteUri, _main), Separator: true);
    }

    /// <summary>Lệnh async từ menu, bấm đúp: lỗi không lường trước chỉ ghi log và báo ở thanh trạng thái, không làm sập app.</summary>
    private async void Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Warn($"Thư viện: {e.Message}");
            _main.Say(L.F("library.failed.other", e.Message));
        }
    }

    private async Task DoAsync(LibraryRow r, LibraryAction action)
    {
        switch (action)
        {
            case LibraryAction.OpenWeb:
                if (LibraryClient.ItemUrl(_host.Library.Index, r.Course, r.Item) is { } u) Links.Open(u.AbsoluteUri);
                break;
            case LibraryAction.Open when r.State == ItemLocal.Downloaded:
                OpenLocal(FirstFile(r));
                break;
            case LibraryAction.Open:
                if (await DownloadAsync(r) is [var file, ..]) OpenLocal(file);
                break;
            case LibraryAction.Download:
                // Tải lần đầu file đọc được (PDF, Markdown) thì mở luôn; tải bản mới thì chỉ báo ở thanh trạng thái.
                if (await DownloadAsync(r) is [var first, ..] && r.State == ItemLocal.None && LibraryActions.OpensAfterDownload(r.Item)) OpenLocal(first);
                break;
            case LibraryAction.Install:
                await InstallAsync(r);
                break;
        }
    }

    /// <summary>Tải các file của mục vào Môn học\&lt;môn&gt;\Thư viện\. Trả về đường dẫn, null nếu lỗi (đã báo người dùng).</summary>
    private async Task<IReadOnlyList<string>?> DownloadAsync(LibraryRow r)
    {
        if (_subject is not { } s) return null;
        _main.Say(L.F("library.downloading", r.Title));
        var res = await Task.Run(() => _host.Library.DownloadAsync(r.Course, r.Item, s.Name));
        if (ReferenceEquals(_subject, s)) await RefreshLocalStateAsync();
        if (res.Ok)
        {
            _main.Say(L.F("library.downloaded", r.Title));
            return res.Paths;
        }
        var text = res.Error switch
        {
            DownloadError.Network => L.F("library.failed.network", r.Title, res.Detail ?? ""),
            DownloadError.Mismatch => L.F("library.failed.mismatch", r.Title),
            DownloadError.TooLarge => L.F("library.failed.tooLarge", r.Title),
            _ => L.F("library.failed.other", r.Title),
        };
        _main.Say("");
        MessageBox.Show(Window.GetWindow(this)!, text, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
        return null;
    }

    /// <summary>
    /// Gói quiz: tải (nếu chưa có bản mới nhất) rồi đi đúng đường Nhập gói của Luyện tập. Gói .json mới thì cài thẳng (PackImport, như
    /// POST /api/packs; validatePack chạy khi Luyện tập nạp gói). .md, .zip hay cập nhật gói đã cài thì mở Luyện tập để người dùng
    /// chọn Nhập file... (khung Luyện tập đọc Markdown/zip và giữ kết quả cũ khi cập nhật).
    /// </summary>
    private async Task InstallAsync(LibraryRow r)
    {
        var files = r.State == ItemLocal.Downloaded ? _host.Library.DownloadedFiles(r.Course, r.Item) : await DownloadAsync(r);
        if (files is not { Count: > 0 }) return;
        var path = files.FirstOrDefault(f => Path.GetExtension(f).ToLowerInvariant() is ".json" or ".md" or ".zip") ?? files[0];
        var owner = Window.GetWindow(this)!;
        var h = await Task.Run(() => QuizPackHandOff.Decide(path, ApiRouter.PackInstalled));
        switch (h.Kind)
        {
            case HandOffKind.Install:
                try { await Task.Run(() => ApiRouter.InstallPack(h.Pack!)); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    MessageBox.Show(owner, L.F("library.installInvalid", Path.GetFileName(path), e.Message), AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _main.Say(L.F("library.installed", r.Title));
                _main.Go("luyen-tap/luyen-tap");   // có tham số thì khung Luyện tập nạp lại, thấy gói mới
                break;
            case HandOffKind.PracticeImport:
                Grids.Copy(path);
                Reveal(path);
                MessageBox.Show(owner, L.F("library.installManual", r.Title), AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
                _main.Go("luyen-tap/luyen-tap");
                break;
            default:
                MessageBox.Show(owner, L.F("library.installInvalid", Path.GetFileName(path), h.Error ?? ""), AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                break;
        }
    }

    private string? FirstFile(LibraryRow r) => _host.Library.DownloadedFiles(r.Course, r.Item) is [var f, ..] ? f : null;

    private void OpenLocal(string? full)
    {
        if (full is not null && !Documents.Open(Paths.RelativeToStudy(full))) _main.Say(L.F("common.cantOpen", Path.GetFileName(full)));
    }

    private static void Reveal(string? full)
    {
        if (full is not null) Documents.Reveal(Paths.RelativeToStudy(full));
    }

    private void OnContribute(object sender, RoutedEventArgs e)
    {
        if (LibraryClient.ContributeUrl(_host.Library.Index) is { } u) Links.Open(u.AbsoluteUri);
    }
}
