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

internal sealed record SubjectRow(string Name, string Meta, string Group, bool Current, List<LmsCourse> Courses)
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
internal sealed record CourseRow(string Term, string Name, string Code, string Teacher, string Url, long Id, LmsCourse Course)
{
    public override string ToString() => $"{Term}, {Name}";   // record có LmsCourse lồng bên trong: ToString mặc định rất dài
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

        var due = Due.Grid;
        due.Columns.Add(Grids.Text(L.T("col.when"), nameof(TimelineItem.When), 130, sortPath: nameof(TimelineItem.Time)));
        due.Columns.Add(Grids.Text(L.T("col.name"), nameof(TimelineItem.Name), star: true));
        due.Columns.Add(Grids.Text(L.T("col.kind"), nameof(TimelineItem.KindName), 90));
        due.Columns.Add(Grids.Right(L.T("col.left"), nameof(TimelineItem.Left), 100, nameof(TimelineItem.Time)));
        Due.KeyOf = o => ((TimelineItem)o).Id;
        Grids.Setup<TimelineItem>(due, e => { if (e.Url is { } u) _host.OpenWeb(u, e.Name); });

        var grades = Grades.Grid;
        grades.Columns.Add(Grids.Flex(L.T("col.class"), nameof(GradeRow.Book), 1, 90));
        grades.Columns.Add(Grids.Text(L.T("col.item"), nameof(GradeRow.Name), star: true));
        grades.Columns.Add(Grids.Right(L.T("col.score"), nameof(GradeRow.GradeText), 70, nameof(GradeRow.SortGrade)));
        grades.Columns.Add(Grids.Right(L.T("col.max"), nameof(GradeRow.MaxText), 80));
        grades.Columns.Add(Grids.Right("%", nameof(GradeRow.Percent), 70));
        grades.LoadingRow += (_, e) => e.Row.Opacity = e.Row.Item is GradeRow { Grade: null } ? 0.55 : 1;
        Grids.Setup<GradeRow>(grades, null, g => [new(L.T("common.copy"), () => Grids.Copy($"{g.Name}\t{g.GradeText}/{g.MaxText}", _main))]);

