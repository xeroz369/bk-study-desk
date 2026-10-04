using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Ui.Pages;
using SoHocTap.Updates;

namespace SoHocTap.Ui;

/// <summary>Một trang trong cửa sổ chính.</summary>
internal interface IPage
{
    string Title { get; }
    string Subtitle { get; }
    /// <summary>Dữ liệu vừa đổi (sync xong...) thì vẽ lại. Chỉ gọi khi page đang hiện; page ẩn được đánh dấu cũ và vẽ lại lúc mở.</summary>
    void Refresh();
    /// <summary>Mở page tại một chỗ cụ thể (môn, tab...). Chuỗi rỗng thì giữ nguyên.</summary>
    void Open(string arg) { }
    /// <summary>Cửa sổ xuống khay: nhả tài nguyên nặng (WebView của Luyện tập), mở lại thì tự dựng lại.</summary>
    void Sleep() { }
}

/// <summary>Một mục trên thanh điều hướng. Đổi số đếm, chế độ gọn qua property change để ListBox không phải dựng lại (mất focus).</summary>
internal sealed class NavItem(PageEntry page, string tip) : INotifyPropertyChanged
{
    private string _badge = "";
    private bool _compact, _overflow;

    public PageEntry Page { get; } = page;
    public string Id => Page.Key;
    public string Label { get; } = L.T(page.LabelKey);
    public string Glyph => Page.Glyph;
    public string Tip { get; } = tip;
    public string AutomationId => "nav-" + Id;
    /// <summary>Tên cho trình đọc màn hình: mục chỉ còn icon vẫn đọc được tên, kèm số việc nếu có.</summary>
    public string AutomationName => _badge.Length > 0 ? L.F("nav.badgeName", Label, _badge) : Label;

    public string Badge
    {
        get => _badge;
        set { if (_badge != value) { _badge = value; Changed(nameof(Badge)); Changed(nameof(AutomationName)); } }
    }

    public bool Compact
    {
        get => _compact;
        set { if (_compact != value) { _compact = value; Changed(nameof(Compact)); } }
    }

