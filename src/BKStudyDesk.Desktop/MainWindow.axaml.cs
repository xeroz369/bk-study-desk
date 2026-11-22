using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using BKStudyDesk.Desktop.Views;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop;

public partial class MainWindow : Window
{
    private readonly AppHost _host = new();
    private readonly (string Title, Func<Control> Build)[] _pages;
    private int _current;
    private int _shownIndex = -1;
    private const int NavCount = 4;
    private SubjectsView? _subjects;
    private LibraryView? _library;
    private bool _showLibrary;
    private string? _libraryPick;
    private Views.Practice.PracticeHost? _practice;
    private InboxView? _inbox;

    public MainWindow()
    {
        InitializeComponent();
        _pages =
        [
            (L.T("nav.today"), () => new HomeView(_host.State, Login, OpenInbox, () => Show(PageOf("luyen-tap")))),
            ("LMS", () => new SubjectsHost(_showLibrary,
                () => _subjects = new SubjectsView(_host.State, _subjects, Download, OpenLibrary, PracticeFor),
                () =>
                {
                    _library = new LibraryView(_host.State, _host.Library, _library);
                    if (_libraryPick is { } pick) { _library.SelectSubject(pick); _libraryPick = null; }
                    return _library;
                },
                on => _showLibrary = on)),
            ("MyBK", () => new MybkView(_host.State, () => { SoHocTap.Sources.Mybk.MybkSource.RequestRefresh(); _host.Hub.Start(SourceIds.Mybk, force: true); })),
            (L.T("nav.calendar"), () => new CalendarView(_host.State)),
            (L.T("nav.practice"), () => PracticeFor(null)),
            (L.T("nav.settings"), () => new SettingsView(SettingsActions())),
        ];
        for (var i = 0; i < NavCount; i++)
        {
            var index = i;
            var item = new Button { Classes = { "bar" }, Content = BarLabel(_pages[i].Title) };
            item.Click += (_, _) => Show(index);
            Nav.Children.Add(item);
        }
        for (var i = 0; i < _pages.Length && i < 9; i++)
        {
            var index = i;
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.D1 + i, KeyModifiers.Control), Command = new Command(() => Show(index)) });
        }
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F5), Command = new Command(SyncNow) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Left, KeyModifiers.Alt), Command = new Command(Back) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.K, KeyModifiers.Control), Command = new Command(OpenQuick) });
        ApplyZoom(Config.Int("app.zoom", Config.Int("practice.zoom", 100)));
        foreach (var (key, step) in new[] { (Key.OemPlus, 1), (Key.Add, 1), (Key.OemMinus, -1), (Key.Subtract, -1), (Key.D0, 0), (Key.NumPad0, 0) })
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(key, KeyModifiers.Control), Command = new Command(() => ZoomStep(step)) });
        SubjectsView.CoursesOpened = _host.CheckCoursesOnOpen;
        AddHandler(PointerMovedEvent, (_, _) => UserActivity.Touch(), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, (_, _) => UserActivity.Touch(), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        Activated += (_, _) => UserActivity.SetFocused(true);
        Deactivated += (_, _) => UserActivity.SetFocused(false);
        Scroller.AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            ZoomStep(e.Delta.Y > 0 ? 1 : -1);
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        SettingsButton.Click += (_, _) => Show(SettingsPage);
        MoreSettings.Click += (_, _) => Show(SettingsPage);
        InboxButton.Click += (_, _) => ToggleInbox();
        SearchButton.Click += (_, _) => OpenQuick();
        Drawer.Width = InboxView.DrawerWidth;
        DrawerScrim.PointerPressed += (_, _) => CloseInbox();
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Escape), Command = new Command(() => { if (Drawer.IsVisible) CloseInbox(); }) });
        OfflineMode.Changed += _ => Avalonia.Threading.Dispatcher.UIThread.Post(ShowSync);
        _host.State.Changed += () => { if (_current != SettingsPage) Show(_current); ShowSync(); UpdateInbox(popup: true); };
        _host.State.StatusChanged += ShowSync;
        _host.State.Progress += ShowSync;
        var moreMenu = (MenuFlyout)More.Flyout!;
        moreMenu.Opening += (_, _) => FillMore(moreMenu);
        _host.LoginProgress += OnLoginProgress;
        _host.State.Changed += ShowInfo;
        _host.MessagesChanged += () =>
        {
            if (_current == PageOf("hom-nay")) Show(_current);
            if (Drawer.IsVisible) _inbox?.Reload(_host.State.Lms);
            UpdateInbox(popup: true);
        };
        _inboxPopHide.Tick += (_, _) => { _inboxPopHide.Stop(); _inboxPop?.Hide(); };
        UpdateInbox(popup: false);
        _host.State.StatusChanged += ShowInfo;
        InfoClose.Click += (_, _) => { _infoClosed = _info?.Key; _loginStage = ""; ShowInfo(); };
        InfoPrimary.Click += (_, _) => Run(_info?.Primary ?? InfoAction.None);
        InfoSecondary.Click += (_, _) => Run(_info?.Secondary ?? InfoAction.None);
        _infoHide.Tick += OnInfoHide;
        var statusFlyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        statusFlyout.Opening += (_, _) => statusFlyout.Content = StatusPanelContent(statusFlyout);
        statusFlyout.Opened += (_, _) => StatusButton.Classes.Set("on", true);
        statusFlyout.Closed += (_, _) => { StatusButton.Classes.Set("on", false); ShowSync(); };
        StatusButton.Flyout = statusFlyout;
        _notices.Changed += ShowSync;
        Bar.SizeChanged += (_, _) => FitBar();
        Closed += (_, _) => _host.Dispose();
        _host.OpenUrl += u => _ = Files.Links.OpenAsync(this, u.AbsoluteUri);
        _host.Start();
        SetupTray();
        Platform.SingleInstance.Listen(() => Avalonia.Threading.Dispatcher.UIThread.Post(ShowFromTray));
        AppEvents.UnhandledError += e => Avalonia.Threading.Dispatcher.UIThread.Post(() => _notices.Add(new Notice(Format.Now, L.T("error.unhandled"), e.Message, "")));
        var args = Environment.GetCommandLineArgs();
        var snapping = _snapping = args.Any(a => a.StartsWith("--snap=", StringComparison.Ordinal));
        if (!snapping) _placement.Apply(this);
        if (args.FirstOrDefault(a => a.StartsWith("--size=", StringComparison.Ordinal))?[7..].Split('x') is [var sw, var sh]
            && double.TryParse(sw, System.Globalization.CultureInfo.InvariantCulture, out var w) && double.TryParse(sh, System.Globalization.CultureInfo.InvariantCulture, out var h))
            (Width, Height) = (w, h);
        _placement.Track(this);
        var flagged = args.Where(a => a.StartsWith("--", StringComparison.Ordinal)).Select(a => PageOf(a[2..])).FirstOrDefault(i => i != 0);
        Show(flagged != 0 || snapping ? flagged : PageOf(_placement.Page));
        if (args.FirstOrDefault(a => a.StartsWith("--tab=", StringComparison.Ordinal))?[6..] is { } tab && int.TryParse(tab, out var t))
        {
            _subjects?.SelectTab(t);
            if (Page.Child is CalendarView calendar) calendar.SelectTab(t);
            if (Page.Child is MybkView mybk) mybk.Tabs.SelectedIndex = t;
            if (Page.Child is Views.Practice.PracticeHost practiceTab) practiceTab.Tab = t;
            if (Page.Child is SettingsView settings && t == 1) settings.ExpandAdvanced();
        }
        ShowSync();
        if (args.FirstOrDefault(a => a.StartsWith("--snap=", StringComparison.Ordinal))?[7..] is { Length: > 0 } snap) SnapAndClose(snap);
