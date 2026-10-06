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
    private readonly (string Title, Func<Control> Build)[] _pages;
    private int _current;
    private int _shownIndex = -1;
    /// <summary>Số mục trang trên thanh trên cùng; trang ngay sau đó là Cài đặt (nút bên phải).</summary>
    private const int NavCount = 5;
    private SubjectsView? _subjects;   // trang Môn học đang hiện: dựng lại (dữ liệu mới) thì giữ môn, tab, thư mục đang xem
    private PracticeView? _practice;   // giữ một instance: chuyển trang không mất bài đang làm

    public MainWindow()
    {
        InitializeComponent();
        _pages =
        [
            (L.T("nav.today"), () => new HomeView(_host.State, () => _host.Login(this))),
            (L.T("nav.subjects"), () => _subjects = new SubjectsView(_host.State, _host.Library, _subjects, Download)),
            (L.T("nav.calendar"), () => new CalendarView(_host.State)),
            (L.T("nav.practice"), () => _practice ??= new PracticeView(_host.Practice, OnPracticeMessage)),
            ("MyBK", () => new MybkView(_host.State)),
            (L.T("nav.settings"), () => new SettingsView(SettingsActions())),   // nút bên phải thanh trên cùng, Ctrl+6
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
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F5), Command = new Command(SyncNow) });   // như 1.x
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
        var moreMenu = (MenuFlyout)More.Flyout!;
        moreMenu.Opening += (_, _) => FillMore(moreMenu);
        _host.LoginProgress += (_, _) => ShowSync();
        var statusFlyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        statusFlyout.Opening += (_, _) => statusFlyout.Content = StatusPanelContent(statusFlyout);
        statusFlyout.Closed += (_, _) => ShowSync();
        StatusButton.Flyout = statusFlyout;
        _notices.Changed += ShowSync;
        Bar.SizeChanged += (_, _) => FitBar();
        Closed += (_, _) => _host.Dispose();
        _host.OpenUrl += u => _ = Files.Links.OpenAsync(this, u.AbsoluteUri);   // khung Luyện tập nhờ mở web
        _host.Start();
        SetupTray();
        Platform.SingleInstance.Listen(() => Avalonia.Threading.Dispatcher.UIThread.Post(ShowFromTray));   // mở app lần nữa: hiện cửa sổ này
        // Lỗi không ai bắt (thường từ việc chạy nền): ghi vào bảng Thông báo, không hiện thông báo hệ điều hành.
        AppEvents.UnhandledError += e => Avalonia.Threading.Dispatcher.UIThread.Post(() => _notices.Add(new Notice(Format.Now, L.T("error.unhandled"), e.Message, "")));
        var args = Environment.GetCommandLineArgs();
        Show(args.Contains("--lich") ? 2 : args.Contains("--mon") ? 1 : args.Contains("--luyen") ? 3 : args.Contains("--mybk") ? 4 : args.Contains("--cai-dat") ? NavCount : 0);
        if (args.FirstOrDefault(a => a.StartsWith("--tab="))?[6..] is { } tab && int.TryParse(tab, out var t))   // chụp kiểm tra
        {
            _subjects?.SelectTab(t);
            if (Page.Child is CalendarView calendar) calendar.SelectTab(t);
            if (Page.Child is MybkView mybk) mybk.Tabs.SelectedIndex = t;
            if (Page.Child is SettingsView settings && t == 1) settings.ExpandAdvanced();
        }
        ShowSync();
        if (args.FirstOrDefault(a => a.StartsWith("--snap="))?[7..] is { Length: > 0 } snap) SnapAndClose(snap);
#if WINDOWS
        if (args.Contains("--toastcheck")) { Log.Info($"Toastcheck: {Platform.Windows.Toast.Setting()}"); Environment.Exit(0); }   // không gửi thông báo
