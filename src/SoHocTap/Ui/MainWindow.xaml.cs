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
    /// <summary>Dữ liệu vừa đổi (sync xong…) thì vẽ lại.</summary>
    void Refresh();
    /// <summary>Mở page tại một chỗ cụ thể (môn, tab…). Chuỗi rỗng thì giữ nguyên.</summary>
    void Open(string arg) { }
}

internal sealed record NavItem(string Id, string Label, string Glyph, string Tip)
{
    public string Badge { get; set; } = "";

    public override string ToString() => Label;
}

public partial class MainWindow : Window, IDisposable
{
    private readonly AppHost _host;
    private readonly WindowPlacement _placement = WindowPlacement.Load();
    private readonly Dictionary<string, IPage> _pages = [];
    private readonly Stack<string> _back = new();
    private readonly List<NavItem> _nav =
    [
        new("hom-nay", L.T("nav.today"), "", L.T("nav.today.tip")),
        new("lich", L.T("nav.calendar"), "", L.T("nav.calendar.tip")),
        new("mon", L.T("nav.subjects"), "", L.T("nav.subjects.tip")),
        new("diem", L.T("nav.grades"), "", L.T("nav.grades.tip")),
        new("dich-vu", L.T("nav.services"), "", L.T("nav.services.tip")),
    ];
    private string _current = "";
    private bool _exiting, _trayHintShown;
    private bool _infoClosedFor;
    private string _loginStage = "";
    private Action? _infoAction;

    public MainWindow()
    {
        InitializeComponent();
        Title = AppInfo.Name;
        if (AppInfo.Practice) _nav.Insert(3, new("luyen-tap", L.T("nav.practice"), "\uE73E", L.T("nav.practice.tip")));
        // Ctrl+1…N đi theo thứ tự trên thanh điều hướng, số tiếp theo là Cài đặt (hai edition có số mục khác nhau).
        for (var i = 0; i < _nav.Count; i++)
        {
            _nav[i] = _nav[i] with { Tip = $"{_nav[i].Tip} (Ctrl+{i + 1})" };
            InputBindings.Add(new KeyBinding(NavigationCommands.GoToPage, Key.D1 + i, ModifierKeys.Control) { CommandParameter = _nav[i].Id });
        }
        InputBindings.Add(new KeyBinding(NavigationCommands.GoToPage, Key.D1 + _nav.Count, ModifierKeys.Control) { CommandParameter = "cai-dat" });
        SettingsButton.ToolTip = $"{L.T("nav.settings.tip")} (Ctrl+{_nav.Count + 1})";
        StVersion.Text = $"{AppInfo.Name} {AppInfo.Version}{(AppInfo.Channel.Length > 0 ? " · " + AppInfo.Channel : "")}";
        StVersion.ToolTip = Core.Paths.AppRoot;
        _placement.Apply(this);
        _host = new AppHost(this);
        Nav.ItemsSource = _nav;
        _host.State.Changed += OnState;
        _host.LoginProgress += OnLogin;
        _host.Navigate += Go;
        _host.Updates.Changed += () => Dispatcher.InvokeAsync(ShowUpdateState);
        Closing += OnClosing;
        Closed += (_, _) => Dispose();
    }