#if WINDOWS
        if (args.Contains("--toastcheck")) { Log.Info($"Toastcheck: {Platform.Windows.Toast.Setting()}"); Environment.Exit(0); }   // không gửi thông báo
#endif
        if (args.Contains("--panel")) Opened += (_, _) => StatusButton.Flyout?.ShowAt(StatusButton);
        if (args.Contains("--quick")) Opened += (_, _) => OpenQuick();
        if (args.Contains("--login")) Opened += (_, _) => Login();
        if (args.Contains("--event")) Opened += async (_, _) => await new EventWindow(null, null).ShowDialog(this);
        if (args.Contains("--setup")) Opened += (_, _) => { var (f, u) = FirstRunWindow.Both(); f.Show(this); u.Show(this); };
        if (args.Contains("--webcheck")) Opened += async (_, _) => { Environment.ExitCode = await Web.WebCheck.RunAsync() ? 0 : 1; _exiting = true; Close(); };
    }

    private readonly List<int> _back = [];

    private void Back()
    {
        if (Drawer.IsVisible)
        {
            if (_inbox?.CanGoBack == true) _inbox.BackToList();
            else CloseInbox();
            return;
        }
        if (_practice is { CanGoBack: true } practice && TopLevel.GetTopLevel(practice) is not null) { practice.GoBack(); return; }
        if (_back.Count == 0) return;
        var to = _back[^1];
        _back.RemoveAt(_back.Count - 1);
        Show(to, remember: false);
    }

    private async void Show(int index, bool remember = true) => await ShowAsync(index, remember);

    private Flyout? _quick;

    private void OpenQuick()
    {
        if (_quick is null)
        {
            var panel = new QuickSearchPanel();
            _quick = new Flyout { Content = panel, Placement = PlacementMode.Bottom };
            panel.Picked += item => { _quick.Hide(); _ = GoQuickAsync(item.Target); };
        }
        List<QuickItem> items = [.. _pages.Select((p, i) => new QuickItem(p.Title, L.T("quick.page"), $"page:{i}"))];
        items.AddRange(SubjectsPresenter.Rows(_host.State.Lms, null).Select(r => new QuickItem(r.Name, L.T("quick.subject"), "subject:" + r.Name)));
        if (_practice is { } practice) items.AddRange(practice.QuickItems());
        items.AddRange(InboxQuickItems());
        ((QuickSearchPanel)_quick.Content!).Reset(items);
        _quick.ShowAt(SearchButton);
    }

    private IEnumerable<QuickItem> InboxQuickItems() =>
        Inbox.Rows(SoHocTap.Data.LmsMessagesStore.Read(), _host.State.Lms?.Announcements ?? [], InboxReads.Load(), Format.Now, 14)
            .Select(r => new QuickItem(r.Title, r.Sub.Length > 0 ? $"{r.KindText}, {r.Sub}" : r.KindText, "inbox:" + r.Key));

    private async Task GoQuickAsync(string target)
    {
        var (kind, value) = target.IndexOf(':') is var i and >= 0 ? (target[..i], target[(i + 1)..]) : (target, "");
        switch (kind)
        {
            case "page" when int.TryParse(value, out var index):
                await ShowAsync(index, true);
                break;
            case "subject":
                _showLibrary = false;
                await ShowAsync(PageOf("mon"), true);
                _subjects?.SelectSubject(value);
                break;
            case "inbox":
                OpenInbox(value);
                break;
            case "lesson":
                await ShowAsync(PageOf("luyen-tap"), true);
                _practice?.OpenLessonById(value);
                break;
        }
    }

    private int _navGen;

    private async Task ShowAsync(int index, bool remember)
    {
        var gen = ++_navGen;
        if (index != _shownIndex && _practice is { } practice && TopLevel.GetTopLevel(practice) is not null)
        {
            await practice.FlushAsync();
            if (gen != _navGen) return;
        }
        if (remember && _shownIndex >= 0 && index != _shownIndex)
        {
            _back.Add(_shownIndex);
            if (_back.Count > Config.Int("app.backHistory", 20)) _back.RemoveAt(0);
        }
        _current = index;
        for (var i = 0; i < Nav.Children.Count; i++)
        {
            var b = (Button)Nav.Children[i];
            b.Classes.Set("on", i == index);
        }
        SettingsButton.Classes.Set("on", index == SettingsPage);
        More.Classes.Set("on", index < NavCount && !Nav.Children[index].IsVisible);
        if (index != _shownIndex) FitBar();
        var old = Page.Child as ITabbedPage;
        if (index != _shownIndex && old is not null) SaveTab(_shownIndex, old.Tab);
        var page = _pages[index].Build();
        if (page is ITabbedPage tabbed && !ReferenceEquals(page, old))
            tabbed.Tab = index == _shownIndex && old is not null ? old.Tab : _snapping ? 0 : Config.Int($"ui.tab.{Routes[index]}", 0);
        Scroller.VerticalScrollBarVisibility = page is IFillPage ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        Page.MaxWidth = page is IFillPage ? double.PositiveInfinity : (double)this.FindResource("ContentMaxWidth")!;
        var changed = index != _shownIndex;
        Page.Child = page;
        if (changed) Scroller.Offset = default;
        _shownIndex = index;
        if (changed) ShowInfo();
        WantMybk(page);
        if (page.FindControl<TabControl>("Tabs") is { } tabs)
            tabs.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, tabs)) WantMybk(page); };
    }

    private void WantMybk(Control page)
    {
        var sections = SoHocTap.Sources.Mybk.MybkSections.ForPage(Routes[_current], page is ITabbedPage t ? t.Tab : 0);
        if (SoHocTap.Sources.Mybk.MybkSource.Show(sections)) _host.Hub.Start(SourceIds.Mybk, force: true, soon: true);
    }

    private int _zoom = 100;

    private static int Token(string key) => (int)Application.Current!.FindResource(key)!;

    private void ApplyZoom(int percent)
    {
        _zoom = Math.Clamp(percent, Token("ZoomMin"), Token("ZoomMax"));
        Zoom.LayoutTransform = _zoom == 100 ? null : new Avalonia.Media.ScaleTransform(_zoom / 100.0, _zoom / 100.0);
    }

    private void ZoomStep(int step)
    {
        ApplyZoom(step == 0 ? 100 : _zoom + step * Token("ZoomStep"));
        if (Config.Int("app.zoom", 100) != _zoom) Config.Set("app.zoom", _zoom);
    }

    private bool _snapping;

    private void SaveTab(int index, int tab)
    {
        if (!_snapping && index >= 0 && tab >= 0 && Config.Int($"ui.tab.{Routes[index]}", 0) != tab) Config.Set($"ui.tab.{Routes[index]}", tab);
    }

    private readonly NoticeLog _notices = new();

    private void ShowSync()
    {
        var rows = Sources();
        var last = new[] { _host.State.SyncedAt(SourceIds.Lms), _host.State.SyncedAt(SourceIds.Mybk) }.Max();
        var (text, attention) = StatusPanel.Bar(rows, _notices.Unread, last, OfflineMode.On);
        StatusText.Text = text;
        StatusText.FontWeight = attention ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal;
        var problem = rows.Any(r => r.Problem is not null && !r.Partial);
        var busy = rows.Any(r => r.Syncing) || OfflineMode.On;
        StatusDot.Fill = this.TryFindResource(problem ? "Danger" : busy ? "Warn" : "Ok", ActualThemeVariant, out var dot) ? dot as Avalonia.Media.IBrush : null;
        var syncing = rows.Any(r => r.Syncing);
        if (!problem && !syncing) StatusText.Text = L.T(OfflineMode.On ? "status.bar.offline" : "status.online");
        ToolTip.SetTip(StatusButton, text + Environment.NewLine + string.Join(Environment.NewLine, rows.Select(r => r.Problem is { } p ? $"{r.State}. {p}" : r.State)));
        ToolTip.SetTip(StatusButton, string.Join(Environment.NewLine, rows.Select(r => r.Problem is { } p ? $"{r.State}. {p}" : r.State)));
        var bar = SyncStatus.Progress([.. new[] { SourceIds.Lms, SourceIds.Mybk }.Select(n => (_host.State.Syncing(n), _host.State.Step(n)))]);
        SyncBar.IsVisible = bar.Visible;
        SyncBar.IsIndeterminate = bar.Visible && bar.Fraction is null;
        if (bar.Fraction is { } f) SyncBar.Value = f;
        FitBar();
    }

    private IReadOnlyList<SourceState> Sources() =>
        [StatusPanel.Source(_host.State, SourceIds.Lms, "LMS"), StatusPanel.Source(_host.State, SourceIds.Mybk, "MyBK")];

    private Control StatusPanelContent(Flyout owner)
    {
        var panel = new StackPanel { Spacing = 12, Width = (double)this.FindResource("PanelWidth")! };
        static Grid Row(Control left, Control? right)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto"), Children = { left } };
            if (right is null) return g;
            right.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            Grid.SetColumn(right, 2);
            g.Children.Add(right);
            return g;
        }
        Button Act(string text, Action run)
        {
            var b = new Button { Content = text };
            b.Click += (_, _) => { owner.Hide(); run(); };
            return b;
        }
        static TextBlock Wrap(string text, params string[] classes)
        {
            var t = new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, TextTrimming = Avalonia.Media.TextTrimming.None };
            t.Classes.AddRange(classes);
            return t;
        }
        var offline = new ToggleSwitch { IsChecked = OfflineMode.On, OnContent = L.T("offline.label"), OffContent = L.T("offline.label"), MinWidth = 0 };
        ToolTip.SetTip(offline, L.T("offline.tip"));
        var offlineHelp = Wrap(L.T("offline.on"), "meta");
        offlineHelp.IsVisible = OfflineMode.On;
        offline.IsCheckedChanged += (_, _) =>
        {
            OfflineMode.Set(offline.IsChecked == true);
            offlineHelp.IsVisible = OfflineMode.On;
        };
        panel.Children.Add(Row(new TextBlock { Text = L.T("status.panel.sync"), Classes = { "card-title" } }, offline));
        panel.Children.Add(offlineHelp);
        foreach (var r in Sources())
        {
            var text = new StackPanel { Spacing = 2, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Children = { Wrap(r.State) } };
            if (r.Problem is { } p) text.Children.Add(Wrap(p, "meta", "danger"));
            if (r.Detail is { Length: > 0 } d) text.Children.Add(Wrap(d, "sub"));
            var id = r.Id;
            var (site, page) = id == SourceIds.Mybk ? ("sources.mybk.home", "MyBK") : ("sources.lms.site", "LMS");
            var actions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4 };
            if (r.NeedsLogin) actions.Children.Add(Act(L.T("status.panel.relogin"), Login));
            var open = Icons.Button("OpenNew", L.F("status.panel.open", page));
            open.Click += (_, _) => { owner.Hide(); _ = Files.Links.OpenAsync(this, Config.Str(site), page); };
            actions.Children.Add(open);
            if (!r.Off && !r.Syncing)
            {
                var sync = Icons.Button("Refresh", L.T(id == SourceIds.Mybk ? "status.panel.syncMybk" : "status.panel.syncLms"));
                sync.Click += (_, _) => { owner.Hide(); SyncSource(id); };
                actions.Children.Add(sync);
            }
            panel.Children.Add(Row(text, actions));
        }
        panel.Children.Add(new Separator { Margin = default });
        var muted = _host.RemindersMutedUntil;
        var noticeActions = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8,
            Children = { Act(L.T(muted is null ? "status.panel.mute" : "status.panel.unmute"), () => { _host.MuteReminders(muted is null); ShowSync(); }) },
        };
        if (_notices.Items.Count > 0) noticeActions.Children.Add(Act(L.T("status.panel.clear"), _notices.Clear));
        panel.Children.Add(Row(new TextBlock { Text = L.T("status.panel.notices"), Classes = { "card-title" } }, noticeActions));
        if (muted is { } until) panel.Children.Add(Wrap(L.F("status.panel.mutedUntil", Format.Hm(until)), "meta"));
        if (_notices.Items.Count == 0) panel.Children.Add(new TextBlock { Text = L.T("status.panel.noNotices"), Classes = { "meta" } });
        foreach (var n in _notices.Items)
        {
            var body = new StackPanel { Spacing = 2, Children = { Wrap(n.Title), Wrap(n.Body, "sub"), new TextBlock { Text = Format.Ago(n.Time), Classes = { "sub" } } } };
            body.Children[0].SetValue(TextBlock.FontWeightProperty, Avalonia.Media.FontWeight.SemiBold);
            if (n.Page.Length == 0) { panel.Children.Add(body); continue; }
            panel.Children.Add(new Button
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Content = body, Command = new Command(() => { owner.Hide(); Go(n.Page); }),
            });
        }
        _notices.MarkRead();
        return new ScrollViewer { MaxHeight = (double)this.FindResource("PanelMaxHeight")!, Content = panel };
    }

    private void SyncNow()
    {
        SyncSource(SourceIds.Lms);
        SyncSource(SourceIds.Mybk);
    }

    private void SyncSource(string id)
    {
        if (id == SourceIds.Mybk) SoHocTap.Sources.Mybk.MybkSource.RefreshButton();
        _host.Hub.Start(id, force: true);
    }

    private InfoBarState? _info;
    private string? _infoClosed;
    private string _loginStage = "", _loginMessage = "";
    private readonly Avalonia.Threading.DispatcherTimer _infoHide = new() { Interval = TimeSpan.FromSeconds(6) };

    private void Login() => _host.Login(view => { LoginHost.Child = view; LoginHost.IsVisible = view is not null; });

    private void OnLoginProgress(string stage, string message)
    {
        (_loginStage, _loginMessage) = (stage, message);
        ShowSync();
        ShowInfo();
    }

    private void ShowInfo()
    {
        var s = _host.State;
        var mybkNeeded = MybkLoginPrompt.Needed(_current == PageOf("mybk"), Format.Now, s.SyncedAt(SourceIds.Mybk), (s.Mybk?.Registration ?? []).Select(r => (r.Start, r.End)));
        _info = InfoBar.Decide(InfoBar.Input(s, _loginStage, _loginMessage, mybkNeeded, Platform.Credentials.Supported, _infoClosed));
        Info.IsVisible = _info is not null;
        if (_info is not { } i) return;
        Info.Classes.Set("success", i.Severity == InfoSeverity.Success);
        Info.Classes.Set("warning", i.Severity == InfoSeverity.Warning);
        Info.Classes.Set("error", i.Severity == InfoSeverity.Error);
        InfoText.Text = i.Text;
        ToolTip.SetTip(InfoText, i.Detail);
        (InfoPrimary.Content, InfoPrimary.IsVisible) = (i.PrimaryText, i.Primary != InfoAction.None);
        (InfoSecondary.Content, InfoSecondary.IsVisible) = (i.SecondaryText, i.Secondary != InfoAction.None);
        if (!i.AutoHide) return;
        _infoHide.Stop();
        _infoHide.Start();
    }

    private void OnInfoHide(object? sender, EventArgs e)
    {
        _infoHide.Stop();
        if (_loginStage is "done" or "logout") _loginStage = "";
        ShowInfo();
    }

    private async void Run(InfoAction action)
    {
        switch (action)
        {
            case InfoAction.Login: Login(); break;
            case InfoAction.Retry: SyncNow(); break;
            case InfoAction.OpenSource:
                var mybk = _info?.Source == SourceIds.Mybk;
                await Files.Links.OpenAsync(this, Config.Str(mybk ? "sources.mybk.home" : "sources.lms.site"), mybk ? "MyBK" : "LMS");
                break;
            case InfoAction.AutoLoginOn:
                await ShowAsync(SettingsPage, remember: true);
                (Page.Child as SettingsView)?.OpenAutoLogin();
                break;
            case InfoAction.AutoLoginNo:
                SoHocTap.Core.Settings.Sso.AutoLoginAsked = true;
                ShowInfo();
                break;
        }
    }

    private void FitBar()
    {
        var room = Bar.Bounds.Width - 32;
        if (room <= 0) return;
        foreach (var b in Nav.Children) b.IsVisible = true;
        More.IsVisible = false;
        double W(Control c) { c.Measure(Size.Infinity); return c.DesiredSize.Width; }
        SearchButton.IsVisible = true;
        double Right() => W(StatusButton) + W(InboxButton) + W(SettingsButton) + (SearchButton.IsVisible ? W(SearchButton) : 0);
        var rightOnly = W(StatusButton) + W(InboxButton) + W(SettingsButton);
        if (Math.Max(W(Nav), rightOnly) + W(SearchButton) / 2 + 24 > room / 2) SearchButton.IsVisible = false;
        if (W(Nav) + Right() <= room) return;
        More.IsVisible = true;
        for (var i = NavCount - 1; i >= 0 && W(Nav) + Right() + W(More) > room; i--)
            if (i != _current) Nav.Children[i].IsVisible = false;
    }

    private int SettingsPage => PageOf("cai-dat");

    private Views.Practice.PracticeHost PracticeFor(string? course)
    {
        _practice ??= new Views.Practice.PracticeHost(_host.Router);
        switch (_practice.Parent)
        {
            case ContentControl c: c.Content = null; break;
            case Decorator d: d.Child = null; break;
            case Panel p: p.Children.Remove(_practice); break;
        }
        _practice.FocusCourse(course);
        return _practice;
    }

    private void ToggleInbox()
    {
        if (Drawer.IsVisible) CloseInbox();
        else OpenInboxDrawer();
    }

    private void OpenInboxDrawer()
    {
        var inbox = InboxPage();
        inbox.Reload(_host.State.Lms);
        if (!ReferenceEquals(Drawer.Child, inbox)) Drawer.Child = inbox;
        Drawer.IsVisible = DrawerScrim.IsVisible = true;
        InboxButton.Classes.Set("on", true);
        _inboxPop?.Hide();
    }

    private void CloseInbox()
    {
        Drawer.IsVisible = DrawerScrim.IsVisible = false;
        InboxButton.Classes.Set("on", false);
    }

    private void Go(string route)
    {
        if (route.Split('/')[0] is "hop-thu" or "tin-nhan") OpenInboxDrawer();
        else Show(PageOf(route));
    }

    private InboxView InboxPage()
    {
        if (_inbox is not null) return _inbox;
        _inbox = new InboxView { SyncNow = _host.SyncMessagesNowAsync };
        _inbox.ReadChanged += () => UpdateInbox(popup: false);
        _inbox.CloseRequested += CloseInbox;
        return _inbox;
    }

    private long _inboxSeen = Format.Now;
    private Flyout? _inboxPop;
    private readonly Avalonia.Threading.DispatcherTimer _inboxPopHide = new() { Interval = TimeSpan.FromSeconds(8) };

    private void UpdateInbox(bool popup)
    {
        var rows = Inbox.Rows(SoHocTap.Data.LmsMessagesStore.Read(), _host.State.Lms?.Announcements ?? [], InboxReads.Load(), Format.Now,
            Config.Int("app.inbox.newsDays", 14));
        var unread = rows.Count(r => r.Unread);
        InboxBadge.IsVisible = unread > 0;
        InboxCount.Text = unread > 99 ? "99+" : unread.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ToolTip.SetTip(InboxButton, unread > 0 ? L.F("inbox.unreadTip", unread) : L.T("nav.inbox"));
        var fresh = rows.Where(r => r.Unread && r.Time > _inboxSeen).OrderByDescending(r => r.Time).Take(3).ToList();
        if (rows.Count > 0) _inboxSeen = Math.Max(_inboxSeen, rows.Max(r => r.Time));
        if (!popup || fresh.Count == 0 || Drawer.IsVisible || !IsVisible) return;
        var panel = new StackPanel { Spacing = 8, Width = 340 };
        panel.Children.Add(new TextBlock { Text = L.F("inbox.popup", fresh.Count), Classes = { "card-title" } });
        foreach (var r in fresh)
        {
            var body = new StackPanel { Spacing = 2 };
            body.Children.Add(new TextBlock { Text = r.Title, FontWeight = Avalonia.Media.FontWeight.SemiBold, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
            if (r.Preview.Length > 0) body.Children.Add(new TextBlock { Text = r.Preview, Classes = { "sub" }, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
            var open = new Button { Content = body, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            var key = r.Key;
            open.Click += (_, _) => { _inboxPop?.Hide(); OpenInbox(key); };
            panel.Children.Add(open);
        }
        _inboxPop ??= new Flyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        _inboxPop.Content = panel;
        _inboxPop.ShowAt(InboxButton);
        _inboxPopHide.Stop();
        _inboxPopHide.Start();
    }

    private static Control BarLabel(string text) => new Grid
    {
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        Children =
        {
            new TextBlock { Text = text, FontWeight = Avalonia.Media.FontWeight.SemiBold, Opacity = 0, IsHitTestVisible = false, Classes = { "bar-text" } },
            new TextBlock { Text = text, Classes = { "bar-text" } },
        },
        [Avalonia.Automation.AutomationProperties.NameProperty] = text,
    };

    private void FillMore(MenuFlyout menu)
    {
        menu.Items.Clear();
        for (var i = 0; i < NavCount; i++)
        {
            if (Nav.Children[i].IsVisible) continue;
            var index = i;
            var item = new MenuItem { Header = _pages[i].Title };
            item.Click += (_, _) => Show(index);
            menu.Items.Add(item);
        }
    }

    private string AccountText()
    {
        var s = _host.State;
        return s.Account != AccountNeed.None ? L.T("settings.needLogin")
            : s.Lms?.User is { } u ? L.F("settings.signedInAs", u) : L.T("settings.signedIn");
    }

    private bool _exiting;
    private bool _trayHintShown;
    private readonly Platform.WindowPlacement _placement = Platform.WindowPlacement.Load();
    private Platform.Notifier? _notifier;

    private void SetupTray()
    {
        var open = new NativeMenuItem(L.F("tray.open", AppInfo.Name));
        open.Click += (_, _) => ShowFromTray();
        var sync = new NativeMenuItem(L.T("status.panel.syncNow"));
        sync.Click += (_, _) => SyncNow();
        var offline = new NativeMenuItem(L.T("offline.label")) { ToggleType = MenuItemToggleType.CheckBox, IsChecked = OfflineMode.On };
        offline.Click += (_, _) => OfflineMode.Set(!OfflineMode.On);
        OfflineMode.Changed += on => Avalonia.Threading.Dispatcher.UIThread.Post(() => offline.IsChecked = on);
        var exit = new NativeMenuItem(L.T("tray.exit"));
        exit.Click += (_, _) => { _exiting = true; Close(); };
        var tray = new TrayIcon
        {
            Icon = Icon, ToolTipText = AppInfo.Name,
            Menu = new NativeMenu { Items = { open, sync, offline, new NativeMenuItemSeparator(), exit } },
        };
        tray.Clicked += (_, _) => ShowFromTray();
        TrayIcon.SetIcons(Application.Current!, [tray]);
        Closing += (_, e) =>
        {
            _placement.Capture(this, Routes[_current]);
            _placement.Save();
            if (Page.Child is ITabbedPage t) SaveTab(_shownIndex, t.Tab);
            if (_exiting || !SoHocTap.Core.Settings.App.CloseToTray) return;
            e.Cancel = true;
            Hide();
            _ = SleepAsync();
            if (!_trayHintShown) { _trayHintShown = true; _notifier?.Show(AppInfo.Name, L.T("tray.stillRunning"), ""); }
        };
        _notifier = new Platform.Notifier(this, page => { ShowFromTray(); Go(page); });
        _host.UpdateOffered += v => Notify(L.F("update.available", v), L.T("notice.updateBody"), "cai-dat");
        _host.Updates.Restarting += _ => Avalonia.Threading.Dispatcher.UIThread.Post(() => { _exiting = true; Close(); });
        Opened += (_, _) =>
        {
            if (!Environment.GetCommandLineArgs().Any(a => a.StartsWith("--snap=", StringComparison.Ordinal)) && FirstRunWindow.Next() is { } ask)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ask.ShowDialog(this), Avalonia.Threading.DispatcherPriority.Background);
            _notifier.Attach();
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _host.StartReminders(Notify), Avalonia.Threading.DispatcherPriority.Background);
        };
    }

    private void Notify(string title, string body, string page)
    {
        _notices.Add(new Notice(Format.Now, title, body, page));
        _notifier!.Show(title, body, page);
    }

    public void StartHidden()
    {
        if (FirstRun.NeedsFolder) FirstRun.SaveFolder(Paths.SuggestedRoot);
        _host.StartReminders(Notify);
    }

    private async Task SleepAsync()
    {
        if (Page.Child is not Views.Practice.PracticeHost practice) return;
        await practice.FlushAsync();
        if (IsVisible) return;
        Page.Child = null;
        _shownIndex = -1;
    }

    private void ShowFromTray()
    {
        Show();
        if (Page.Child is null) Show(_current);
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        _notifier?.Flush();
    }

    private static readonly string[] Routes = ["hom-nay", "mon", "mybk", "lich", "luyen-tap", "cai-dat"];

    private void OpenInbox(string key)
    {
        OpenInboxDrawer();
        InboxPage().Select(key);
    }

    private static int PageOf(string route)
    {
        var key = route.Split('/')[0] switch { "tl" or "mon-hoc" => "mon", "luyen" => "luyen-tap", "diem" or "dich-vu" => "mybk", "thu-vien" => "mon", var k => k };
        return Math.Max(0, Array.IndexOf(Routes, key));
    }

    private void OpenLibrary(string subject)
    {
        _showLibrary = true;
        _libraryPick = subject;
        Show(PageOf("mon"));
    }

    private async Task<string?> BackupAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = L.T("settings.backup"),
            SuggestedFileName = $"{AppInfo.Name} {DateTime.Now:yyyy-MM-dd}.zip",
            FileTypeChoices = [new FilePickerFileType("ZIP") { Patterns = ["*.zip"] }],
        });
        if (file?.TryGetLocalPath() is not { } path) return null;
        try
        {
            var (count, skipped) = await Task.Run(() => DataBackup.Write(Paths.Data, path, AppInfo.Version, DateTime.Now));
            Log.Info($"Sao lưu dữ liệu: {count} file{(skipped.Count > 0 ? $", bỏ qua {skipped.Count}" : "")}");
            return L.F("settings.backupDone", count, path) + (skipped.Count > 0 ? " " + L.F("settings.backupSkipped", string.Join(", ", skipped)) : "");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Sao lưu dữ liệu: {e.Message}");
            return L.F("settings.backupFailed", e.Message);
        }
    }

    private SoHocTap.Presentation.SettingsActions SettingsActions() => new(
        AccountText: AccountText,
        Login: Login,
        Logout: _host.LogoutAsync,
        SyncNow: SyncNow,
        ApplyTheme: App.ApplyTheme,
        ApplyAccent: App.ApplyAccent,
        PickRoot: async () => (await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = L.T("settings.rootPick") }))
            .FirstOrDefault()?.TryGetLocalPath(),
        OpenLink: url => Files.Links.OpenAsync(this, url),
        OpenLog: async () => { if (File.Exists(Log.LogFile)) await Launcher.LaunchFileInfoAsync(new FileInfo(Log.LogFile)); },
        SetLibraryUrl: _host.Library.SetBaseUrl,
        ClearLibrary: _host.Library.ClearCache,
        Backup: BackupAsync,
        AutostartGet: () => Platform.Autostart.Enabled,
        AutostartSet: Platform.Autostart.Set,
        AutoLoginSupported: Platform.Credentials.Supported,
        AutoLoginSave: Platform.Credentials.Enable,
        ForgetCredentials: Platform.Credentials.Forget,
        UpdateSupported: SoHocTap.Updates.UpdateService.CanSelfUpdate,
        UpdateCheck: async () => await _host.Updates.CheckAsync(manual: true, default) is { } o ? L.F("update.available", o.Version)
            : _host.Updates.LastError is { } err ? L.F("update.error", err) : L.F("update.latest", AppInfo.Version),
        UpdateInstall: async () =>
        {
            if (_host.Updates.Offer is null && await _host.Updates.CheckAsync(manual: true, default) is null) return L.F("update.latest", AppInfo.Version);
            if (!await _host.Updates.DownloadAsync(userAsked: true, default)) return L.F("update.failed", _host.Updates.LastError ?? "");
            _host.Updates.ApplyAndRestart();
            return L.F("update.installing", _host.Updates.Offer?.Version ?? "");
        },
        PageCount: _pages.Length,
        Refresh: () => Avalonia.Threading.Dispatcher.UIThread.Post(() => Show(SettingsPage)),
        Restart: () => { if (Platform.SingleInstance.StartRestart()) { _exiting = true; Close(); } },
        Fonts: App.InstalledFonts,
        ApplyFont: (family, size) =>
        {
            var choice = SoHocTap.Ui.FontChoice.Parse(family, size, App.IsFontInstalled);
            App.ApplyFont(choice);
            SoHocTap.Presentation.FontConfig.Save(choice);
            FitBar();
        });

    private void Download(IReadOnlyCollection<long>? pick, bool examsOnly)
    {
        var w = new DownloadWindow(pick, examsOnly);
        w.Closed += (_, _) => Show(_current);
        w.Finished += (text, seen) => { if (!seen) _notifier?.Show(L.T("download.title"), text, "mon", outside: true); };
        w.ShowDialog(this);
    }

    private void SnapAndClose(string path) => Opened += async (_, _) =>
    {
        await Task.Delay(1500);
        var size = new PixelSize((int)(Bounds.Width * RenderScaling), (int)(Bounds.Height * RenderScaling));
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(size, new Vector(96 * RenderScaling, 96 * RenderScaling));
        bitmap.Render(this);
        bitmap.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        if (_quick is { IsOpen: true, Content: Control c } && TopLevel.GetTopLevel(c) is { } popup && !ReferenceEquals(popup, this))
        {
            var ps = new PixelSize((int)(popup.Bounds.Width * RenderScaling), (int)(popup.Bounds.Height * RenderScaling));
            using var pb = new Avalonia.Media.Imaging.RenderTargetBitmap(ps, new Vector(96 * RenderScaling, 96 * RenderScaling));
            pb.Render(popup);
            pb.Save(Path.ChangeExtension(path, null) + "-popup.png", new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        _exiting = true;
        Close();
    };
}
