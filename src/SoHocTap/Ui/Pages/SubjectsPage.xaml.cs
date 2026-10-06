using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SoHocTap.Core;
using SoHocTap.Files;
using SoHocTap.Shell;
using SoHocTap.Ui.Controls;

namespace SoHocTap.Ui.Pages;

/// <param name="Files">Số tài liệu trên máy (không tính rác); null = chưa quét xong.</param>
internal sealed record SubjectRow(string Name, string Meta, string Group, bool Current, List<LmsCourse> Courses, int? Files = null)
{
    public override string ToString() => Name;   // tên cho screen reader đọc
}
internal sealed record FileRow(string Name, bool Dir, string Kind, long Size, long Modified, int Count)
{
    public string SizeText => Dir ? L.F("subjects.items", Count) : Format.Size(Size);
    public string ModifiedText => Dir ? "" : Format.DateTime(Modified);
    public string Icon => Dir ? "" : "";
    /// <summary>Folder luôn đứng trước file dù sort kiểu gì (giống Explorer).</summary>
    public string SortName => (Dir ? "0" : "1") + Name;
    public long SortSize => Dir ? -1 : Size;
}
internal sealed record RecentRow(string Name, string Folder, string Path, long Size, long Modified)
{
    public string SizeText => Format.Size(Size);
    public string ModifiedText => Format.DateTime(Modified);
}
internal sealed record NewsRow2(string Title, string Author, string Forum, long Time, string Url)
{
    public string When => Format.Ago(Time);
}

public partial class SubjectsPage : UserControl, IPage
{
    private readonly AppHost _host;
    private readonly MainWindow _main;
    private readonly TimelineList DueList;
    private SubjectRow? _subject;
    private string _root = "", _dir = "";
    private List<SubjectRow> _rows = [];