    /// <summary>Khởi động: sync, bật scheduler, mở lại page lần trước. hidden = chỉ chạy dưới tray (khi khởi động cùng Windows).</summary>
    internal void Start(bool hidden)
    {
        new WindowInteropHelper(this).EnsureHandle();   // WebView2 ẩn (login, MyBK) cần có HWND dù window chưa hiện
        _host.Start();
        Go(string.IsNullOrEmpty(_placement.Page) ? "hom-nay" : _placement.Page);
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

    /// <summary>Thanh trạng thái: "Có bản x.y.z" hoặc "Khởi động lại để cập nhật".</summary>
    private void ShowUpdateState()
    {
        var u = _host.Updates;
        var notice = u.Offer is null ? UpdateService.StartupNotice : null;
        StUpdate.Visibility = u.Offer is null && notice is null ? Visibility.Collapsed : Visibility.Visible;
        if (u.Offer is { } o) StUpdateText.Text = u.Downloaded ? L.T("update.restart") : L.F("update.available", o.Version);
        else if (notice is not null) StUpdateText.Text = notice;
    }

    private void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_host.Updates.Offer is { } o) new UpdateWindow(_host.Updates, o) { Owner = this }.ShowDialog();
        else Go("cai-dat");   // báo cài lỗi: mở Cài đặt → Cập nhật để thử lại
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
            if (!_trayHintShown) { _host.TrayHint(); _trayHintShown = true; }
            return;
        }
        _placement.Capture(this);
        _placement.Page = _current;
        _placement.Save();
    }

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------------ điều hướng

    internal void Go(string route)
    {
        var parts = route.Split('/', 2);
        var id = _nav.Any(n => n.Id == parts[0]) || parts[0] is "cai-dat" or "gioi-thieu" ? parts[0] : "hom-nay";
        if (_current.Length > 0 && _current != id) _back.Push(_current);
        _current = id;
        if (!_pages.TryGetValue(id, out var page))
        {
            page = id switch
            {
                "lich" => new CalendarPage(_host),
                "mon" => new SubjectsPage(_host, this),
                "luyen-tap" => new PracticePage(_host, this),
                "diem" => new GradesPage(_host),
                "cai-dat" => new SettingsPage(_host),
                "gioi-thieu" => new AboutPage(),
                "dich-vu" => new ServicesPage(_host, this),
                _ => new HomePage(_host, this),
            };
            _pages[id] = page;
            page.Refresh();
        }
        Page.Content = page;
        if (parts.Length > 1) page.Open(parts[1]);
        // Page Luyện tập đã có breadcrumb riêng bên trong nên không cần dòng mô tả.
        PageSubtitle.Text = page.Subtitle;
        PageSubtitle.Visibility = page.Subtitle.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        var item = _nav.FirstOrDefault(n => n.Id == id);
        if (!ReferenceEquals(Nav.SelectedItem, item)) Nav.SelectedItem = item;
        SettingsButton.IsChecked = id == "cai-dat";
        AboutButton.IsChecked = id == "gioi-thieu";
    }

    private void OnNav(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is NavItem n && n.Id != _current) Go(n.Id);
    }

    private void OnGoTo(object sender, ExecutedRoutedEventArgs e) => Go((string)e.Parameter);

    /// <summary>Ctrl+số từ khung Luyện tập: mục thứ n trên thanh điều hướng, n = số mục + 1 là Cài đặt.</summary>
    internal void GoIndex(int n) => Go(n >= 1 && n <= _nav.Count ? _nav[n - 1].Id : n == _nav.Count + 1 ? "cai-dat" : _current);

    /// <summary>Số phím Ctrl+số đang dùng (các mục điều hướng và Cài đặt).</summary>
    internal static int PageKeys => AppInfo.Practice ? 7 : 6;
    private void OnSettings(object sender, RoutedEventArgs e) => Go("cai-dat");
    private void OnAbout(object sender, RoutedEventArgs e) => Go("gioi-thieu");
    private void OnBack(object sender, ExecutedRoutedEventArgs e) => Back();

    internal void Back()
    {
        if (_back.Count == 0) return;
        var to = _back.Pop();
        _current = "";
        Go(to);
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

    internal void Say(string text) => StMessage.Text = text;

    private void OnState()
    {
        var s = _host.State;
        string Src(string name, string label)
        {
            if (s.Syncing(name)) return L.F("status.syncing", label);
            if (s.Error(name) is { } err) return L.F("status.syncError", label);
            return s.SyncedAt(name) is { } t ? L.F("status.syncedAt", label, Format.DayDiff(t) == 0 ? Format.Hm(t) : Format.DateTime(t)) : L.F("status.neverSynced", label);
        }
        StLms.Text = Src("lms", "LMS");
        StLms.ToolTip = s.Error("lms");
        StMybk.Text = Src("mybk", "MyBK");
        StMybk.ToolTip = s.Error("mybk");
        StAccount.Text = L.T(s.Account == AccountNeed.None ? "status.signedIn" : "status.signedOut");
        var syncing = s.Syncing("lms") || s.Syncing("mybk");
        SyncButton.IsEnabled = !syncing;
        SyncText.Text = L.T(syncing ? "nav.syncing" : "nav.sync");
        if (!syncing && StMessage.Text == L.T("status.syncStarted")) Say(L.T("status.synced"));

        var exam = s.Timeline.FirstOrDefault(e => e.Kind == "exam" && e.Time > Format.Now);
        StExam.Text = exam is null ? "" : L.F("status.exam", exam.Subject, Format.DayDiff(exam.Time) is var d && d > 0 ? L.F("format.days", d) : L.T("status.examToday"));

        _nav[0].Badge = s.Upcoming(24).Count(e => !e.Done && e.Kind is not ("class" or "exam")) is var n && n > 0 ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
        Nav.Items.Refresh();
        Nav.SelectedItem = _nav.FirstOrDefault(x => x.Id == _current);
        SettingsButton.IsChecked = _current == "cai-dat";
        AboutButton.IsChecked = _current == "gioi-thieu";

        UpdateInfoBar();
        foreach (var p in _pages.Values) p.Refresh();
        if (_pages.TryGetValue(_current, out var cur)) PageSubtitle.Text = cur.Subtitle;
    }

    // ------------------------------------------------------------------ InfoBar: đăng nhập

    /// <summary>Bước login → key thông báo trong file ngôn ngữ.</summary>
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
        ShowInfo(text, stage is "error" or "cancel", null, null);
        if (stage is "done" or "logout")
        {
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            t.Tick += (_, _) => { t.Stop(); _loginStage = ""; UpdateInfoBar(); };
            t.Start();
        }
    }

    private void UpdateInfoBar()
    {
        if (_loginStage.Length > 0) return;   // đang hiện tiến độ login
        var need = _host.State.Account;
        if (need == AccountNeed.None || _infoClosedFor) { InfoBar.Visibility = Visibility.Collapsed; if (need == AccountNeed.None) _infoClosedFor = false; return; }
        var what = need switch { AccountNeed.Both => L.T("info.both"), AccountNeed.Lms => "LMS", _ => "MyBK" };
        // Lần đầu chạy (chưa có dữ liệu gì) thì mời đăng nhập, chứ không báo hết session.
        var firstRun = _host.State.Lms is null && _host.State.Mybk is null;
        ShowInfo(firstRun ? L.T("info.firstRun") : L.F("info.needLogin", what), !firstRun, L.T("common.loginHcmut"), () => _host.Login());
    }

    private void ShowInfo(string text, bool warn, string? action, Action? run)
    {
        InfoText.Text = text;
        InfoIcon.Text = warn ? "" : "";
        InfoBar.Background = (System.Windows.Media.Brush)FindResource(warn ? "SystemFillColorCautionBackgroundBrush" : "SystemFillColorAttentionBackgroundBrush");
        InfoAction.Visibility = action is null ? Visibility.Collapsed : Visibility.Visible;
        InfoAction.Content = action;
        _infoAction = run;
        InfoBar.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnInfoAction(object sender, RoutedEventArgs e) => _infoAction?.Invoke();
    private void OnInfoClose(object sender, RoutedEventArgs e)
    {
        if (_loginStage.Length == 0) _infoClosedFor = true;
        _loginStage = "";
        InfoBar.Visibility = Visibility.Collapsed;
    }
}
