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

public partial class SubjectsView : UserControl, IFillPage, ITabbedPage
{
    private readonly AppState _state = null!;
    private readonly Action<IReadOnlyCollection<long>?, bool>? _download;
    private readonly Action<string>? _openLibrary;
    private IReadOnlyList<SubjectRow> _rows = [];
    private SubjectRow? _subject;
    private string _root = "", _dir = "";
    private FolderHistory _history = new();
    private readonly HashSet<TabItem> _filled = [];
    private int _gen;

    public SubjectsView() => InitializeComponent();

    public void HideTitle() { if (PageTitle.Parent is Control head) head.IsVisible = false; }

    private readonly MenuItem DownloadButton = new() { Header = L.T("subjects.download"), IsVisible = false };
    private readonly MenuItem ExamsButton = new() { Header = L.T("subjects.downloadExams"), IsVisible = false };
    private readonly MenuItem LibraryButton = new() { Header = L.T("subjects.openLibrary"), IsVisible = false };
    private readonly MenuItem DownloadAllButton = new() { Header = L.T("subjects.downloadAll"), IsVisible = false };

    private void BuildMore()
    {
        ToolTip.SetTip(ExamsButton, L.T("subjects.downloadExamsTip"));
        ToolTip.SetTip(DownloadAllButton, L.T("subjects.downloadAll.tip"));
        DownloadButton.Click += OnDownload;
        ExamsButton.Click += OnDownloadExams;
        LibraryButton.Click += OnOpenLibrary;
        DownloadAllButton.Click += OnDownloadAll;
        MoreButton.Flyout = new MenuFlyout
        {
            Placement = Avalonia.Controls.PlacementMode.BottomEdgeAlignedRight,
            Items = { DownloadButton, ExamsButton, LibraryButton, new Separator(), DownloadAllButton },
        };
        MoreButton.IsVisible = DownloadButton.IsVisible || LibraryButton.IsVisible;
    }

    private Func<string, Control?>? _practice;

    internal SubjectsView(AppState state, SubjectsView? previous, Action<IReadOnlyCollection<long>?, bool>? download, Action<string>? openLibrary,
        Func<string, Control?>? practice = null) : this()
    {
        _practice = practice;
        PracticeTab.IsVisible = practice is not null;
        _keepDir = previous?._dir;
        if (previous is not null) _history = previous._history;
        if (previous is not null) Tabs.SelectedIndex = previous.Tabs.SelectedIndex;
        _state = state;
        _download = download;
        _openLibrary = openLibrary;
        Files.AddHandler(PointerPressedEvent, OnFilesPointer, RoutingStrategies.Tunnel, handledEventsToo: true);
        RowMenus.Attach(Files, OnFileMenu);
        RowMenus.Attach(Recent, OnRecentMenu);
        RowMenus.Attach(Media, OnMediaMenu);
        RowMenus.Attach(Due, OnDueMenu);
        Subjects.ContainerPrepared += (_, e) =>
        {
            var header = Subjects.Items.Count > e.Index && Subjects.Items[e.Index] is SubjectTermHeader;
            e.Container.IsEnabled = e.Container.Focusable = !header;
        };
        DownloadButton.IsVisible = DownloadAllButton.IsVisible = ExamsButton.IsVisible = download is not null;
        LibraryButton.IsVisible = openLibrary is not null;
        BuildMore();
        _wanted = previous?.Selected;
        _termKey = previous?._termKey ?? (Config.Str("ui.subjectsTerms", "current") == "all" ? SubjectsPresenter.AllTerms : null);
        Build(null);
        _ = ScanAsync();
    }

    private string? _wanted;
    private string? _keepDir;

    public void SelectPractice() => Tabs.SelectedItem = PracticeTab;

    public void SelectTab(int index) => Tabs.SelectedIndex = Math.Clamp(index, 0, Tabs.ItemCount - 1);

    public int Tab { get => Tabs.SelectedIndex; set => SelectTab(value); }

    public string? Selected => _subject?.Name;

    public void SelectSubject(string name)
    {
        _wanted = name;
        if (string.IsNullOrEmpty(Filter.Text)) ShowList();
        else Filter.Text = "";
    }