        var courses = Courses.Grid;
        courses.Columns.Add(Grids.Text(L.T("col.term"), nameof(CourseRow.Term), 80));
        courses.Columns.Add(Grids.Text(L.T("col.class"), nameof(CourseRow.Name), star: true));
        courses.Columns.Add(Grids.Flex(L.T("col.code"), nameof(CourseRow.Code), 0.8, 90));
        courses.Columns.Add(Grids.Flex(L.T("col.teacher"), nameof(CourseRow.Teacher), 1, 90));
        Courses.KeyOf = o => ((CourseRow)o).Id;
        Grids.Setup<CourseRow>(courses, c => _host.OpenWeb(c.Url, c.Name), c =>
        [
            new(L.T("subjects.downloadBySection"), () => Download(c.Course)),
            new(L.T("subjects.copyCode"), () => Grids.Copy(c.Code, _main), Separator: true),
        ]);
    }

    public string Title => L.T("nav.subjects");
    public string Subtitle => L.T("subjects.subtitle");

    public void Open(string arg)
    {
        var name = Uri.UnescapeDataString(arg);
        if (_stale) Load();   // vừa chuyển sang trang (chưa hiện) mà cần chọn môn: quét luôn
        foreach (var o in List.Items)
            if (o is SubjectRow r && r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { List.SelectedItem = r; List.ScrollIntoView(r); return; }
    }

    private bool _stale;

    public void Refresh()
    {
        if (!IsVisible) { _stale = true; return; }
        Load();
    }

    private void Load()
    {
        _stale = false;
        var lms = _host.State.Lms;
        var term = lms?.Term;
        var map = new Dictionary<string, (string Name, List<LmsCourse> Courses, int Files)>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in lms?.Courses ?? [])
        {
            if (!map.TryGetValue(c.Subject, out var v)) v = (c.Subject, [], 0);
            v.Courses.Add(c);
            map[c.Subject] = v;
        }
        foreach (var d in Documents.ListSubjects().OfType<JsonObject>())
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
            return new SubjectRow(v.Name, string.Join(", ", new[] { codes, L.F("subjects.docs", v.Files) }.Where(x => x.Length > 0)),
                L.T(current ? "subjects.current" : "subjects.past"), current, v.Courses);
        }).OrderByDescending(r => r.Current).ThenBy(r => r.Name, StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), true)).ToList();
        _rows = rows;
        ShowList();
    }

    private void OnFilter(object sender, TextChangedEventArgs e)
    {
        FilterHint.Visibility = Filter.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowList();
    }

    private void ShowList()
    {
        var q = Filter.Text.Trim();
        var rows = q.Length == 0 ? _rows : _rows.Where(r => r.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || r.Meta.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        var keep = _subject?.Name;
        List.ItemsSource = Grids.Grouped(rows, nameof(SubjectRow.Group));
        if (rows.FirstOrDefault(r => r.Name == keep) is { } same) List.SelectedItem = same;
        else if (rows.Count > 0 && q.Length > 0) List.SelectedItem = rows[0];
        else if (List.SelectedItem is null && rows.Count > 0) List.SelectedItem = rows[0];
    }

    private void OnPick(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not SubjectRow s) return;
        var changed = _subject?.Name != s.Name;
        _subject = s;
        SubjectName.Text = s.Name;
        LmsButton.IsEnabled = DownloadButton.IsEnabled = s.Courses.Count > 0;
        _root = Config.Str("folders.subjects", "Môn học") + "/" + s.Name;
        if (changed) _dir = _root;
        LoadDir();

        var st = _host.State;
        // Môn chỉ có tài liệu trên máy (không có lớp LMS) thì các tab LMS nói rõ vậy, không để bảng trống.
        var onLms = s.Courses.Count > 0;
        Due.Show(st.Timeline.Where(x => x.Subject.StartsWith(s.Name, StringComparison.OrdinalIgnoreCase) && x.Time > Format.Now - 86400
                                        && x.Time < Format.Now + 60 * 86400 && x.Kind != "class").ToList(), L.T("subjects.dueEmpty"), st, Src.Lms, Src.Mybk);
        var books = (st.Lms?.Grades ?? []).Where(b => b.Subject.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
        Grades.Show(books.SelectMany(b => b.Items.Select(i => new GradeRow(b.Part ?? L.T("common.theory"), i.Name, i.Grade, i.Max, i.Grade is null ? "" : i.Percent ?? "",
            i.Kind is "course" or "category"))).ToList(), L.T(onLms ? "subjects.gradesEmpty" : "subjects.notOnLms"), st, Src.Lms);
        Courses.Show(s.Courses.OrderByDescending(c => c.Term).Select(c => new CourseRow(c.Term, c.Part ?? L.T("common.theory"), c.Code, c.Teacher, c.Url, c.Id, c)).ToList(),
            L.T("subjects.notOnLms"), st, Src.Lms);

        var recent = Documents.SubjectFiles(s.Name, 200);
        Recent.Show((recent["files"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new RecentRow(
            x["name"]!.GetValue<string>(), x["folder"]?.GetValue<string>() is { } fd && fd != "." ? fd : "",
            x["path"]!.GetValue<string>(), x["size"]?.GetValue<long>() ?? 0, x["modified"]?.GetValue<long>() ?? 0)).ToList(), L.T("subjects.recentEmpty"));
        News.Show((st.Lms?.Announcements ?? []).Where(a => a.Subject.Equals(s.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.Time).Select(a => new NewsRow2(a.Title, a.Author ?? "", a.Forum, a.Time, a.Url ?? "")).ToList(),
            L.T(onLms ? "subjects.newsEmpty" : "subjects.notOnLms"), st, Src.Lms);

        // Thông tin nhanh: mã môn · số tài liệu · mốc gần nhất · tổng điểm LMS.
        var next = st.Timeline.FirstOrDefault(x => x.Subject.StartsWith(s.Name, StringComparison.OrdinalIgnoreCase) && x.Time > Format.Now && !x.Done && x.Kind != "class");
        var total = books.SelectMany(b => b.Items).FirstOrDefault(i => i.Kind == "course" && i.Grade is not null);
        SubjectMeta.Text = string.Join(", ", new[]
        {
            string.Join(", ", s.Courses.Select(c => c.Code.Split('_')[0]).Distinct()),
            L.F("subjects.docs", recent["total"]?.GetValue<int>() ?? 0),
            // Tên đã mở đầu bằng loại (vd. "Quiz 3: …") thì bỏ chữ loại để khỏi lặp "Quiz Quiz 3".
            next is null ? "" : L.F("subjects.next", next.Name.StartsWith(next.KindName, StringComparison.CurrentCultureIgnoreCase) ? "" : next.KindName, next.Name, next.Left).Trim(),
            total is null ? "" : L.F("subjects.lmsScore", Format.Score(total.Grade), Format.Score(total.Max)),
        }.Where(x => x.Length > 0));
    }

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
        if (_host.Hub.Get("lms") is not SoHocTap.Sources.Lms.LmsSource lms) return;
        new DownloadWindow(lms, c) { Owner = Window.GetWindow(this) }.ShowDialog();
        LoadDir();
        if (_subject is not null) OnPick(this, null!);
    }

    // ------------------------------------------------------------------ tài liệu

    private void LoadDir()
    {
        var d = Documents.ListDir(_dir);
        PathText.Text = _dir.Replace('/', '\\');
        UpButton.IsEnabled = !_dir.Equals(_root, StringComparison.OrdinalIgnoreCase);
        if (d["missing"] is not null) { Files.Show(null, L.T("subjects.filesEmpty")); return; }
        var rows = (d["dirs"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new FileRow(x["name"]!.GetValue<string>(), true, L.T("files.folder"), 0, 0, x["count"]?.GetValue<int>() ?? 0))
            .Concat((d["files"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new FileRow(x["name"]!.GetValue<string>(), false,
                Format.FileKind(x["ext"]?.GetValue<string>() ?? ""), x["size"]?.GetValue<long>() ?? 0, x["modified"]?.GetValue<long>() ?? 0, 0))).ToList();
        Files.Show(rows, L.T("subjects.folderEmpty"));
    }

    private string Full(FileRow f) => _dir + "/" + f.Name;

    private void OpenFile(FileRow f)
    {
        if (f.Dir) { _dir = Full(f); LoadDir(); }
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
        LoadDir();
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
