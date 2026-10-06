using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using BKStudyDesk.Desktop.Views;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Presentation;
using SoHocTap.Sources;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop;

/// <summary>
/// Khung cửa sổ: thanh trên cùng (mục trang chỉ chữ, mục đang mở đậm và gạch dưới), chữ đồng bộ, vùng nội dung.
/// Mỗi trang là một View trong Views/; trang nào hiện gì nằm ở BKStudyDesk.Core/Presentation.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppHost _host = new();
    private readonly (string Title, Func<Control>? Build)[] _pages;
    private int _current;
    private int _shownIndex = -1;
    /// <summary>Số mục trang trên thanh trên cùng; trang ngay sau đó là Cài đặt (nút bên phải).</summary>
    private const int NavCount = 5;
    private SubjectsView? _subjects;
    private PracticeView? _practice;   // giữ một instance: chuyển trang không mất bài đang làm   // trang Môn học đang hiện: dựng lại (dữ liệu mới) thì giữ môn đang chọn

    public MainWindow()
    {
        InitializeComponent();
        _pages =
        [
            ("Hôm nay", () => new HomeView(_host.State, () => _host.Login(this))),
            ("Môn học", () => _subjects = new SubjectsView(_host.State, _host.Library, _subjects, Download)),
            ("Lịch", () => new CalendarView(_host.State)),
            ("Luyện tập", () => _practice ??= new PracticeView(_host.Practice, OnPracticeMessage)),
            ("MyBK", () => new MybkView(_host.State)),
            ("Cài đặt", () => new SettingsView(SettingsActions())),   // nút bên phải thanh trên cùng, Ctrl+6
        ];
        for (var i = 0; i < NavCount; i++)
        {
            var index = i;
            var item = new Button
            {
                Classes = { "bar" }, Height = 48,
                Content = new Grid { Children = { new TextBlock { Text = _pages[i].Title, VerticalAlignment = VerticalAlignment.Center },
                    new Border { Height = 3, Background = Avalonia.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Bottom, IsVisible = false } } },
            };
            item.Click += (_, _) => Show(index);
            Nav.Children.Add(item);
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.D1 + i, KeyModifiers.Control), Command = new Command(() => Show(index)) });
        }
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.D1 + NavCount, KeyModifiers.Control), Command = new Command(() => Show(NavCount)) });
        SettingsButton.Click += (_, _) => Show(NavCount);
        MoreSettings.Click += (_, _) => Show(NavCount);
        // Dữ liệu mới: dựng lại trang đang xem. Cài đặt không vẽ dữ liệu đồng bộ: giữ nguyên (đang gõ dở trong ô thì không mất chữ).
        _host.State.Changed += () => { if (_current != NavCount) Show(_current); ShowSync(); };
        _host.State.StatusChanged += ShowSync;
        _host.State.Progress += ShowSync;
        var accountMenu = (MenuFlyout)AccountButton.Flyout!;
        accountMenu.Opening += (_, _) => Fill(accountMenu.Items, AccountItems());
        MoreAccount.SubmenuOpened += (_, _) => Fill(MoreAccount.Items, AccountItems());
        MoreAccount.Items.Add(new MenuItem());   // chỗ giữ để mục có mũi tên mở menu con; dựng thật khi mở
        _host.LoginProgress += (_, _) => ShowSync();
        Bar.SizeChanged += (_, _) => FitBar();
        Closed += (_, _) => _host.Dispose();
        _host.OpenUrl += u => _ = Launcher.LaunchUriAsync(u);
        _host.Start();
        SetupTray();
        var args = Environment.GetCommandLineArgs();
        Show(args.Contains("--lich") ? 2 : args.Contains("--mon") ? 1 : args.Contains("--luyen") ? 3 : args.Contains("--mybk") ? 4 : args.Contains("--cai-dat") ? NavCount : 0);
        if (args.FirstOrDefault(a => a.StartsWith("--tab="))?[6..] is { } tab && int.TryParse(tab, out var t))   // chụp kiểm tra
        {
            _subjects?.SelectTab(t);
            if (Page.Child is MybkView mybk) mybk.Tabs.SelectedIndex = t;
            if (Page.Child is SettingsView settings && t == 1) settings.ExpandAdvanced();
        }
        ShowSync();
        if (args.FirstOrDefault(a => a.StartsWith("--snap="))?[7..] is { Length: > 0 } snap) SnapAndClose(snap);