    private async Task ScanAsync()
    {
        try { Build(await Documents.ListSubjectsAsync()); }
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Warn($"Quét thư mục môn: {e.Message}"); }
    }

    private void Build(JsonArray? scan)
    {
        _wanted ??= _subject?.Name;
        _rows = SubjectsPresenter.WithDue(SubjectsPresenter.Rows(_state.Lms, scan), _state.Timeline, Format.Now, Config.Int("subjects.dueDays", 7));
        ShowList();
    }

    private void OnFilter(object? sender, TextChangedEventArgs e) => ShowList();

    private IReadOnlyList<(string Key, string Title)> _terms = [];
    private string? _termKey;
    private bool _fillingTerms;

    private void FillTerms()
    {
        _terms = SubjectsPresenter.TermOptions(_rows);
        var index = Math.Max(0, _terms.ToList().FindIndex(t => t.Key == _termKey));
        _termKey = _terms.Count > 0 ? _terms[index].Key : null;
        _fillingTerms = true;
        TermFilter.ItemsSource = _terms.Select(t => t.Title).ToList();
        TermFilter.SelectedIndex = _terms.Count > 0 ? index : -1;
        _fillingTerms = false;
        TermFilter.IsVisible = _terms.Count > 2;
    }

    private void OnTermFilter(object? sender, SelectionChangedEventArgs e)
    {
        if (_fillingTerms || TermFilter.SelectedIndex < 0 || TermFilter.SelectedIndex >= _terms.Count) return;
        _termKey = _terms[TermFilter.SelectedIndex].Key;
        var remember = _termKey == SubjectsPresenter.AllTerms ? "all" : "current";
        if (Config.Str("ui.subjectsTerms", "current") != remember) Config.Set("ui.subjectsTerms", remember);
        ShowList();
    }

    private void ShowList()
    {
        if (_wanted is { } want && _termKey != SubjectsPresenter.AllTerms && _rows.FirstOrDefault(r => NameMatch.Same(r.Name, want)) is { } target
            && SubjectsPresenter.TermOf(target) != _termKey)
            _termKey = SubjectsPresenter.TermOf(target);
        FillTerms();
        var text = Filter.Text ?? "";
        var found = SubjectsPresenter.Filter(_rows, text);
        var all = text.Trim().Length > 0 || _termKey == SubjectsPresenter.AllTerms;
        var rows = all ? found : SubjectsPresenter.InTerm(found, _termKey ?? SubjectsPresenter.AllTerms);
        Subjects.ItemsSource = all ? SubjectsPresenter.Grouped(rows) : rows;
        ListEmpty.IsVisible = rows.Count == 0;
        ListEmpty.Text = L.T(_rows.Count == 0 ? "subjects.listEmpty" : "subjects.noMatch");
        var keep = _wanted ?? _subject?.Name;
        Subjects.SelectedItem = rows.FirstOrDefault(r => NameMatch.Same(r.Name, keep)) ?? rows.FirstOrDefault();
        _wanted = null;
        DetailCard.IsVisible = Subjects.SelectedItem is not null;
    }

    public static Action<IReadOnlyList<long>>? CoursesOpened { get; set; }

    private void OnPick(object? sender, SelectionChangedEventArgs e)
    {
        if (Subjects.SelectedItem is not SubjectRow s) return;
        var changed = _subject?.Name != s.Name;
        if (changed && s.Current && s.OnLms) CoursesOpened?.Invoke([.. s.Courses.Select(c => c.Id)]);
        if (changed && _subject is not null) Tabs.SelectedIndex = 0;
        _subject = s;
        SubjectName.Text = s.Name;
        SubjectMeta.Text = SubjectsPresenter.Meta(s, _state.Timeline, _state.Lms, Format.Now);
        LmsButton.IsEnabled = DownloadButton.IsEnabled = ExamsButton.IsEnabled = s.OnLms;
        _root = Config.Str("folders.subjects") + "/" + s.Name;
        if (!_history.Root.Equals(_root, StringComparison.OrdinalIgnoreCase)) _history.Reset(_root);
        if (changed) _dir = _keepDir is { } keep && keep.StartsWith(_root, StringComparison.OrdinalIgnoreCase) ? keep : _root;
        _keepDir = null;
        _filled.Clear();
        DetailCard.IsVisible = true;
        FillTab();
    }

    private void OnTab(object? sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, Tabs)) FillTab();
    }

    private void FillTab()
    {
        if (_subject is not { } s || Tabs.SelectedItem is not TabItem tab || !_filled.Add(tab)) return;
        if (tab == OverviewTab) ShowOverview(s);
        else if (tab == FilesTab)
        {
            if (RecentToggle.IsChecked == true) _ = LoadRecentAsync(s);
            else _ = LoadDirAsync();
        }
        else if (tab == DueTab)
            Tables.Show(Due, DueEmpty, SubjectsPresenter.Due(_state.Timeline, s, Format.Now), L.T("subjects.dueEmpty"));
        else if (tab == NewsTab)
            Tables.Show(News, NewsEmpty, SubjectsPresenter.News(_state.Lms, s), L.T(s.OnLms ? "subjects.newsEmpty" : "subjects.notOnLms"));
        else if (tab == MediaTab)
            ShowMedia(s);
        else if (tab == PracticeTab)
            PracticeSlot.Content = _practice?.Invoke(s.Name);
        else if (tab == GradesTab)
            Tables.Show(Grades, GradesEmpty, SubjectsPresenter.Grades(_state.Lms, s), L.T(s.OnLms ? "subjects.gradesEmpty" : "subjects.notOnLms"));
    }

    private void ShowOverview(SubjectRow s)
    {
        OverviewPanel.Children.Clear();
        var o = SubjectsPresenter.Overview(_state.Timeline, _state.Lms, _state.Mybk, s, Format.Now);
        StackPanel Section(string title, TabItem? more)
        {
            var head = new DockPanel();
            if (more is not null)
            {
                var go = new HyperlinkButton { Content = L.T("subjects.overview.more"), Padding = new Thickness(0) };
                go.Click += (_, _) => Tabs.SelectedItem = more;
                DockPanel.SetDock(go, Dock.Right);
                head.Children.Add(go);
            }
            head.Children.Add(new TextBlock { Text = title, Classes = { "card-title" } });
            var box = new StackPanel { Spacing = 6 };
            box.Children.Add(head);
            OverviewPanel.Children.Add(box);
            return box;
        }
        void Line(StackPanel box, string main, string right, Action? open = null)
        {
            var row = new DockPanel();
            var r = new TextBlock { Text = right, Classes = { "meta" }, Margin = new Thickness(12, 0, 0, 0) };
            DockPanel.SetDock(r, Dock.Right);
            row.Children.Add(r);
            Control text = open is null ? new TextBlock { Text = main, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis }
                : new HyperlinkButton { Content = new TextBlock { Text = main, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis }, Padding = new Thickness(0) };
            if (text is HyperlinkButton h) h.Click += (_, _) => open!();
            row.Children.Add(text);
            box.Children.Add(row);
        }
        void Empty(StackPanel box, string text) => box.Children.Add(new TextBlock { Text = text, Classes = { "meta" } });

        var due = Section(L.T("subjects.overview.due"), DueTab);
        foreach (var d in o.Due) Line(due, d.Name, d.Left);
        if (o.Due.Count == 0) Empty(due, L.T("subjects.dueEmpty"));
        var news = Section(L.T("subjects.overview.news"), NewsTab);
        foreach (var n in o.News) Line(news, n.Title, n.When, n.Read is { } read ? () => ReaderWindow.Open(this, read) : null);
        if (o.News.Count == 0) Empty(news, L.T(s.OnLms ? "subjects.newsEmpty" : "subjects.notOnLms"));
        var grades = Section(L.T("subjects.overview.grades"), GradesTab);
        foreach (var g in o.Grades) Line(grades, g, "");
        if (o.Grades.Count == 0) Empty(grades, L.T("subjects.overview.noGrades"));
    }

    private static void Status(DataGrid grid, TextBlock empty, string text)
    {
        grid.IsVisible = false;
        empty.Text = text;
        empty.IsVisible = true;
    }

    private async Task LoadDirAsync()
    {
        var gen = ++_gen;
        var dir = _dir;
        ShowPath(dir);
        UpButton.IsEnabled = _history.CanGoBack(dir);
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
        _dirRows = SubjectsPresenter.FileRows(d);
        ShowDir();
    }

    private FileGroup _group = FileGroup.All;
    private IReadOnlyList<FileRow> _dirRows = [];

    private void ShowDir() =>
        Tables.Show(Files, FilesEmpty, FileFilter.Apply(_dirRows, FileSearch.Text ?? "", _group), L.T(Filtering ? "subjects.noFileMatch" : "subjects.folderEmpty"));

    private bool Filtering => (FileSearch.Text ?? "").Trim().Length > 0 || _group != FileGroup.All;

    private void OnFileSearch(object? sender, TextChangedEventArgs e)
    {
        if ((FileSearch.Text ?? "").Trim().Length > 0 && RecentToggle.IsChecked != true) { SetView(all: true); return; }
        if (RecentToggle.IsChecked == true) ShowRecent();
        else ShowDir();
    }

    private void OnKind(object? sender, RoutedEventArgs e)
    {
        if (sender is not Avalonia.Controls.Primitives.ToggleButton b || !Enum.TryParse<FileGroup>(b.Tag as string, out var g)) return;
        _group = g;
        foreach (var t in KindBar.Children.OfType<Avalonia.Controls.Primitives.ToggleButton>()) t.IsChecked = ReferenceEquals(t, b);
        if (RecentToggle.IsChecked == true) ShowRecent();
        else ShowDir();
    }

    private void OnViewAll(object? sender, RoutedEventArgs e) => SetView(all: true);

    private void OnViewFolders(object? sender, RoutedEventArgs e) => SetView(all: false);

    private void SetView(bool all)
    {
        RecentToggle.IsChecked = all;
        FolderToggle.IsChecked = !all;
        Recent.IsVisible = UnreadOnly.IsVisible = all;
        Files.IsVisible = FolderBar.IsVisible = !all;
        _filled.Remove(FilesTab);
        FillTab();
    }

    private async Task LoadRecentAsync(SubjectRow s)
    {
        var gen = ++_gen;
        _recent = null;
        Status(Recent, FilesEmpty, L.T("subjects.scanning"));
        try
        {
            var recent = await Documents.SubjectFilesAsync(s.Name, 1000);
            if (gen != _gen) return;
            _recent = recent;
            ShowRecent();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Warn($"Quét tài liệu {s.Name}: {e.Message}");
            if (gen == _gen) Status(Recent, FilesEmpty, L.T("subjects.scanFailed"));
        }
    }

    private System.Text.Json.Nodes.JsonObject? _recent;

    private void ShowRecent()
    {
        if (_recent is null) return;
        var unread = UnreadOnly.IsChecked == true;
        var rows = FileFilter.Apply(SubjectsPresenter.RecentRows(_recent, DocReads.Marks, unread), FileSearch.Text ?? "", _group);
        Tables.Show(Recent, FilesEmpty, rows, L.T(Filtering ? "subjects.noFileMatch" : unread ? "subjects.allRead" : "subjects.recentEmpty"));
    }

    private void OnUnreadOnly(object? sender, RoutedEventArgs e) => ShowRecent();

    private void MarkRead(string path, long modified, bool read)
    {
        DocReads.Set(path, modified, read);
        ShowRecent();
    }

    private void OnUp(object? sender, RoutedEventArgs e) => GoBack();

    private void GoBack()
    {
        if (_history.Back(_dir) is not { } dir) return;
        _dir = dir;
        _ = LoadDirAsync();
    }

    private void GoTo(string dir)
    {
        _history.Went(_dir, dir);
        _dir = dir;
        _ = LoadDirAsync();
    }

    private void OnFilesPointer(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Files).Properties.IsXButton1Pressed) return;
        GoBack();
        e.Handled = true;
    }

    private void ShowPath(string dir)
    {
        PathBar.Children.Clear();
        var crumbs = SubjectsPresenter.Crumbs(_root, dir);
        for (var i = 0; i < crumbs.Count; i++)
        {
            var (name, path) = crumbs[i];
            if (i > 0) PathBar.Children.Add(new TextBlock { Text = "/", Classes = { "meta" }, Margin = new Thickness(4, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            if (i == crumbs.Count - 1)
            {
                PathBar.Children.Add(new TextBlock { Text = name, Classes = { "meta" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
                continue;
            }
            var link = new HyperlinkButton { Content = new TextBlock { Text = name }, Padding = new Thickness(2, 0) };
            link.Click += (_, _) => GoTo(path);
            PathBar.Children.Add(link);
        }
    }

    private async void OnOpenFolder(object? sender, RoutedEventArgs e) => await Opener.OpenAsync(this, _dir);

    private async void OnFileOpen(object? sender, TappedEventArgs e)
    {
        if (Files.SelectedItem is not FileRow f) return;
        if (f.Dir) GoTo(_dir + "/" + f.Name);
        else
        {
            DocReads.Set(_dir + "/" + f.Name, f.Modified, read: true);
            await Opener.OpenAsync(this, _dir + "/" + f.Name);
        }
    }

    private void OnFilesKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Back) { GoBack(); e.Handled = true; }
        else if (e.Key == Key.Enter) { OnFileOpen(sender, null!); e.Handled = true; }
    }

    private async void OnRecentOpen(object? sender, TappedEventArgs e)
    {
        if (Recent.SelectedItem is not RecentRow r) return;
        MarkRead(r.Path, r.Modified, read: true);
        await Opener.OpenAsync(this, r.Path);
    }

    private void OnFileMenu(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout m) return;
        m.Items.Clear();
        if (Files.SelectedItem is not FileRow f)
        {
            m.Items.Add(Item(L.T("subjects.back"), () => GoBack(), _history.CanGoBack(_dir)));
            m.Items.Add(Item(L.T("subjects.openFolder"), () => _ = Opener.OpenAsync(this, _dir)));
            m.Items.Add(Item(L.T("subjects.refresh"), () => _ = LoadDirAsync()));
            return;
        }
        var rel = _dir + "/" + f.Name;
        if (f.Dir)
        {
            m.Items.Add(Item(L.T("common.open"), () => GoTo(rel)));
            m.Items.Add(Item(L.T("subjects.openInFiles"), () => _ = Opener.OpenAsync(this, rel)));
        }
        else
        {
            m.Items.Add(Item(L.T("common.open"), () => { DocReads.Set(rel, f.Modified, read: true); _ = Opener.OpenAsync(this, rel); }));
            m.Items.Add(Item(L.T("files.reveal"), () => _ = Opener.RevealAsync(this, rel)));
            var read = DocReads.Marks.IsRead(rel, f.Modified);
            m.Items.Add(Item(L.T(read ? "subjects.markUnread" : "subjects.markRead"), () => DocReads.Set(rel, f.Modified, !read)));
        }
        m.Items.Add(new Separator());
        m.Items.Add(Item(L.T("common.copyName"), () => _ = Copy(f.Name)));
        m.Items.Add(Item(L.T("common.copyPath"), () => _ = Copy(Paths.StudyPath(rel) ?? rel)));

        static MenuItem Item(string header, Action run, bool enabled = true)
        {
            var i = new MenuItem { Header = header, IsEnabled = enabled };
            i.Click += (_, _) => run();
            return i;
        }
    }

    private void OnRecentMenu(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout m) return;
        Fill(m, Recent.SelectedItem is RecentRow r ? (r.Name, r.Path) : null);
        if (Recent.SelectedItem is not RecentRow row) return;
        var mark = new MenuItem { Header = L.T(row.Read ? "subjects.markUnread" : "subjects.markRead") };
        mark.Click += (_, _) => MarkRead(row.Path, row.Modified, !row.Read);
        m.Items.Insert(0, mark);
    }

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

    private async void OnDueOpen(object? sender, TappedEventArgs e)
    {
        if (Due.SelectedItem is CalendarRow { Url: { } url }) await OpenUrl(url);
    }

    private void OnDueMenu(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout menu) return;
        menu.Items.Clear();
        if (Due.SelectedItem is not CalendarRow row) return;
        if (_state.Lms?.Events.FirstOrDefault(x => x.Id == row.Id) is { } ev && SubjectsPresenter.IntroReading(ev) is { } intro)
        {
            var read = new MenuItem { Header = L.T("reader.intro") };
            read.Click += (_, _) => ReaderWindow.Open(this, intro);
            menu.Items.Add(read);
        }
        if (row.Url is { Length: > 0 } url)
        {
            var open = new MenuItem { Header = L.T("reader.openLms") };
            open.Click += async (_, _) => await OpenUrl(url);
            menu.Items.Add(open);
        }
    }

    private async void OnNewsOpen(object? sender, TappedEventArgs e)
    {
        if (News.SelectedItem is SubjectNews { Read: { } read }) ReaderWindow.Open(this, read);
        else if (News.SelectedItem is SubjectNews { Url: { } url }) await OpenUrl(url);
    }

    private static readonly SoHocTap.Sources.Lms.MediaKind?[] MediaKinds =
        [null, SoHocTap.Sources.Lms.MediaKind.Video, SoHocTap.Sources.Lms.MediaKind.Link, SoHocTap.Sources.Lms.MediaKind.Page, SoHocTap.Sources.Lms.MediaKind.Tool];

    private void ShowMedia(SubjectRow s)
    {
        if (MediaKindFilter.ItemCount == 0)
        {
            MediaKindFilter.ItemsSource = MediaKinds.Select(k => L.T(k is { } kind ? "subjects.media." + kind.ToString().ToLowerInvariant() : "subjects.media.all")).ToList();
            MediaKindFilter.SelectedIndex = 0;
        }
        ReadPastButton.IsVisible = s.OnLms && !s.Current;
        var only = MediaKinds[Math.Max(0, MediaKindFilter.SelectedIndex)];
        var rows = SubjectsPresenter.Media(s, LmsCourseStore.Read, DocReads.Marks, only);
        Tables.Show(Media, MediaEmpty, Tables.Grouped(rows, nameof(MediaRow.Group)),
            L.T(!s.OnLms ? "subjects.notOnLms" : s.Current ? "subjects.mediaEmpty" : "subjects.mediaEmptyPast"));
    }

    private void OnMediaKind(object? sender, SelectionChangedEventArgs e)
    {
        if (_subject is { } s && MediaKindFilter.SelectedIndex >= 0) ShowMedia(s);
    }

    private void MarkSeen(MediaRow m, bool seen)
    {
        DocReads.Set(SubjectsPresenter.MediaKey(m.Url), 0, seen);
        if (_subject is { } s) ShowMedia(s);
    }

    private async void OnMediaOpen(object? sender, TappedEventArgs e)
    {
        if (Media.SelectedItem is not MediaRow { Url.Length: > 0 } m) return;
        MarkSeen(m, true);
        if (m.Kind == SoHocTap.Sources.Lms.MediaKind.Page && _subject is { } s && SubjectsPresenter.PageReading(m.Url, m.Name, s.Name) is { } page)
        {
            ReaderWindow.Open(this, page);
            return;
        }
        await OpenUrl(m.Url);
    }

    private void OnMediaMenu(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout menu) return;
        menu.Items.Clear();
        if (Media.SelectedItem is not MediaRow m) return;
        var mark = new MenuItem { Header = L.T(m.Seen ? "subjects.media.markUnseen" : "subjects.media.markSeen") };
        mark.Click += (_, _) => MarkSeen(m, !m.Seen);
        var copy = new MenuItem { Header = L.T("common.copyLink") };
        copy.Click += async (_, _) => await Copy(m.Url);
        menu.Items.Add(mark);
        menu.Items.Add(copy);
    }

    private async void OnReadPast(object? sender, RoutedEventArgs e)
    {
        if (_subject is not { } s) return;
        ReadPastButton.IsEnabled = false;
        MediaStatus.Text = L.T("subjects.readPastRunning");
        try
        {
            var n = await SoHocTap.Sources.Lms.LmsSource.ReadStructuresAsync([.. s.Courses.Select(c => c.Id)], CancellationToken.None);
            MediaStatus.Text = L.F("subjects.readPastDone", n);
        }
        catch (Exception x) when (x is not OutOfMemoryException)
        {
            Log.Warn($"Đọc lớp kỳ cũ {s.Name}: {x.Message}");
            MediaStatus.Text = L.F("subjects.readPastFailed", x.Message);
        }
        finally { ReadPastButton.IsEnabled = true; }
        if (_subject == s) ShowMedia(s);
    }

    private void OnMediaKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        OnMediaOpen(sender, null!);
        e.Handled = true;
    }

    private void OnOpenLms(object? sender, RoutedEventArgs e) => PickCourse(LmsButton, c => _ = OpenUrl(c.Url));

    private void OnDownload(object? sender, RoutedEventArgs e)
    {
        if (_subject is { OnLms: true } s) _download?.Invoke([.. s.Courses.Select(c => c.Id)], false);
    }

    private void OnDownloadAll(object? sender, RoutedEventArgs e) => _download?.Invoke(null, false);

    private void OnDownloadExams(object? sender, RoutedEventArgs e)
    {
        if (_subject is { OnLms: true } s) _download?.Invoke([.. s.Courses.Select(c => c.Id)], true);
    }

    private void OnOpenLibrary(object? sender, RoutedEventArgs e)
    {
        if (_subject is { } s) _openLibrary?.Invoke(s.Name);
    }

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