    internal SubjectsPage(AppHost host, MainWindow main)
    {
        InitializeComponent();
        // Trang đang ẩn thì không quét thư mục tài liệu (đồng bộ, đăng nhập đều gọi Refresh); mở lại trang thì quét một lần.
        IsVisibleChanged += (_, _) => { if (IsVisible && _stale) Load(); };
        _host = host;
        _main = main;

        var icon = new DataGridTemplateColumn { Header = "", Width = new DataGridLength(28), SortMemberPath = nameof(FileRow.Dir) };
        var f = new FrameworkElementFactory(typeof(TextBlock));
        f.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(FileRow.Icon)));
        f.SetValue(TextBlock.FontFamilyProperty, FindResource("IconFont"));
        f.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        f.SetValue(TextBlock.MarginProperty, new Thickness(6, 0, 0, 0));
        icon.CellTemplate = new DataTemplate { VisualTree = f };
        var files = Files.Grid;
        files.Columns.Add(Grids.Remember(icon));
        files.Columns.Add(Grids.Text(L.T("col.name"), nameof(FileRow.Name), star: true, sortPath: nameof(FileRow.SortName)));
        files.Columns.Add(Grids.Flex(L.T("col.modified"), nameof(FileRow.ModifiedText), 1.3, 110, sortPath: nameof(FileRow.Modified)));
        files.Columns.Add(Grids.Flex(L.T("col.kind"), nameof(FileRow.Kind), 0.9, 70, sortPath: nameof(FileRow.SortName)));
        files.Columns.Add(Grids.Right(L.T("col.size"), nameof(FileRow.SizeText), 90, nameof(FileRow.SortSize)));
        Files.KeyOf = o => ((FileRow)o).Name;
        Grids.Setup<FileRow>(files, OpenFile, FileMenu);
        files.PreviewKeyDown += (_, e) => { if (e.Key == Key.Back) { e.Handled = true; OnUp(this, new RoutedEventArgs()); } };

        var recentGrid = Recent.Grid;
        recentGrid.Columns.Add(Grids.Text(L.T("col.name"), nameof(RecentRow.Name), star: true));
        recentGrid.Columns.Add(Grids.Flex(L.T("col.folder"), nameof(RecentRow.Folder), 1.2, 100));
        recentGrid.Columns.Add(Grids.Flex(L.T("col.modified"), nameof(RecentRow.ModifiedText), 1, 110, sortPath: nameof(RecentRow.Modified)));
        recentGrid.Columns.Add(Grids.Right(L.T("col.size"), nameof(RecentRow.SizeText), 90, nameof(RecentRow.Size)));
        Recent.KeyOf = o => ((RecentRow)o).Path;
        Grids.Setup<RecentRow>(recentGrid, r => { if (!Documents.Open(r.Path)) _main.Say(L.F("common.cantOpen", r.Name)); }, r =>
        [
            new(L.T("common.showInExplorer"), () => Documents.Reveal(r.Path)),
            new(L.T("common.copyPath"), () => Grids.Copy(Path.Combine(Paths.StudyRoot, r.Path), _main), Separator: true),
        ]);

        var news = News.Grid;
        news.Columns.Add(Grids.Text(L.T("col.title"), nameof(NewsRow2.Title), star: true));
        news.Columns.Add(Grids.Flex(L.T("col.author"), nameof(NewsRow2.Author), 0.9, 90));
        news.Columns.Add(Grids.Flex(L.T("col.forum"), nameof(NewsRow2.Forum), 1, 90));
        news.Columns.Add(Grids.Right(L.T("col.at"), nameof(NewsRow2.When), 100, nameof(NewsRow2.Time)));
        News.KeyOf = o => ((NewsRow2)o).Url;
        Grids.Setup<NewsRow2>(news, n => _host.OpenWeb(n.Url, n.Title), n => [new(L.T("common.copyTitle"), () => Grids.Copy(n.Title, _main), Separator: true)]);

        DueList = new TimelineList(TimelineView.Subject, host, main);
        DueHost.Child = DueList;

        var grades = Grades.Grid;
        grades.Columns.Add(Grids.Flex(L.T("col.class"), nameof(GradeRow.Book), 1, 90));
        grades.Columns.Add(Grids.Text(L.T("col.item"), nameof(GradeRow.Name), star: true));
        grades.Columns.Add(Grids.Right(L.T("col.score"), nameof(GradeRow.GradeText), 70, nameof(GradeRow.SortGrade)));
        grades.Columns.Add(Grids.Right(L.T("col.max"), nameof(GradeRow.MaxText), 80));
        grades.Columns.Add(Grids.Right("%", nameof(GradeRow.Percent), 70));
        grades.LoadingRow += (_, e) => e.Row.Opacity = e.Row.Item is GradeRow { Grade: null } ? 0.55 : 1;
        Grids.Setup<GradeRow>(grades, null, g => [new(L.T("common.copy"), () => Grids.Copy($"{g.Name}\t{g.GradeText}/{g.MaxText}", _main))]);

        SetupLibrary();
    }

    public string Title => L.T("nav.subjects");
    public string Subtitle => L.T("subjects.subtitle");

    public void Open(string arg)
    {
        var name = Uri.UnescapeDataString(arg);
        if (_stale) Load();   // vừa chuyển sang trang (chưa hiện) mà cần chọn môn: dựng danh sách luôn (quét thư mục chạy nền)
        _want = name;
        SelectWanted();
    }

    private string? _want;   // môn cần chọn (Open) khi danh sách chưa có môn đó (đang quét thư mục)

    private void SelectWanted()
    {
        if (_want is null) return;
        foreach (var o in List.Items)
            if (o is SubjectRow r && NameMatch.Same(r.Name, _want)) { _want = null; List.SelectedItem = r; List.ScrollIntoView(r); return; }
    }

    private bool _stale;

    public void Refresh()
    {
        if (!IsVisible) { _stale = true; return; }
        Load();
    }

    private JsonArray? _scan;            // kết quả quét thư mục môn gần nhất (Documents có cache theo mtime)
    private LmsData? _lmsShown;          // dữ liệu LMS đã dựng danh sách
    private int _loadGen;

    /// <summary>
    /// Dựng danh sách môn: ngay từ dữ liệu LMS và lần quét trước (không chặn UI thread), rồi quét thư mục trên thread pool
    /// (Documents.ListSubjectsAsync, cache theo mtime nên mở lại trang mà không đổi gì thì chỉ đi qua các thư mục). Kết quả y như cũ
    /// thì không dựng lại danh sách, không vẽ lại các tab.
    /// </summary>
    private async void Load()
    {
        _stale = false;
        var gen = ++_loadGen;
        var lms = _host.State.Lms;
        if (!ReferenceEquals(lms, _lmsShown) || _rows.Count == 0) Build(lms, _scan);
        try
        {
            var scan = await Documents.ListSubjectsAsync();
            if (gen != _loadGen) return;   // đã có lượt Load mới hơn
            if (_scan is not null && JsonNode.DeepEquals(scan, _scan) && ReferenceEquals(lms, _lmsShown)) return;
            _scan = scan;
            Build(lms, scan);
        }
        // Thư mục tài liệu trên ổ rút ra, mất quyền: giữ danh sách theo LMS, ghi log; lỗi lạ cũng không được làm sập trang (async void).
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Warn($"Quét thư mục môn: {e.Message}"); }
    }

    private void Build(LmsData? lms, JsonArray? scan)
    {
        _lmsShown = lms;
        var term = lms?.Term;
        // So tên theo NFC: thư mục trên đĩa có thể ở dạng NFD, tên môn từ LMS là NFC; không so thì một môn hiện thành hai dòng.
        var map = new Dictionary<string, (string Name, List<LmsCourse> Courses, int Files)>(NameMatch.NfcIgnoreCase);
        foreach (var c in lms?.Courses ?? [])
        {
            if (!map.TryGetValue(c.Subject, out var v)) v = (c.Subject, [], 0);
            v.Courses.Add(c);
            map[c.Subject] = v;
        }
        foreach (var d in (scan ?? []).OfType<JsonObject>())
        {
            var n = d["name"]!.GetValue<string>();
            var files = d["files"]?.GetValue<int>() ?? 0;
            map[n] = map.TryGetValue(n, out var v) ? v with { Files = files } : (n, [], files);
        }
        // Môn kỳ trước chỉ có trên LMS (máy chưa có tài liệu nào) thì ẩn, trừ khi bật đọc cả lớp kỳ trước.
        var pastTerms = Config.Bool("sources.lms.pastTerms", false);
        var rows = map.Values.Where(v => pastTerms || v.Files > 0 || v.Courses.Any(c => c.Term == term)).Select(v =>
        {
            var current = v.Courses.Any(c => c.Term == term);
            var codes = string.Join(", ", v.Courses.Select(c => c.Code.Split('_')[0]).Distinct());
            // Chưa quét xong thì chưa ghi số tài liệu (không ghi "0 tài liệu" sai).
            var docs = scan is null ? "" : L.F("subjects.docs", v.Files);
            return new SubjectRow(v.Name, string.Join(", ", new[] { codes, docs }.Where(x => x.Length > 0)),
                L.T(current ? "subjects.current" : "subjects.past"), current, v.Courses, scan is null ? null : v.Files);
        }).OrderByDescending(r => r.Current).ThenBy(r => r.Name, StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), true)).ToList();
        _rows = rows;
        ShowList();
        SelectWanted();
    }

    private void OnFilter(object sender, TextChangedEventArgs e)
    {
        FilterHint.Visibility = Filter.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowList();
    }

    private void ShowList()
    {
        var q = Filter.Text.Trim();
        // Lọc bỏ dấu: gõ "giai tich" vẫn ra "Giải tích 2", tên NFD trên đĩa cũng khớp.
        var rows = q.Length == 0 ? _rows : _rows.Where(r => NameMatch.ContainsFolded(r.Name, q) || NameMatch.ContainsFolded(r.Meta, q)).ToList();
        var keep = _subject?.Name;
        List.ItemsSource = Grids.Grouped(rows, nameof(SubjectRow.Group));
        if (rows.FirstOrDefault(r => NameMatch.Same(r.Name, keep)) is { } same) List.SelectedItem = same;
        else if (rows.Count > 0 && q.Length > 0) List.SelectedItem = rows[0];
        else if (List.SelectedItem is null && rows.Count > 0) List.SelectedItem = rows[0];
    }

    // ------------------------------------------------------------------ chi tiết môn, tab tải lười

    /// <summary>Tab đã dựng cho môn đang chọn; tab khác chỉ dựng khi người dùng mở (đỡ quét thư mục, đỡ dựng bảng không ai xem).</summary>
    private readonly HashSet<TabItem> _filled = [];

    private void OnPick(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not SubjectRow s) return;
        var changed = _subject?.Name != s.Name;
        _subject = s;
        SubjectName.Text = s.Name;
        LmsButton.IsEnabled = DownloadButton.IsEnabled = s.Courses.Count > 0;
        _root = Config.Str("folders.subjects", "Môn học") + "/" + s.Name;
        if (changed) _dir = _root;
        _filled.Clear();   // môn khác, hay dữ liệu mới (danh sách dựng lại): tab nào mở thì dựng lại tab đó
        ShowMeta(s);
        UpdateLibraryTab(s);
        FillTab();
    }

    private void OnTab(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged của DataGrid bên trong cũng nổi bọt lên đây: chỉ nghe của chính TabControl.
        if (!ReferenceEquals(e.OriginalSource, Tabs)) return;
        FillTab();
    }

    /// <summary>Dựng tab đang chọn nếu chưa dựng cho môn này.</summary>
    private void FillTab()
    {
        if (_subject is not { } s || Tabs.SelectedItem is not TabItem tab || !_filled.Add(tab)) return;
        var st = _host.State;
        // Môn chỉ có tài liệu trên máy (không có lớp LMS) thì các tab LMS nói rõ vậy, không để bảng trống.
        var onLms = s.Courses.Count > 0;
        // Theo tên tab (x:Name), không theo số thứ tự: đổi thứ tự tab trong XAML không làm lệch.
        if (tab == FilesTab)
        {
            if (RecentToggle.IsChecked == true) _ = LoadRecentAsync(s);
            else _ = LoadDirAsync();
        }
        else if (tab == LibraryTab) _ = LoadLibraryAsync(s);
        else if (tab == DueTab)
            // Mọi mốc sắp tới (không cắt ở 60 ngày, DESIGN 6b-3) và hạn LMS đã qua mà chưa làm (cột Còn ghi "quá hạn").
            DueList.Show(st.Timeline.Where(x => Mine(x, s) && (x.Time > Format.Now - 86400 || x.Overdue) && x.Kind != "class").ToList(),
                L.T("subjects.dueEmpty"), st, Src.Lms, Src.Mybk);
        else if (tab == NewsTab)
            News.Show((st.Lms?.Announcements ?? []).Where(a => NameMatch.Same(a.Subject, s.Name))
                .OrderByDescending(a => a.Time).Select(a => new NewsRow2(a.Title, a.Author ?? "", a.Forum, a.Time, a.Url ?? "")).ToList(),
                L.T(onLms ? "subjects.newsEmpty" : "subjects.notOnLms"), st, Src.Lms);
        else if (tab == GradesTab)
            Grades.Show(Books(s).SelectMany(b => b.Items.Select(i => new GradeRow(b.Part ?? L.T("common.theory"), i.Name, i.Grade, i.Max, i.Grade is null ? "" : i.Percent ?? "",
                i.Kind is "course" or "category"))).ToList(), L.T(onLms ? "subjects.gradesEmpty" : "subjects.notOnLms"), st, Src.Lms);
    }

    /// <summary>Tab Tài liệu: đổi giữa thư mục và danh sách mới cập nhật (đường dẫn, nút Lên chỉ có nghĩa với thư mục).</summary>
    private void OnRecentToggle(object sender, RoutedEventArgs e)
    {
        var recent = RecentToggle.IsChecked == true;
        Recent.Visibility = recent ? Visibility.Visible : Visibility.Collapsed;
        Files.Visibility = UpButton.Visibility = PathText.Visibility = recent ? Visibility.Collapsed : Visibility.Visible;
        _filled.Remove(FilesTab);
        FillTab();
    }

    private IEnumerable<LmsGradeBook> Books(SubjectRow s) => (_host.State.Lms?.Grades ?? []).Where(b => NameMatch.Same(b.Subject, s.Name));

    /// <summary>Tab Mới cập nhật: quét cả cây thư mục môn trên thread pool (có cache), bảng hiện "đang đọc" trong lúc chờ.</summary>
    private async Task LoadRecentAsync(SubjectRow s)
    {
        Recent.ShowStatus(DataState.Loading, L.T("subjects.scanning"));
        try
        {
            var recent = await Documents.SubjectFilesAsync(s.Name, 200);
            if (!ReferenceEquals(_subject, s)) return;   // đã chọn môn khác trong lúc quét
            Recent.Show((recent["files"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new RecentRow(
                x["name"]!.GetValue<string>(), x["folder"]?.GetValue<string>() is { } fd && fd != "." ? fd : "",
                x["path"]!.GetValue<string>(), x["size"]?.GetValue<long>() ?? 0, x["modified"]?.GetValue<long>() ?? 0)).ToList(), L.T("subjects.recentEmpty"));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Warn($"Quét tài liệu {s.Name}: {e.Message}");
            if (ReferenceEquals(_subject, s)) Recent.ShowStatus(DataState.Error, L.T("subjects.scanFailed"));
        }
    }

    /// <summary>Thông tin nhanh: mã môn, số tài liệu (từ lần quét danh sách), mốc gần nhất, tổng điểm LMS.</summary>
    private void ShowMeta(SubjectRow s)
    {
        var st = _host.State;
        var next = st.Timeline.FirstOrDefault(x => Mine(x, s) && x.Time > Format.Now && !x.Done && x.Kind != "class");
        var total = Books(s).SelectMany(b => b.Items).FirstOrDefault(i => i.Kind == "course" && i.Grade is not null);
        SubjectMeta.Text = string.Join(", ", new[]
        {
            string.Join(", ", s.Courses.Select(c => c.Code.Split('_')[0]).Distinct()),
            s.Files is { } n ? L.F("subjects.docs", n) : "",
            // Tên đã mở đầu bằng loại (vd. "Quiz 3: ...") thì bỏ chữ loại để khỏi lặp "Quiz Quiz 3".
            next is null ? "" : L.F("subjects.next", next.Name.StartsWith(next.KindName, StringComparison.CurrentCultureIgnoreCase) ? "" : next.KindName, next.Name, next.Left).Trim(),
            total is null ? "" : L.F("subjects.lmsScore", Format.Score(total.Grade), Format.Score(total.Max)),
        }.Where(x => x.Length > 0));
    }

    /// <summary>
    /// Mốc thuộc môn đang chọn: mốc LMS khớp theo id lớp, mốc khác (lịch thi MyBK) khớp đúng tên môn sau khi chuẩn hóa.
    /// Không dùng StartsWith(tên môn): "Giải tích 1" khớp nhầm cả mốc của "Giải tích 12".
    /// </summary>
    private static bool Mine(TimelineItem x, SubjectRow s) =>
        x.Course is { } id ? s.Courses.Any(c => c.Id == id) : NameMatch.SubjectIs(x.Subject, s.Name);

    // ------------------------------------------------------------------ download theo mục

    private void OnDownload(object sender, RoutedEventArgs e)
    {
        if (_subject is null || _subject.Courses.Count == 0) return;
        var courses = _subject.Courses.OrderByDescending(c => c.Term).ToList();
        if (courses.Count == 1) { Download(courses[0]); return; }
        var menu = Grids.Build(courses.Select(c => new MenuEntry($"{c.Term}, {c.Part ?? L.T("common.theory")}, {c.Teacher}", () => Download(c))));
        menu.PlacementTarget = DownloadButton;
        menu.IsOpen = true;
    }

    private void Download(LmsCourse c)
    {
        if (_host.Hub.Get(SourceIds.Lms) is not SoHocTap.Sources.Lms.LmsSource lms) return;
        new DownloadWindow(lms, c) { Owner = Window.GetWindow(this) }.ShowDialog();
        // Có thể vừa thêm tệp: dựng lại tab đang mở và số tài liệu (cache theo mtime tự biết thư mục đã đổi).
        _filled.Clear();
        FillTab();
        Load();
    }

    // ------------------------------------------------------------------ tài liệu

    private int _dirGen;

    /// <summary>Một tầng thư mục, đọc trên thread pool (ổ mạng, ổ USB chậm không làm đứng UI); bảng hiện "đang đọc" nếu lâu.</summary>
    private async Task LoadDirAsync()
    {
        var gen = ++_dirGen;
        var dir = _dir;
        PathText.Text = dir.Replace('/', '\\');
        UpButton.IsEnabled = !dir.Equals(_root, StringComparison.OrdinalIgnoreCase);
        var task = Task.Run(() => Documents.ListDir(dir));
        // Đọc nhanh (thường vài ms) thì không chớp chữ "đang đọc".
        if (await Task.WhenAny(task, Task.Delay(150)) != task && gen == _dirGen) Files.ShowStatus(DataState.Loading, L.T("subjects.scanning"));
        JsonObject d;
        try { d = await task; }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Warn($"Đọc thư mục {dir}: {e.Message}");
            if (gen == _dirGen) Files.ShowStatus(DataState.Error, L.T("subjects.scanFailed"));
            return;
        }
        if (gen != _dirGen) return;   // đã đi sang thư mục khác
        if (d["missing"] is not null) { Files.Show(null, L.T("subjects.filesEmpty")); return; }
        var rows = (d["dirs"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new FileRow(x["name"]!.GetValue<string>(), true, L.T("files.folder"), 0, 0, x["count"]?.GetValue<int>() ?? 0))
            .Concat((d["files"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new FileRow(x["name"]!.GetValue<string>(), false,
                Format.FileKind(x["ext"]?.GetValue<string>() ?? ""), x["size"]?.GetValue<long>() ?? 0, x["modified"]?.GetValue<long>() ?? 0, 0))).ToList();
        Files.Show(rows, L.T("subjects.folderEmpty"));
    }

    private string Full(FileRow f) => _dir + "/" + f.Name;

    private void OpenFile(FileRow f)
    {
        if (f.Dir) { _dir = Full(f); _ = LoadDirAsync(); }
        else if (!Documents.Open(Full(f))) _main.Say(L.F("common.cantOpen", f.Name));
    }

    private IEnumerable<MenuEntry> FileMenu(FileRow f)
    {
        yield return new(L.T("common.showInExplorer"), () => Documents.Reveal(Full(f)));
        yield return new(L.T("common.copyName"), () => Grids.Copy(f.Name, _main), Separator: true);
        yield return new(L.T("common.copyPath"), () => Grids.Copy(Path.Combine(Paths.StudyRoot, Full(f).Replace('/', '\\')), _main));
    }

    private void OnUp(object sender, RoutedEventArgs e)
    {
        if (_dir.Equals(_root, StringComparison.OrdinalIgnoreCase)) return;
        _dir = _dir[.._dir.LastIndexOf('/')];
        _ = LoadDirAsync();
    }

    private void OnReveal(object sender, RoutedEventArgs e)
    {
        if (!Documents.Open(_dir)) _main.Say(L.T("subjects.folderMissing"));
    }

    private void OnOpenLms(object sender, RoutedEventArgs e)
    {
        if (_subject is null) return;
        var courses = _subject.Courses.OrderByDescending(c => c.Term).ToList();
        if (courses.Count == 1) { _host.OpenWeb(courses[0].Url, courses[0].Name); return; }
        var menu = Grids.Build(courses.Select(c => new MenuEntry($"{c.Term}, {c.Part ?? L.T("common.theory")}, {c.Teacher}", () => _host.OpenWeb(c.Url, c.Name))));
        menu.PlacementTarget = LmsButton;
        menu.IsOpen = true;
    }
}

internal sealed record GradeRow(string Book, string Name, double? Grade, double? Max, string Percent, bool Total)
{
    public string GradeText => Format.Score(Grade);
    public string MaxText => Format.Score(Max);
    public double SortGrade => Grade ?? -1;
}