    /// <summary>Không đủ chỗ trên thanh: mục nằm trong menu Thêm.</summary>
    public bool Overflow
    {
        get => _overflow;
        set { if (_overflow != value) { _overflow = value; Changed(nameof(Overflow)); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public override string ToString() => Label;
}

public partial class MainWindow : Window, IDisposable
{
    private readonly AppHost _host;
    private readonly WindowPlacement _placement = WindowPlacement.Load();
    private readonly Dictionary<string, IPage> _pages = [];
    /// <summary>Page đã dựng nhưng đang ẩn lúc dữ liệu đổi: vẽ lại khi mở (không vẽ lại mọi page sau mỗi lượt sync).</summary>
    private readonly HashSet<string> _stale = [];
    private readonly Stack<string> _back = new();
    private readonly List<NavItem> _nav;
    private string _current = "";
    private bool _exiting, _trayHintShown;
    private bool _infoClosedFor;
    private string _loginStage = "";

    public MainWindow()
    {
        InitializeComponent();
        StatusBarRoot.SizeChanged += (_, e) => { if (e.WidthChanged) FitStatusBar(); };
        AppFont.Changed += () => Dispatcher.BeginInvoke(FitStatusBar, System.Windows.Threading.DispatcherPriority.Loaded);
        Title = AppInfo.Name;
        Core.AppEvents.UnhandledError += ShowUnhandled;
        // Ctrl+1 đến Ctrl+N theo thứ tự trong PageRegistry: các mục điều hướng, rồi Cài đặt.
        _nav = [.. PageRegistry.Nav.Select(p => new NavItem(p, Hotkeyed(L.T(p.TipKey), p)))];
        for (var i = 0; i < PageRegistry.Hotkeys.Count && i < 9; i++)
            InputBindings.Add(new KeyBinding(NavigationCommands.GoToPage, Key.D1 + i, ModifierKeys.Control) { CommandParameter = PageRegistry.Hotkeys[i].Key });
        SettingsButton.ToolTip = Hotkeyed(L.T("nav.settings.tip"), PageRegistry.Find("cai-dat"));
        StVersion.Text = $"{AppInfo.Name} {AppInfo.Version}";
        StVersion.ToolTip = Core.Paths.AppRoot;
        _placement.Apply(this);
        _host = new AppHost(this);
        Nav.ItemsSource = _nav;
        _host.State.Changed += OnState;
        _host.State.Progress += UpdateStatus;
        _host.State.StatusChanged += () => { UpdateStatus(); UpdateInfoBar(); };
        _progressDelay.Tick += (_, _) => { _progressDelay.Stop(); UpdateStatus(); };
        InfoBar.Closed += OnInfoClose;
        _host.LoginProgress += OnLogin;
        _host.Navigate += Go;
        _updateView = new DownloadView(_host.Updates, StUpdateBar);
        _host.Updates.Changed += () => Dispatcher.InvokeAsync(ShowUpdateState);
        _host.Updates.Restarting += version => Dispatcher.InvokeAsync(() =>
        {
            StUpdate.Visibility = Visibility.Collapsed;
            Say(L.F("update.installing", version));
        });
        Closing += OnClosing;
        Closed += (_, _) => Dispose();
        Loaded += (_, _) => FitTopBar();
        // Đổi phông, cỡ chữ ở Cài đặt: bề rộng các mục trên thanh trên cùng đổi theo, đo lại rồi xếp lại (NavFit).
        AppFont.Changed += OnFontChanged;
        Nav.ItemContainerGenerator.StatusChanged += (_, _) =>
        {
            if (Nav.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                Dispatcher.BeginInvoke(FitTopBar, System.Windows.Threading.DispatcherPriority.Loaded);
        };
    }

    private static string Hotkeyed(string tip, PageEntry? page)
    {
        var i = page is null ? -1 : IndexOf(PageRegistry.Hotkeys, page);
        return i >= 0 ? $"{tip} (Ctrl+{i + 1})" : tip;
    }

    private static int IndexOf(IReadOnlyList<PageEntry> list, PageEntry page)
    {
        for (var i = 0; i < list.Count; i++) if (list[i] == page) return i;
        return -1;
    }

    /// <summary>Khởi động: sync, bật scheduler, mở lại page lần trước. hidden = chỉ chạy dưới tray (khi khởi động cùng Windows).</summary>
    internal void Start(bool hidden)
    {
        new WindowInteropHelper(this).EnsureHandle();   // WebView2 ẩn (login, MyBK) cần có HWND dù window chưa hiện
        _host.Start();
        Go(string.IsNullOrEmpty(_placement.Page) ? PageRegistry.Home : _placement.Page);
        Startup.Refresh();
        if (!hidden) Show();
        // Bản Store lần đầu: cho user chọn folder lưu tài liệu (gợi ý ổ khác ổ C) thay vì ép vào Documents.
        if (FolderSetupWindow.Needed)
        {
            if (hidden) new FolderSetupWindow().Close();   // chạy ngầm: lưu luôn folder gợi ý, lần mở window sau không hỏi nữa
            else Dispatcher.BeginInvoke(() => new FolderSetupWindow { Owner = this }.ShowDialog(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        // Bản public lần đầu: hỏi có kiểm tra bản mới không (chưa trả lời thì app không gọi mạng để kiểm tra). Mỗi lần mở chỉ một hộp thoại.
        else if (!hidden) AskUpdateMode();
        ShowUpdateState();
        // Vừa lên bản mới: không ghi "Đã cập nhật lên ..." ở thanh trạng thái, vì số phiên bản đã hiện ngay bên cạnh (StVersion);
        // câu báo nằm lại mãi chỉ thừa (người dùng góp ý 04/10/2026). Việc cập nhật vẫn ghi trong app.log.
    }

    private bool _askedUpdate;

    /// <summary>
    /// Bản public chưa chọn chế độ cập nhật: hỏi một lần mỗi lần mở (chưa trả lời thì app không gọi mạng để kiểm tra).
    /// Mở cùng Windows (chạy ngầm) thì hỏi lúc người dùng mở cửa sổ từ khay lần đầu.
    /// </summary>
    private void AskUpdateMode()
    {
        if (_askedUpdate || !UpdateService.Supported || UpdateService.Mode != UpdateMode.Ask) return;
        _askedUpdate = true;
        Dispatcher.BeginInvoke(() => new UpdateWindow { Owner = this }.ShowDialog(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private readonly DownloadView _updateView;

    /// <summary>Thanh trạng thái: "Đang tải bản x.y.z... 42% (12,3/29,1 MB)", "Có bản x.y.z" hoặc "Khởi động lại để cập nhật".</summary>
    private void ShowUpdateState()
    {
        var u = _host.Updates;
        if (_updateView.Render() is { } progress)
        {
            StUpdate.Visibility = Visibility.Visible;
            StUpdateText.Text = progress;
            return;
        }
        var notice = u.Offer is null ? UpdateService.StartupNotice : null;
        StUpdate.Visibility = u.Offer is null && notice is null ? Visibility.Collapsed : Visibility.Visible;
        if (u.Offer is { } o) StUpdateText.Text = u.Downloaded ? L.T("update.restart") : L.F("update.available", o.Version);
        else if (notice is not null) StUpdateText.Text = notice;
    }

    private void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_host.Updates.Offer is { } o) new UpdateWindow(_host.Updates, o) { Owner = this }.ShowDialog();
        else Go("cai-dat");   // báo cài lỗi: mở Cài đặt, phần Cập nhật để thử lại
    }

    /// <summary>Hiện lại window (từ tray, hoặc khi mở app lần thứ hai).</summary>
    internal void ShowFromTray()
    {
        if (!IsVisible) Show();
        if (!FolderSetupWindow.Needed) AskUpdateMode();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>Thoát hẳn (từ menu tray, hoặc bấm X khi không bật thu xuống tray).</summary>
    internal void Exit()
    {
        _exiting = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Config app.closeToTray: bấm X chỉ ẩn window, app vẫn chạy dưới tray để sync và nhắc hạn.
        if (!_exiting && Config.Bool("app.closeToTray", true))
        {
            e.Cancel = true;
            _placement.Capture(this);
            _placement.Page = _current;
            _placement.Save();
            Hide();
            foreach (var p in _pages.Values) p.Sleep();
            if (!_trayHintShown) { _host.TrayHint(); _trayHintShown = true; }
            return;
        }
        _placement.Capture(this);
        _placement.Page = _current;
        _placement.Save();
    }

    public void Dispose()
    {
        AppFont.Changed -= OnFontChanged;
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------------ điều hướng

    internal void Go(string route)
    {
        var (entry, arg) = PageRegistry.Resolve(route);
        var id = entry.Key;
        if (_current.Length > 0 && _current != id) _back.Push(_current);
        _current = id;
        if (!_pages.TryGetValue(id, out var page))
        {
            page = entry.Create(_host, this);
            _pages[id] = page;
            page.Refresh();
        }
        else if (_stale.Remove(id)) page.Refresh();
        Page.Content = page;
        if (arg.Length > 0) page.Open(arg);
        // Page Luyện tập đã có breadcrumb riêng bên trong nên không cần dòng mô tả.
        PageSubtitle.Text = page.Subtitle;
        PageSubtitle.Visibility = page.Subtitle.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        ShowCurrent();
    }

    /// <summary>Chỉ báo trang đang mở: mục điều hướng (hoặc menu Thêm), nút Cài đặt, Giới thiệu.</summary>
    private void ShowCurrent()
    {
        var item = _nav.FirstOrDefault(n => n.Id == _current);
        if (!ReferenceEquals(Nav.SelectedItem, item)) Nav.SelectedItem = item;
        SettingsButton.IsChecked = _current == "cai-dat";
        AboutButton.IsChecked = _current == "gioi-thieu";
        NavMore.FontWeight = item is { Overflow: true } ? FontWeights.SemiBold : FontWeights.Normal;
        foreach (var mi in NavMore.Items.OfType<MenuItem>()) mi.IsChecked = (string)mi.Tag == _current;
    }

    private void OnNav(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is NavItem n && n.Id != _current) Go(n.Id);
    }

    /// <summary>Thanh điều hướng không cuộn: chặn BringIntoView để không đẩy cả hàng sang ngang khi chọn mục.</summary>
    private void OnNavBringIntoView(object sender, RequestBringIntoViewEventArgs e) => e.Handled = true;

    private void OnGoTo(object sender, ExecutedRoutedEventArgs e) => Go((string)e.Parameter);

    /// <summary>Ctrl+số từ khung Luyện tập: trang thứ n trong PageRegistry.Hotkeys, ngoài khoảng thì đứng yên.</summary>
    internal void GoIndex(int n) => Go(PageRegistry.ByHotkey(n)?.Key ?? _current);

    /// <summary>Số phím Ctrl+số đang dùng (các mục điều hướng và Cài đặt).</summary>
    internal static int PageKeys => PageRegistry.Hotkeys.Count;
    // Nghe Checked thay vì Click: UI Automation (trình đọc màn hình, test) bấm ToggleButton qua TogglePattern, chỉ đổi IsChecked
    // chứ không bắn Click (ToggleButtonAutomationPeer trong dotnet/wpf).
    private void OnSettings(object sender, RoutedEventArgs e) { if (_current != "cai-dat") Go("cai-dat"); }
    private void OnAbout(object sender, RoutedEventArgs e) { if (_current != "gioi-thieu") Go("gioi-thieu"); }

    /// <summary>Bấm lại nút của trang đang mở thì giữ nguyên trạng thái chọn (nút là chỉ báo trang hiện tại).</summary>
    private void OnToggleOff(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton b && (b == SettingsButton ? "cai-dat" : "gioi-thieu") == _current) b.IsChecked = true;
    }
    private void OnBack(object sender, ExecutedRoutedEventArgs e) => Back();

    internal void Back()
    {
        if (_back.Count == 0) return;
        var to = _back.Pop();
        _current = "";
        Go(to);
    }

    // ------------------------------------------------------------------ thanh trên cùng co giãn

    private void OnTopBarSize(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) FitTopBar();
    }

    private (List<double> Full, List<double> Compact, List<double> Right, double More)? _widths;

    /// <summary>
    /// Đo bề rộng từng mục ở hai chế độ (có chữ, chỉ icon), cụm nút bên phải ở hai mức và nút Thêm. Đổi chế độ rồi UpdateLayout cho chắc:
    /// Measure trực tiếp trả kích thước cũ vì phần tử cha chưa bị đánh dấu cần đo lại. Chỉ đo khi chưa có số (số đếm hay DPI đổi thì xóa).
    /// </summary>
    private (List<double> Full, List<double> Compact, List<double> Right, double More)? MeasureTopBar()
    {
        var containers = _nav.Select(n => Nav.ItemContainerGenerator.ContainerFromItem(n) as ListBoxItem).ToList();
        if (containers.Any(c => c is null)) return null;
        double Outer(FrameworkElement el) => el.DesiredSize.Width + el.Margin.Left + el.Margin.Right;
        List<double> Items(bool compact)
        {
            foreach (var n in _nav) { n.Overflow = false; n.Compact = compact; }
            UpdateLayout();
            return [.. containers.Select(c => Outer(c!))];
        }
        var full = Items(false);
        var small = Items(true);
        var right = new List<double>();
        for (var level = 0; level < NavFit.RightLevels; level++)
        {
            SetRight(level);
            UpdateLayout();
            right.Add(Outer(RightButtons));
        }
        NavMoreBar.Visibility = Visibility.Visible;
        UpdateLayout();
        return (full, small, right, Outer(NavMoreBar));
    }

    private void OnFontChanged() => Dispatcher.BeginInvoke(() =>
    {
        _widths = null;
        FitTopBar();
    }, System.Windows.Threading.DispatcherPriority.Loaded);

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _widths = null;
        FitTopBar();
    }

    private bool _fitting;

    /// <summary>Chọn cách hiện thanh trên cùng cho bề rộng hiện tại (NavFit) và áp vào các mục.</summary>
    private void FitTopBar()
    {
        var available = TopBar.ActualWidth - TopBar.Padding.Left - TopBar.Padding.Right;
        if (_fitting || available <= 0 || Nav.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated) return;
        _fitting = true;
        try { _widths ??= MeasureTopBar(); }
        finally { _fitting = false; }
        if (_widths is not { } w) return;
        // Chừa vài px: số đo lệch nhau vì làm tròn theo DPI (250%), sát quá thì mục cuối bị cắt mất một nửa.
        var layout = NavFit.Choose(available - 8, w.Full, w.Compact, w.Right, w.More);
        foreach (var (n, i) in _nav.Select((n, i) => (n, i)))
        {
            n.Compact = layout.NavCompact;
            n.Overflow = i >= layout.Visible;
        }
        SetRight(layout.Right);
        NavMoreBar.Visibility = layout.Visible < _nav.Count ? Visibility.Visible : Visibility.Collapsed;
        BuildMore();
        ShowCurrent();
    }

    /// <summary>Mức gọn của cụm nút bên phải: 1 = Đồng bộ chỉ icon (Cài đặt, Giới thiệu luôn chỉ icon).</summary>
    private void SetRight(int level)
    {
        SyncText.Visibility = level >= 1 ? Visibility.Collapsed : Visibility.Visible;
        SyncGlyph.Margin = level >= 1 ? new Thickness(0) : new Thickness(0, 0, 6, 0);
    }

    /// <summary>Menu Thêm: các mục không đủ chỗ, cùng icon, tên và phím tắt như trên thanh.</summary>
    private void BuildMore()
    {
        NavMore.Items.Clear();
        foreach (var n in _nav.Where(n => n.Overflow))
        {
            var mi = new MenuItem
            {
                Header = n.Badge.Length > 0 ? $"{n.Label} ({n.Badge})" : n.Label,
                Tag = n.Id,
                IsCheckable = false,
                ToolTip = n.Tip,
                Icon = new TextBlock { Text = n.Glyph, FontFamily = (System.Windows.Media.FontFamily)FindResource("IconFont"), FontSize = 15 },
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(mi, "more-" + n.Id);
            System.Windows.Automation.AutomationProperties.SetName(mi, n.AutomationName);
            mi.Click += (_, _) => Go(n.Id);
            NavMore.Items.Add(mi);
        }
    }

    // ------------------------------------------------------------------ sync, trạng thái

    private void OnSync(object sender, ExecutedRoutedEventArgs e) => Sync();
    private void OnSyncClick(object sender, RoutedEventArgs e) => Sync();
    internal void Sync()
    {
        _host.SyncAll();
        Say(L.T("status.syncStarted"));
    }

    private void OnOpenLms(object sender, RoutedEventArgs e) => _host.OpenSource("lms");
    private void OnOpenMybk(object sender, RoutedEventArgs e) => _host.OpenSource("mybk");

    /// <summary>
    /// Lỗi không bắt được (AppEvents.UnhandledError của nhánh core, có thể bắn từ thread nền): báo một câu ở thanh trạng thái.
    /// </summary>
    internal void ShowUnhandled(Exception e) => Dispatcher.BeginInvoke(() => Say(L.F("error.unhandled", e.Message)));

    internal void Say(string text)
    {
        StMessage.Text = text;
        StMessage.ToolTip = text.Length > 0 ? text : null;   // câu dài bị cắt "..." thì di chuột xem đủ
    }

    /// <summary>Thanh trạng thái: từng nguồn đang làm bước nào (kèm số), lỗi gì, đồng bộ lúc nào; thanh tiến độ khi đang đồng bộ.</summary>
    private void UpdateStatus()
    {
        var s = _host.State;
        string Src(string name, string label)
        {
            // "LMS: Đang kiểm tra khóa học 3/12...", "LMS: Đang tải tệp 2/5 (Giải tích 2)...": không phần trăm, không ước thời gian.
            if (s.Syncing(name))
                return s.Step(name) is { } st
                    ? L.F("status.step", label, L.F(st.Key, st.Count, st.Of, st.Detail ?? ""))
                    : L.F("status.syncing", label);
            if (s.Error(name) is { } err) return L.F("status.syncError", label);
            return s.SyncedAt(name) is { } t ? L.F("status.syncedAt", label, Format.DayDiff(t) == 0 ? Format.Hm(t) : Format.DateTime(t)) : L.F("status.neverSynced", label);
        }
        // Lỗi cả lượt hoặc lỗi từng phần: biểu tượng lỗi chuẩn cạnh tên nguồn, chi tiết trong tooltip (và trên InfoBar).
        void Show(string name, string label, TextBlock text, SeverityIcon icon)
        {
            text.Text = Src(name, label);
            List<string> problems = s.Syncing(name) ? [] : s.ExplainError(name, label) is { } err ? [err.Text]
                : s.Warnings(name).Select(x => x.What).Distinct().ToList();
            icon.Visibility = problems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            var tip = problems.Count > 0 ? string.Join("\n", problems) : null;
            text.ToolTip = icon.ToolTip = tip;
            System.Windows.Automation.AutomationProperties.SetHelpText(text, tip ?? "");
        }
        Show("lms", "LMS", StLms, StLmsIcon);
        Show("mybk", "MyBK", StMybk, StMybkIcon);
        StAccount.Text = L.T(s.Account == AccountNeed.None ? "status.signedIn" : "status.signedOut");
        var syncing = s.Syncing("lms") || s.Syncing("mybk");
        SyncButton.IsEnabled = !syncing;
        var syncText = L.T(syncing ? "nav.syncing" : "nav.sync");
        if (SyncText.Text != syncText)
        {
            // "Đang đồng bộ..." dài hơn "Đồng bộ": đo lại thanh trên cùng, kẻo mục điều hướng cuối bị cắt mất.
            SyncText.Text = syncText;
            _widths = null;
            Dispatcher.BeginInvoke(FitTopBar, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        // Hết đồng bộ: nói đúng kết quả, có nguồn lỗi thì không ghi "Đã đồng bộ".
        if (!syncing && StMessage.Text == L.T("status.syncStarted")) Say(L.T(SyncFailure() is null ? "status.synced" : "status.syncHadError"));

        // Một thanh có số cho cả LMS và MyBK (ProgressGate: hiện sau 1 giây, giữ ít nhất 800 ms, không lùi, vô định khi chưa biết tổng).
        var running = SourceNames.Where(s.Syncing).Select(s.Step).ToList();
        var bar = _bar.Update(syncing, Sources.SyncProgress.Overall(running), DateTime.UtcNow);
        StProgress.IsIndeterminate = bar.Indeterminate;
        if (!bar.Indeterminate) StProgress.Value = bar.Value;
        StProgressItem.Visibility = bar.Visible ? Visibility.Visible : Visibility.Collapsed;
        Dispatcher.BeginInvoke(FitStatusBar, System.Windows.Threading.DispatcherPriority.Loaded);
        _progressDelay.Stop();
        if (bar.Recheck is { } again)
        {
            _progressDelay.Interval = again < TimeSpan.FromMilliseconds(20) ? TimeSpan.FromMilliseconds(20) : again;
            _progressDelay.Start();
        }
    }

    /// <summary>
    /// Thanh trạng thái hết chỗ (cửa sổ hẹp, chữ lớn): không để mục cuối (phiên bản) bị cắt. Bỏ trước dòng kỳ thi gần nhất (đã có ở Hôm nay),
    /// rồi mới rút gọn chữ LMS, MyBK bằng "..." (di chuột xem đủ). Đủ chỗ thì hiện lại hết (DESIGN 6b-3).
    /// </summary>
    private void FitStatusBar()
    {
        var avail = StatusBarRoot.ActualWidth - StatusBarRoot.Padding.Left - StatusBarRoot.Padding.Right;
        if (avail <= 0) return;
        StExam.Visibility = StExam.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        StLms.MaxWidth = StMybk.MaxWidth = double.PositiveInfinity;
        double Need()
        {
            var sum = 0.0;
            foreach (var item in StatusBarRoot.Items.OfType<FrameworkElement>())
            {
                if (item.Visibility != Visibility.Visible || ReferenceEquals(item, StMessage.Parent)) continue;
                item.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                sum += item.DesiredSize.Width;
            }
            return sum;
        }
        var need = Need();
        if (need > avail && StExam.Visibility == Visibility.Visible)
        {
            StExam.Visibility = Visibility.Collapsed;
            need = Need();
        }
        if (need > avail)
        {
            StLms.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            StMybk.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var cap = Math.Max(60, (StLms.DesiredSize.Width + StMybk.DesiredSize.Width - (need - avail)) / 2);
            StLms.MaxWidth = StMybk.MaxWidth = cap;
        }
        foreach (var t in new[] { StLms, StMybk })
            if (double.IsFinite(t.MaxWidth) && t.ToolTip is null) t.ToolTip = t.Text;
    }

    private void OnState()
    {
        var s = _host.State;
        UpdateStatus();

        var exam = s.Timeline.FirstOrDefault(e => e.Kind == "exam" && e.Time > Format.Now);
        StExam.Text = exam is null ? "" : L.F("status.exam", exam.Subject, Format.DayDiff(exam.Time) is var d && d > 0 ? L.F("format.days", d) : L.T("status.examToday"));
        StExam.ToolTip = StExam.Text.Length > 0 ? StExam.Text : null;

        // Số đếm đổi qua property change: không Items.Refresh() (dựng lại cả ListBox, mất focus bàn phím đang ở thanh điều hướng).
        var badge = s.Upcoming(24).Count(e => !e.Done && e.Kind is not ("class" or "exam")) is var n && n > 0 ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
        if (_nav.FirstOrDefault(x => x.Id == PageRegistry.Home) is { } home && home.Badge != badge)
        {
            home.Badge = badge;
            _widths = null;   // số đếm đổi bề rộng mục Hôm nay
            Dispatcher.BeginInvoke(FitTopBar, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        UpdateInfoBar();
        // Chỉ vẽ lại page đang hiện; page khác vẽ lại khi người dùng mở (Go).
        foreach (var (key, p) in _pages)
        {
            if (key == _current && IsVisible) p.Refresh();
            else _stale.Add(key);
        }
        if (_pages.TryGetValue(_current, out var cur)) PageSubtitle.Text = cur.Subtitle;
    }

    /// <summary>Mở lại từ khay: page đang hiện có thể đã cũ (sync chạy lúc cửa sổ ẩn).</summary>
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (_stale.Remove(_current) && _pages.TryGetValue(_current, out var cur))
        {
            cur.Refresh();
            PageSubtitle.Text = cur.Subtitle;
        }
    }

    // ------------------------------------------------------------------ InfoBar: đăng nhập

    /// <summary>Bước login: key thông báo trong file ngôn ngữ.</summary>
    private static readonly Dictionary<string, string> LoginText = new()
    {
        ["check"] = "info.check",
        ["password"] = "info.password",
        ["mybk"] = "info.mybk",
        ["done"] = "info.done",
        ["cancel"] = "info.cancel",
        ["logout"] = "info.logout",
    };

    private void OnLogin(string stage, string message)
    {
        _loginStage = stage;
        var text = stage switch
        {
            "lms" => message.Length > 0 ? L.F("info.lmsFor", message) : L.T("info.lms"),
            "error" => L.F("info.error", message),
            _ => LoginText.TryGetValue(stage, out var key) ? L.T(key) : "",
        };
        ShowInfo(stage switch { "error" => Severity.Error, "cancel" => Severity.Warning, "done" => Severity.Success, _ => Severity.Informational }, text, null, null);
        // InfoBar dành cho thông báo dài hạn, không tự ẩn (hướng dẫn InfoBar của Microsoft): báo xong thì để người dùng tự đóng.
        if (stage is "done" or "logout") { _loginStage = ""; _keepShown = true; }
    }

    private bool _keepShown;   // đang hiện "đăng nhập xong/đã đăng xuất": giữ tới khi người dùng đóng hoặc có lỗi mới
    private readonly ProgressGate _bar = new();
    private static readonly string[] SourceNames = ["lms", "mybk"];
    // Hẹn vẽ lại thanh tiến độ khi không có sự kiện mới: tới lúc hiện (1 giây), hết lúc giữ (800 ms), số đứng yên 5 giây.
    private readonly System.Windows.Threading.DispatcherTimer _progressDelay = new() { Interval = ProgressGate.ShowDelay };

    private void UpdateInfoBar()
    {
        if (_loginStage.Length > 0) return;   // đang hiện tiến độ login
        var need = _host.State.Account;
        if (need == AccountNeed.None && SyncFailure() is { } fail)
        {
            // Lỗi không phải hết phiên (mất mạng, server chậm, LMS giới hạn...): báo rõ trên thanh, không chỉ trong tooltip.
            _keepShown = false;
            if (_errorClosed == fail.Text) { InfoBar.Hide(); return; }
            // Nút phụ mở trang trường trong cửa sổ app: thấy trang đang lỗi gì, hoặc đăng nhập lại khi app chưa nhận ra phiên đã hết.
            InfoBar.Show(Severity.Error, "", fail.Text, L.T("web.retry"), () => { _host.SyncAll(force: true); Say(L.T("status.syncStarted")); }, fail.Detail,
                L.F("info.openSource", fail.Label), () => _host.OpenSource(fail.Source));
            return;
        }
        if (need == AccountNeed.None && _keepShown) return;
        if (need == AccountNeed.None || _infoClosedFor)
        {
            InfoBar.Hide();
            if (need == AccountNeed.None) _infoClosedFor = false;
            return;
        }
        var what = need switch { AccountNeed.Both => L.T("info.both"), AccountNeed.Lms => "LMS", _ => "MyBK" };
        // Lần đầu chạy (chưa có dữ liệu gì) thì mời đăng nhập, chứ không báo hết session.
        var firstRun = _host.State.Lms is null && _host.State.Mybk is null;
        // Hết phiên là sự cố đã xảy ra (đồng bộ bị chặn) nên là Error, theo định nghĩa mức độ của InfoBar.
        _keepShown = false;
        ShowInfo(firstRun ? Severity.Informational : Severity.Error, firstRun ? L.T("info.firstRun") : L.F("info.needLogin", what), L.T("common.loginHcmut"), () => _host.Login());
    }

    private string? _errorClosed;   // lỗi đồng bộ người dùng đã đóng: lỗi khác (hoặc lỗi lần sau) thì hiện lại

    /// <summary>
    /// Lỗi đồng bộ gần nhất của LMS/MyBK (đang đồng bộ thì chưa tính) để hiện trên InfoBar: câu dễ hiểu (vấn đề + cách xử lý) và chữ
    /// kỹ thuật cho mục Chi tiết. Lỗi cả lượt, hoặc lượt xong nhưng có phần không đọc được.
    /// </summary>
    private (string Text, string? Detail, string Source, string Label)? SyncFailure()
    {
        var s = _host.State;
        foreach (var (name, label) in new[] { ("lms", "LMS"), ("mybk", "MyBK") })
        {
            if (s.Syncing(name)) continue;
            if (s.ExplainError(name, label) is { } err)
            {
                var (text, detail) = err;
                return (L.F("info.syncFailed", label, text), detail, name, label);
            }
            if (s.Warnings(name) is { Count: > 0 } w)
                return (L.F("info.syncWarnings", label, string.Join(", ", w.Select(x => x.What).Distinct().Take(4)) + (w.Count > 4 ? "..." : "")),
                        string.Join("\n", w.Select(x => $"{x.What}: {x.Detail}")), name, label);
        }
        return null;
    }

    /// <summary>
    /// Mức độ theo hướng dẫn InfoBar của Microsoft: Error = sự cố đã xảy ra (đồng bộ lỗi, đăng nhập lỗi), Warning = cần làm gì đó kẻo
    /// dữ liệu cũ dần (phiên hết, huỷ đăng nhập), Success = việc chạy nền xong (đăng nhập xong), Informational = đang làm, mời đăng nhập.
    /// </summary>
    private void ShowInfo(Severity severity, string text, string? action, Action? run)
    {
        if (text.Length == 0) { InfoBar.Hide(); return; }
        InfoBar.Show(severity, "", text, action, run);
    }

    private void OnInfoClose()
    {
        if (_loginStage.Length == 0)
        {
            if (_host.State.Account == AccountNeed.None && SyncFailure() is { } fail) _errorClosed = fail.Text;
            else _infoClosedFor = true;
        }
        _loginStage = "";
        _keepShown = false;
    }
}