#if WINDOWS
        if (args.Contains("--toastcheck")) { Log.Info($"Toastcheck: {Platform.Windows.Toast.Setting()}"); Environment.Exit(0); }   // không gửi thông báo
#endif
        if (args.Contains("--login")) Opened += (_, _) => _host.Login(this);
        if (args.Contains("--webcheck")) Opened += async (_, _) => { Environment.ExitCode = await Web.WebCheck.RunAsync() ? 0 : 1; _exiting = true; Close(); };
    }

    private void Show(int index)
    {
        _current = index;
        for (var i = 0; i < Nav.Children.Count; i++)
        {
            var b = (Button)Nav.Children[i];
            b.Classes.Set("on", i == index);
            ((Grid)b.Content!).Children[1].IsVisible = i == index;
        }
        SettingsButton.Classes.Set("on", index == NavCount);
        var page = _pages[index].Build?.Invoke() ?? new TextBlock { Text = "Trang này có ở bản sau.", Classes = { "meta" } };
        // Trang lấp đầy (bảng) nhận đúng khung cửa sổ và tự cuộn; trang thường cuộn cả vùng.
        Scroller.VerticalScrollBarVisibility = page is IFillPage ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        Page.MaxWidth = page is IFillPage ? double.PositiveInfinity : (double)this.FindResource("ContentMaxWidth")!;   // bảng dùng hết bề ngang, trang chữ giữ độ rộng dễ đọc
        var changed = index != _shownIndex;
        Page.Child = page;
        if (changed) Scroller.Offset = default;   // trang khác: về đầu trang (dựng lại cùng trang vì dữ liệu mới thì giữ chỗ đang xem)
        _shownIndex = index;
    }

    private void ShowSync()
    {
        MoreSync.Header = SyncText.Text = SyncStatus.Text(_host.State.Syncing(SourceIds.Lms), _host.Hub.Failed(SourceIds.Lms), _host.State.SyncedAt(SourceIds.Lms), Format.Now);
        FitBar();
    }

    /// <summary>
    /// Thanh trên cùng không đặt mốc độ rộng cứng: đo chữ thật (đổi theo ngôn ngữ, cỡ chữ, chữ đồng bộ) rồi chọn cách đầy đủ nhất còn vừa.
    /// Thứ tự bỏ bớt: tên app, rồi gom phần phải vào nút Thêm. Mục trang không bao giờ bị giấu.
    /// </summary>
    private void FitBar()
    {
        var room = Bar.Bounds.Width - 48 - 24;   // lề hai bên 24, chừa 24 giữa mục trang và phần phải
        if (room <= 0) return;
        Brand.IsVisible = Tools.IsVisible = More.IsVisible = true;
        double W(Control c) { c.Measure(Size.Infinity); return c.DesiredSize.Width; }
        double nav = W(Nav), brand = W(Brand), tools = W(Tools);
        Brand.IsVisible = nav + brand + tools <= room;
        Tools.IsVisible = Brand.IsVisible || nav + tools <= room;
        More.IsVisible = !Tools.IsVisible;
    }

    /// <summary>
    /// Menu Tài khoản: dòng đầu là tình trạng (tên LMS hay "Cần đăng nhập"), rồi Đăng nhập (Đăng nhập lại), Đăng xuất. Đăng xuất
    /// nằm trong menu con một mục để không bấm nhầm (Avalonia không có hộp thoại xác nhận sẵn).
    /// </summary>
    private IEnumerable<MenuItem> AccountItems()
    {
        var s = _host.State;
        var need = s.Account;
        yield return new MenuItem { IsEnabled = false, Header = need == AccountNeed.None
            ? (s.Lms?.User is { } u ? L.F("settings.signedInAs", u) : L.T("settings.signedIn")) : L.T("settings.needLogin") };
        var login = new MenuItem { Header = L.T(need == AccountNeed.None ? "settings.relogin" : "common.loginHcmut") };
        login.Click += (_, _) => _host.Login(this);
        yield return login;
        var confirm = new MenuItem { Header = "Đăng xuất khỏi HCMUT (dữ liệu đã đồng bộ vẫn còn)" };
        confirm.Click += async (_, _) => await _host.LogoutAsync();
        yield return new MenuItem { Header = L.T("settings.logout"), Items = { confirm } };
    }

    private static void Fill(Avalonia.Controls.ItemCollection items, IEnumerable<MenuItem> fresh)
    {
        items.Clear();
        foreach (var item in fresh) items.Add(item);
    }

    /// <summary>
    /// Tin từ khung Luyện tập: phím tắt của app khi khung đang giữ focus (Ctrl+số, F5) và route của trang khác của app
    /// ("mon/...", "lich", "mybk", "cai-dat"): chuyển sang trang tương ứng, route lạ thì về Hôm nay.
    /// </summary>
    private void OnPracticeMessage(System.Text.Json.Nodes.JsonObject m)
    {
        var type = m["type"]?.GetValue<string>();
        if (type == "key")
        {
            var key = m["key"]?.GetValue<string>() ?? "";
            if (key == "F5") _host.Hub.Start(SourceIds.Lms, force: true);
            else if (key.StartsWith("Ctrl+", StringComparison.Ordinal) && int.TryParse(key[5..], out var n) && n >= 1 && n <= _pages.Length) Show(n - 1);
        }
        else if (type == "navigate")
        {
            Show(PageOf(m["route"]?.GetValue<string>() ?? ""));
        }
    }

    private bool _exiting;   // Thoát từ khay: đóng thật, không thu xuống khay
    private Platform.Notifier? _notifier;

    /// <summary>
    /// Khay hệ thống (TrayIcon có sẵn của Avalonia): Mở, Đồng bộ ngay, Thoát; bấm vào biểu tượng thì mở cửa sổ. Nút X thu xuống khay khi
    /// bật app.closeToTray (mặc định), app vẫn đồng bộ và nhắc hạn. Nhắc hạn bắt đầu ở đây vì cần cửa sổ để hiện.
    /// </summary>
    private void SetupTray()
    {
        var open = new NativeMenuItem($"Mở {AppInfo.Name}");
        open.Click += (_, _) => ShowFromTray();
        var sync = new NativeMenuItem("Đồng bộ ngay");
        sync.Click += (_, _) => { _host.Hub.Start(SourceIds.Lms, force: true); _host.Hub.Start(SourceIds.Mybk, force: true); };
        var exit = new NativeMenuItem("Thoát");
        exit.Click += (_, _) => { _exiting = true; Close(); };
        var tray = new TrayIcon
        {
            Icon = Icon, ToolTipText = AppInfo.Name,
            Menu = new NativeMenu { Items = { open, sync, new NativeMenuItemSeparator(), exit } },
        };
        tray.Clicked += (_, _) => ShowFromTray();
        TrayIcon.SetIcons(Application.Current!, [tray]);
        Closing += (_, e) =>
        {
            if (_exiting || !SoHocTap.Core.Settings.App.CloseToTray) return;
            e.Cancel = true;
            Hide();
        };
        _notifier = new Platform.Notifier(this, page => { ShowFromTray(); Show(PageOf(page)); });
        _host.UpdateOffered += v => _notifier.Show(L.F("update.available", v), "Vào Cài đặt, mục Cập nhật để tải và cài.", "cai-dat");
        // Update.exe đang đợi app thoát để cài bản mới: thoát hẳn (không thu xuống khay).
        _host.Updates.Restarting += _ => Avalonia.Threading.Dispatcher.UIThread.Post(() => { _exiting = true; Close(); });
        // Cửa sổ đã hiện và dựng xong lớp phủ: gắn chỗ hiện thông báo rồi mới bắt đầu nhắc (lời nhắc đầu tiên không bị rơi).
        Opened += (_, _) =>
        {
            _notifier.Attach();
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _host.StartReminders((title, body, page) => _notifier.Show(title, body, page)),
                Avalonia.Threading.DispatcherPriority.Background);
        };
    }

    /// <summary>Mở lúc đăng nhập máy (--tray): không hiện cửa sổ; đồng bộ đã chạy, nhắc hạn bắt đầu ngay (hiện bằng thông báo hệ điều hành).</summary>
    public void StartHidden() => _host.StartReminders((title, body, page) => _notifier!.Show(title, body, page));

    private void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        _notifier?.Flush();
    }

    /// <summary>Route của lời nhắc, khung Luyện tập ("lich", "mon/...", "luyen-tap"...) thành số thứ tự trang.</summary>
    private static int PageOf(string route) => route.Split('/')[0] switch
    {
        "mon" or "tl" => 1, "lich" => 2, "luyen-tap" => 3, "mybk" or "diem" => 4, "cai-dat" => NavCount, _ => 0,
    };

    /// <summary>Việc của trang Cài đặt cần tới cửa sổ và phần chạy nền.</summary>
    private SoHocTap.Presentation.SettingsActions SettingsActions() => new(
        AccountText: () => AccountItems().First().Header as string ?? "",
        Login: () => _host.Login(this),
        Logout: _host.LogoutAsync,
        ApplyTheme: App.ApplyTheme,
        SyncNow: () => { _host.Hub.Start(SourceIds.Lms, force: true); _host.Hub.Start(SourceIds.Mybk, force: true); },
        OpenLog: async () => { if (File.Exists(SoHocTap.Core.Paths.DataFile("app.log"))) await Launcher.LaunchFileInfoAsync(new FileInfo(SoHocTap.Core.Paths.DataFile("app.log"))); },
        SetLibraryUrl: _host.Library.SetBaseUrl,
        ClearLibrary: _host.Library.ClearCache,
        AutostartGet: () => Platform.Autostart.Enabled,
        AutostartSet: Platform.Autostart.Set,
        AutoLoginSupported: Platform.Credentials.Supported,
        AutoLoginToggle: async () =>
        {
            if (SoHocTap.Core.Settings.Sso.AutoLogin) { Platform.Credentials.Forget(); return "Đã tắt và xóa tài khoản đã lưu."; }
            return await new AutoLoginWindow().ShowDialog<bool>(this) ? "Đã bật." : null;
        },
        ForgetCredentials: Platform.Credentials.Forget,
        UpdateSupported: SoHocTap.Updates.UpdateService.CanSelfUpdate,
        UpdateCheck: async () => await _host.Updates.CheckAsync(manual: true, default) is { } o ? L.F("update.available", o.Version)
            : _host.Updates.LastError is { } err ? L.F("update.error", err) : L.F("update.latest", AppInfo.Version),
        UpdateInstall: async () =>
        {
            if (_host.Updates.Offer is null && await _host.Updates.CheckAsync(manual: true, default) is null) return L.F("update.latest", AppInfo.Version);
            if (!await _host.Updates.DownloadAsync(userAsked: true, default)) return L.F("update.failed", _host.Updates.LastError ?? "");
            _host.Updates.ApplyAndRestart();   // Restarting: cửa sổ đóng hẳn để Update.exe cài
            return L.F("update.installing", _host.Updates.Offer?.Version ?? "");
        });

    /// <summary>Cửa sổ Tải tài liệu của một lớp; tải xong thì trang Môn học đọc lại thư mục.</summary>
    private void Download(SoHocTap.Data.LmsCourse course)
    {
        if (_host.Hub.Get(SourceIds.Lms) is not SoHocTap.Sources.Lms.LmsSource lms) return;
        var w = new DownloadWindow(lms, course);
        w.Closed += (_, _) => Show(_current);
        w.ShowDialog(this);
    }


    private void OnSync(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _host.Hub.Start(SourceIds.Lms, force: true);
    }

    /// <summary>
    /// --snap=file.png: dựng xong thì tự vẽ cửa sổ ra ảnh rồi thoát. Dùng để chụp kiểm tra giao diện trên mọi hệ điều hành
    /// (Linux, macOS, máy CI) mà không chụp màn hình người dùng.
    /// </summary>
    private void SnapAndClose(string path) => Opened += async (_, _) =>
    {
        await Task.Delay(1500);   // chờ bố cục và chữ xong
        var size = new PixelSize((int)(Bounds.Width * RenderScaling), (int)(Bounds.Height * RenderScaling));
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(size, new Vector(96 * RenderScaling, 96 * RenderScaling));
        bitmap.Render(this);
        bitmap.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        _exiting = true;
        Close();
    };

    /// <summary>ICommand nhỏ cho phím tắt (Avalonia không có sẵn lớp lệnh đơn giản).</summary>
    private sealed class Command(Action run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => run();
    }
}