#endif
        if (args.Contains("--panel")) Opened += (_, _) => StatusButton.Flyout?.ShowAt(StatusButton);   // chụp kiểm tra bảng Thông báo
        if (args.Contains("--login")) Opened += (_, _) => _host.Login(this);
        if (args.Contains("--event")) Opened += async (_, _) => await new EventWindow(null, null).ShowDialog(this);   // chụp kiểm tra hộp thoại Thêm sự kiện
        if (args.Contains("--setup")) Opened += (_, _) => { var (f, u) = FirstRunWindow.Both(); f.Show(this); u.Show(this); };   // chụp kiểm tra câu hỏi lần đầu
        if (args.Contains("--webcheck")) Opened += async (_, _) => { Environment.ExitCode = await Web.WebCheck.RunAsync() ? 0 : 1; _exiting = true; Close(); };
    }

    private async void Show(int index)
    {
        // Rời Luyện tập: khung bị hủy khi gỡ khỏi cửa sổ, nên ghi nốt kết quả đang chờ trước.
        if (index != _shownIndex && Page.Child is PracticeView practice) await practice.FlushAsync();
        _current = index;
        for (var i = 0; i < Nav.Children.Count; i++)
        {
            var b = (Button)Nav.Children[i];
            b.Classes.Set("on", i == index);
            ((Grid)b.Content!).Children[1].IsVisible = i == index;
        }
        SettingsButton.Classes.Set("on", index == NavCount);
        More.Classes.Set("on", index == NavCount);   // Cài đặt nằm trong nút Thêm khi thanh hẹp
        if (index != _shownIndex) FitBar();   // trang đang xem luôn có mục trên thanh
        var page = _pages[index].Build();
        // Trang lấp đầy (bảng) nhận đúng khung cửa sổ và tự cuộn; trang thường cuộn cả vùng.
        Scroller.VerticalScrollBarVisibility = page is IFillPage ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        Page.MaxWidth = page is IFillPage ? double.PositiveInfinity : (double)this.FindResource("ContentMaxWidth")!;   // bảng dùng hết bề ngang, trang chữ giữ độ rộng dễ đọc
        var changed = index != _shownIndex;
        Page.Child = page;
        if (changed) Scroller.Offset = default;   // trang khác: về đầu trang (dựng lại cùng trang vì dữ liệu mới thì giữ chỗ đang xem)
        _shownIndex = index;
    }

    private readonly NoticeLog _notices = new();

    /// <summary>Chữ trên nút Thông báo: đang đồng bộ, lỗi ở nguồn nào, giờ xong gần nhất, số thông báo chưa xem (StatusPanel.Bar).</summary>
    private void ShowSync()
    {
        var rows = Sources();
        var last = new[] { _host.State.SyncedAt(SourceIds.Lms), _host.State.SyncedAt(SourceIds.Mybk) }.Max();
        var (text, attention) = StatusPanel.Bar(rows, _notices.Unread, last);
        StatusText.Text = text;
        StatusText.FontWeight = attention ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal;
        ToolTip.SetTip(StatusButton, string.Join(Environment.NewLine, rows.Select(r => r.Problem is { } p ? $"{r.State}. {p}" : r.State)));
        FitBar();
    }

    private IReadOnlyList<SourceState> Sources() =>
        [StatusPanel.Source(_host.State, SourceIds.Lms, "LMS"), StatusPanel.Source(_host.State, SourceIds.Mybk, "MyBK")];

    /// <summary>
    /// Bảng của nút Thông báo (Flyout có sẵn): từng nguồn một dòng (trạng thái, vấn đề nếu có, nút Đăng nhập lại hay Thử lại),
    /// nút Đồng bộ ngay, rồi các thông báo gần đây (bấm để mở trang). Mở bảng là đã xem các thông báo.
    /// </summary>
    private Control StatusPanelContent(Flyout owner)
    {
        var panel = new StackPanel { Spacing = 12, Width = 380 };
        panel.Children.Add(new TextBlock { Text = L.T("status.panel.sync"), Classes = { "card-title" } });
        foreach (var r in Sources())
        {
            var line = new StackPanel { Spacing = 2 };
            line.Children.Add(new TextBlock { Text = r.State, TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontWeight = r.Problem is null ? Avalonia.Media.FontWeight.Normal : Avalonia.Media.FontWeight.SemiBold });
            if (r.Problem is { } p) line.Children.Add(new TextBlock { Text = p, Classes = { "meta", "danger" } });
            if (r.Detail is { Length: > 0 } d) line.Children.Add(new TextBlock { Text = d, Classes = { "sub" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, TextTrimming = Avalonia.Media.TextTrimming.None });
            if (r.Problem is not null)
            {
                var fix = new Button { Content = L.T(r.NeedsLogin ? "status.panel.relogin" : "web.retry"), Margin = new Thickness(0, 4, 0, 0) };
                fix.Click += (_, _) => { owner.Hide(); if (r.NeedsLogin) _host.Login(this); else SyncNow(); };
                line.Children.Add(fix);
            }
            panel.Children.Add(line);
        }
        var sync = new Button { Content = L.T("status.panel.syncNow") };
        sync.Click += (_, _) => { SyncNow(); owner.Hide(); };
        panel.Children.Add(sync);
        panel.Children.Add(new Separator());
        panel.Children.Add(new TextBlock { Text = L.T("status.panel.notices"), Classes = { "card-title" } });
        if (_notices.Items.Count == 0) panel.Children.Add(new TextBlock { Text = L.T("status.panel.noNotices"), Classes = { "meta" } });
        foreach (var n in _notices.Items.Take(8))
        {
            var item = new Button { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Content = new StackPanel { Spacing = 2, Children =
                {
                    new TextBlock { Text = n.Title, FontWeight = Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new TextBlock { Text = n.Body, Classes = { "sub" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, TextTrimming = Avalonia.Media.TextTrimming.None },
                    new TextBlock { Text = Format.Ago(n.Time), Classes = { "sub" } },
                } } };
            item.Click += (_, _) => { owner.Hide(); Show(PageOf(n.Page)); };
            panel.Children.Add(item);
        }
        _notices.MarkRead();
        return new ScrollViewer { MaxHeight = 560, Content = panel };
    }

    private void SyncNow()
    {
        _host.Hub.Start(SourceIds.Lms, force: true);
        _host.Hub.Start(SourceIds.Mybk, force: true);
    }

    /// <summary>
    /// Thanh trên cùng không đặt mốc độ rộng cứng: đo chữ thật (đổi theo ngôn ngữ, cỡ chữ, chữ đồng bộ) rồi chọn cách đầy đủ nhất còn vừa.
    /// Thứ tự bỏ bớt: tên app, rồi gom phần phải vào nút Thêm. Mục trang không bao giờ bị giấu.
    /// </summary>
    private void FitBar()
    {
        var room = Bar.Bounds.Width - 32;   // lề hai bên 16
        if (room <= 0) return;
        foreach (var b in Nav.Children) b.IsVisible = true;
        Tools.IsVisible = true;
        More.IsVisible = false;
        double W(Control c) { c.Measure(Size.Infinity); return c.DesiredSize.Width; }   // DesiredSize đã gồm Margin
        if (W(Nav) + W(StatusButton) + W(Tools) <= room) return;
        Tools.IsVisible = false;
        More.IsVisible = true;
        // Vẫn chật: mục trang từ cuối lên (trừ trang đang xem) chuyển vào nút Thêm.
        for (var i = NavCount - 1; i >= 0 && W(Nav) + W(StatusButton) + W(More) > room; i--)
            if (i != _current) Nav.Children[i].IsVisible = false;
    }

    /// <summary>Menu nút Thêm: các trang không vừa thanh (FitBar ẩn), rồi Tài khoản, Cài đặt.</summary>
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
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        menu.Items.Add(MoreAccount);
        menu.Items.Add(MoreSettings);
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
        var confirm = new MenuItem { Header = L.T("account.logoutConfirm") };
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
            if (key == "F5") SyncNow();
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
        var open = new NativeMenuItem(L.F("tray.open", AppInfo.Name));
        open.Click += (_, _) => ShowFromTray();
        var sync = new NativeMenuItem(L.T("status.panel.syncNow"));
        sync.Click += (_, _) => { _host.Hub.Start(SourceIds.Lms, force: true); _host.Hub.Start(SourceIds.Mybk, force: true); };
        var exit = new NativeMenuItem(L.T("tray.exit"));
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
            _ = SleepAsync();
        };
        _notifier = new Platform.Notifier(this, page => { ShowFromTray(); Show(PageOf(page)); });
        _host.UpdateOffered += v => Notify(L.F("update.available", v), L.T("notice.updateBody"), "cai-dat");
        // Update.exe đang đợi app thoát để cài bản mới: thoát hẳn (không thu xuống khay).
        _host.Updates.Restarting += _ => Avalonia.Threading.Dispatcher.UIThread.Post(() => { _exiting = true; Close(); });
        // Cửa sổ đã hiện và dựng xong lớp phủ: gắn chỗ hiện thông báo rồi mới bắt đầu nhắc (lời nhắc đầu tiên không bị rơi).
        Opened += (_, _) =>
        {
            // Lần đầu mở (như 1.x): hỏi nơi lưu tài liệu, rồi lần mở sau hỏi chế độ cập nhật; mỗi lần mở chỉ một câu.
            if (!Environment.GetCommandLineArgs().Any(a => a.StartsWith("--snap=", StringComparison.Ordinal)) && FirstRunWindow.Next() is { } ask)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ask.ShowDialog(this), Avalonia.Threading.DispatcherPriority.Background);
            _notifier.Attach();
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _host.StartReminders(Notify), Avalonia.Threading.DispatcherPriority.Background);
        };
    }

    /// <summary>Một lời nhắc, thông báo: ghi vào danh sách Thông báo của app và hiện ngay (trong app hay của hệ điều hành).</summary>
    private void Notify(string title, string body, string page)
    {
        _notices.Add(new Notice(Format.Now, title, body, page));
        _notifier!.Show(title, body, page);
    }

    /// <summary>Mở lúc đăng nhập máy (--tray): không hiện cửa sổ; đồng bộ đã chạy, nhắc hạn bắt đầu ngay (hiện bằng thông báo hệ điều hành).</summary>
    public void StartHidden()
    {
        // Chạy ngầm lần đầu: lưu luôn thư mục gợi ý (không có cửa sổ để hỏi), lần mở cửa sổ sau không hỏi nữa.
        if (FirstRun.NeedsFolder) FirstRun.SaveFolder(Paths.SuggestedRoot);
        _host.StartReminders(Notify);
    }

    /// <summary>
    /// Xuống khay khi đang ở Luyện tập: cửa sổ ẩn vẫn giữ khung (vài trăm MB), nên ghi nốt kết quả rồi gỡ trang (khung bị hủy) như 1.x.
    /// Mở lại từ khay thì dựng lại đúng trang.
    /// </summary>
    private async Task SleepAsync()
    {
        if (Page.Child is not PracticeView practice) return;
        await practice.FlushAsync();
        if (IsVisible) return;   // đã mở lại trong lúc chờ
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
        SyncNow: SyncNow,
        ApplyTheme: App.ApplyTheme,
        ApplyAccent: App.ApplyAccent,
        PickRoot: async () => (await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = L.T("settings.rootPick") }))
            .FirstOrDefault()?.TryGetLocalPath(),
        OpenLink: url => Files.Links.OpenAsync(this, url),
        OpenLog: async () => { if (File.Exists(Log.LogFile)) await Launcher.LaunchFileInfoAsync(new FileInfo(Log.LogFile)); },
        SetLibraryUrl: _host.Library.SetBaseUrl,
        ClearLibrary: _host.Library.ClearCache,
        AutostartGet: () => Platform.Autostart.Enabled,
        AutostartSet: Platform.Autostart.Set,
        AutoLoginSupported: Platform.Credentials.Supported,
        AutoLoginToggle: async () =>
        {
            if (SoHocTap.Core.Settings.Sso.AutoLogin) { Platform.Credentials.Forget(); return L.T("set.autoLoginOffDone"); }
            return await new AutoLoginWindow().ShowDialog<bool>(this) ? L.T("set.autoLoginOnDone") : null;
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
        },
        PageCount: _pages.Length,
        Refresh: () => Avalonia.Threading.Dispatcher.UIThread.Post(() => Show(NavCount)),   // dựng lại trang Cài đặt sau khi ô chọn xong việc
        Restart: () => { if (Platform.SingleInstance.StartRestart()) { _exiting = true; Close(); } },
        Fonts: App.InstalledFonts,
        ApplyFont: (family, size) =>
        {
            var choice = SoHocTap.Ui.FontChoice.Parse(family, size, App.IsFontInstalled);
            App.ApplyFont(choice);
            SoHocTap.Presentation.FontConfig.Save(choice);
            FitBar();   // chữ to hơn: thanh trên cùng đo lại
        });

    /// <summary>Cửa sổ Tải tài liệu của một lớp; tải xong thì trang Môn học đọc lại thư mục.</summary>
    private void Download(SoHocTap.Data.LmsCourse course)
    {
        if (_host.Hub.Get(SourceIds.Lms) is not SoHocTap.Sources.Lms.LmsSource lms) return;
        var w = new DownloadWindow(lms, course);
        w.Closed += (_, _) => Show(_current);
        w.ShowDialog(this);
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
