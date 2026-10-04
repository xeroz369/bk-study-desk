using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using SoHocTap.Core;
using SoHocTap.Files;
using SoHocTap.Shell;
using SoHocTap.Updates;

namespace SoHocTap.Ui.Pages;

internal sealed record KeyRow(string Keys, string What);
internal sealed record LanguageItem(string Code, string Name)
{
    public override string ToString() => Name;   // tên cho screen reader đọc
}

public partial class SettingsPage : UserControl, IPage
{
    private readonly AppHost _host;
    private bool _languageReady;

    internal SettingsPage(AppHost host)
    {
        InitializeComponent();
        _host = host;
        Keys.Columns.Add(Grids.Flex(L.T("col.key"), nameof(KeyRow.Keys), 1, 160));
        Keys.Columns.Add(Grids.Text(L.T("col.what"), nameof(KeyRow.What), star: true));
        Grids.NameRows(Keys);
        // Tên phím viết bằng chữ (Ctrl+1 đến Ctrl+7, Alt+Mũi tên trái), không dùng gạch nối dài hay ký hiệu mũi tên.
        Keys.ItemsSource = new List<KeyRow>
        {
            new(L.F("settings.keys.range", "Ctrl+1", $"Ctrl+{MainWindow.PageKeys}"), L.T("settings.keys.pages")),
            new("F5", L.T("settings.keys.sync")),
            new(L.T("settings.keys.backKey"), L.T("settings.keys.back")),
            new(L.T("settings.keys.rowsKey"), L.T("settings.keys.rows")),
            new(L.T("settings.keys.enter"), L.T("settings.keys.open")),
            new(L.T("settings.keys.menu"), L.T("settings.keys.menuWhat")),
            new(L.T("settings.keys.click"), L.T("settings.keys.sort")),
            new("Backspace", L.T("settings.keys.up")),
        };
        Load();
        _host.Library.Changed += () => Dispatcher.InvokeAsync(ShowLibraryStatus);
        var langs = L.Available().Select(x => new LanguageItem(x.Code, x.Name)).ToList();
        LanguageBox.ItemsSource = langs;
        LanguageBox.SelectedItem = langs.FirstOrDefault(x => x.Code == Config.Str("app.language", L.Base)) ?? langs.FirstOrDefault(x => x.Code == L.Code);
        _languageReady = true;
        ShowRestart();
        LoadFont();
        LoadUpdates();
    }

    // ------------------------------------------------------------------ phông, cỡ chữ

    private sealed record SizeItem(double Size, string Name)
    {
        public override string ToString() => Name;
    }

    private bool _fontReady;

    /// <summary>Dòng đầu là Mặc định (Source rỗng = theo theme, tức phông của Windows), sau đó mọi phông trên máy.</summary>
    private void LoadFont()
    {
        var families = new List<FontItem> { new("", L.F("settings.font.default", AppFont.SystemFamily)) };
        families.AddRange(AppFont.Installed());
        FontFamilyBox.ItemsSource = families;
        var sizes = new List<SizeItem> { new(0, L.F("settings.font.sizeDefault", Math.Round(AppFont.SystemSize, 1))) };
        sizes.AddRange(FontChoice.Sizes.Select(s => new SizeItem(s, L.F("settings.font.sizeN", s))));
        FontSizeBox.ItemsSource = sizes;
        ShowFont();
        _fontReady = true;
    }

    private void ShowFont()
    {
        var cur = AppFont.Current;
        var families = (List<FontItem>)FontFamilyBox.ItemsSource;
        FontFamilyBox.SelectedItem = families.FirstOrDefault(f => string.Equals(f.Source, cur.Family, StringComparison.OrdinalIgnoreCase)) ?? families[0];
        var sizes = (List<SizeItem>)FontSizeBox.ItemsSource;
        FontSizeBox.SelectedItem = sizes.FirstOrDefault(s => s.Size == cur.Size) ?? sizes[0];
    }

    private void OnFontFamily(object sender, SelectionChangedEventArgs e)
    {
        if (_fontReady && FontFamilyBox.SelectedItem is FontItem f) SetFont(AppFont.Current with { Family = f.Source });
    }

    private void OnFontSize(object sender, SelectionChangedEventArgs e)
    {
        if (_fontReady && FontSizeBox.SelectedItem is SizeItem s) SetFont(AppFont.Current with { Size = s.Size });
    }

    private void OnFontReset(object sender, RoutedEventArgs e)
    {
        SetFont(FontChoice.Default);
        _fontReady = false;
        try { ShowFont(); }
        finally { _fontReady = true; }
    }

    /// <summary>Áp ngay cho cả app và lưu app.font. Ghi config lỗi thì chữ vẫn đổi trong lần chạy này, báo ngay dưới thẻ.</summary>
    private void SetFont(FontChoice choice)
    {
        FontStatus.Text = "";
        try { AppFont.Set(choice); }
        catch (IOException x) { FontStatus.Text = L.F("settings.saveError", x.Message); }
    }

    // ------------------------------------------------------------------ cập nhật

    private sealed record ModeItem(UpdateMode Mode, string Name)
    {
        public override string ToString() => Name;
    }

    private bool _updateReady;

    private void LoadUpdates()
    {
        if (!UpdateService.Supported) return;
        UpdateCard.Visibility = Visibility.Visible;
        var items = new List<ModeItem>
        {
            new(UpdateMode.Notify, L.T("update.mode.notify")),
            new(UpdateMode.Off, L.T("update.mode.off")),
        };
        if (UpdateService.CanSelfUpdate) items.Insert(1, new(UpdateMode.Auto, L.T("update.mode.auto")));
        ImportZip.Visibility = Paths.Kind == InstallKind.Installed ? Visibility.Visible : Visibility.Collapsed;
        UpdateModeBox.ItemsSource = items;
        UpdateModeBox.SelectedItem = items.FirstOrDefault(x => x.Mode == UpdateService.Mode);
        var hours = UpdatePolicy.CheckHourChoices.Select(h => new HoursItem(h, L.F("update.intervalHours", h))).ToList();
        UpdateIntervalBox.ItemsSource = hours;
        UpdateIntervalBox.SelectedItem = hours.First(x => x.Hours == UpdatePolicy.NearestChoice(Config.Int("app.update.checkHours", 6)));
        ShowIntervalRow();
        _updateReady = true;
        _updateView = new DownloadView(_host.Updates, UpdateBar);
        // Chỉ nghe khi trang đang hiện (trang Cài đặt được giữ lại sau khi rời đi).
        Loaded += (_, _) => { _host.Updates.Changed += OnUpdatesChanged; ShowUpdateNote(); };
        Unloaded += (_, _) => { _host.Updates.Changed -= OnUpdatesChanged; _updateView.Stop(); };
        ShowUpdateNote();
    }

    private DownloadView? _updateView;

    private void OnUpdatesChanged() => Dispatcher.InvokeAsync(ShowUpdateNote);

    private void OnUpdateCancel(object sender, RoutedEventArgs e) => _host.Updates.CancelDownload();

    private sealed record HoursItem(int Hours, string Name)
    {
        public override string ToString() => Name;
    }

    /// <summary>Chu kỳ chỉ có nghĩa khi app được phép kiểm tra (Báo, Tự động).</summary>
    private void ShowIntervalRow() =>
        UpdateIntervalRow.Visibility = UpdateService.Mode is UpdateMode.Notify or UpdateMode.Auto ? Visibility.Visible : Visibility.Collapsed;

    private void OnUpdateInterval(object sender, SelectionChangedEventArgs e)
    {
        if (_updateReady && UpdateIntervalBox.SelectedItem is HoursItem h) Config.Set("app.update.checkHours", h.Hours);
    }

    private void ShowUpdateNote()
    {
        var u = _host.Updates;
        var progress = _updateView?.Render();
        UpdateProgressRow.Visibility = u.Downloading ? Visibility.Visible : Visibility.Collapsed;
        UpdateNote.Text = progress ?? (u.LastError is { } err ? L.F("update.error", err)
            : u.Downloaded && u.Offer is not null ? L.T("update.restart")
            : u.Offer is { } o ? L.F("update.available", o.Version)
            : u.LastCheck is { } t ? L.F("update.latest", AppInfo.Version) + ", " + L.F("update.lastCheck", t.ToLocalTime().ToString("g", L.Culture))
            : !UpdateService.CanSelfUpdate ? L.T("update.zipNote") : "");
    }

    /// <summary>
    /// Chép data\ của bản zip cũ sang bản cài (DataImport.Plan chọn file), rồi khởi động lại để app đọc dữ liệu mới.
    /// Người dùng chọn thư mục bản zip (thư mục chứa data\ hoặc chính data\, hay app\ bên trong).
    /// </summary>
    private void OnImportZip(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this)!;
        var d = new Microsoft.Win32.OpenFolderDialog { Title = L.T("update.importPick") };
        if (d.ShowDialog(owner) != true) return;
        var picked = d.FolderName;
        var src = new[] { Path.Combine(picked, "data"), picked, Path.Combine(Path.GetDirectoryName(picked) ?? picked, "data") }
            .FirstOrDefault(x => File.Exists(Path.Combine(x, "config.json")) && !string.Equals(Path.GetFullPath(x), Path.GetFullPath(Paths.Data), StringComparison.OrdinalIgnoreCase));
        if (src is null) { MessageBox.Show(owner, L.T("update.importNone"), AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (MessageBox.Show(owner, L.F("update.importConfirm", src), AppInfo.Name, MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) != MessageBoxResult.OK)
            return;
        try
        {
            var files = DataImport.Plan(Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(src, f)));
            foreach (var rel in files)
            {
                var to = Path.Combine(Paths.Data, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(Path.Combine(src, rel), to, overwrite: true);
            }
            Log.Info($"Đã chép {files.Count} file dữ liệu từ bản zip");
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(owner, L.F("update.importFailed", x.Message), AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        OnRestart(sender, e);
    }

    private void OnUpdateMode(object sender, SelectionChangedEventArgs e)
    {
        if (_updateReady && UpdateModeBox.SelectedItem is ModeItem m) UpdateService.SetMode(m.Mode);
        if (_updateReady) ShowIntervalRow();
    }

    private async void OnUpdateCheck(object sender, RoutedEventArgs e)
    {
        UpdateCheck.IsEnabled = false;
        UpdateNote.Text = L.T("update.checking");
        UpdateOffer? offer;
        try { offer = await _host.Updates.CheckAsync(manual: true, default); }
        finally { UpdateCheck.IsEnabled = true; }
        ShowUpdateNote();
        if (offer is not null) new UpdateWindow(_host.Updates, offer) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    public string Title => L.T("nav.settings");
    public string Subtitle => L.T("settings.subtitle");

    private void Load()
    {
        PdfApp.Text = Config.Str("viewer.pdfApp");
        Root.Text = Config.Str("folders.root");
        RootNote.Text = L.F("settings.rootNote", Paths.StudyRoot);
        AutoStart.IsChecked = Startup.Enabled;
        var toTray = Config.Bool("app.closeToTray", true);
        CloseToTray.IsChecked = toTray;
        CloseExit.IsChecked = !toTray;
        LmsHours.Text = Config.Int("sources.lms.syncHours", 3).ToString(CultureInfo.InvariantCulture);
        MybkHours.Text = Config.Int("sources.mybk.syncHours", 12).ToString(CultureInfo.InvariantCulture);
        NotifyHours.Text = Config.Int("notify.hoursBefore", 24).ToString(CultureInfo.InvariantCulture);
        LastHours.Text = Config.Int("notify.lastHours", 2).ToString(CultureInfo.InvariantCulture);
        MaxMb.Text = Config.Int("sources.lms.maxFileMB", 200).ToString(CultureInfo.InvariantCulture);
        AutoDownload.IsChecked = Config.Bool("sources.lms.autoDownload", false);
        AutoExtract.IsChecked = Config.Bool("archives.extract", true);
        SaveQuizzes.IsChecked = Config.Bool("sources.lms.saveQuizzes", true);
        Remember.Content = L.T("settings.remember");
        Remember.IsChecked = Config.Int("sso.rememberDays", RememberDays) > 0;
        KeepAlive.IsChecked = Config.Int("sso.keepAliveMinutes", KeepAliveMinutes) > 0;
        DebugLog.IsChecked = DiagnosticLog.Active();
        LibraryEnabled.IsChecked = Library.LibraryService.Enabled;
        LibraryUrl.Text = Library.LibraryService.BaseUrl;
        // Địa chỉ thư viện chỉ đổi khi đang bật log chẩn đoán (dev, thử thư viện trên máy); bình thường là chữ chỉ đọc.
        LibraryUrl.IsReadOnly = !DiagnosticLog.Active();
        ShowLibraryStatus();
    }

    // ------------------------------------------------------------------ thư viện

    private void ShowLibraryStatus()
    {
        var lib = _host.Library;
        LibraryStatus.Text = Library.LibraryService.BaseUrl.Length == 0 ? L.T("settings.library.noUrl")
            : lib.LastChecked is { } t ? L.F("settings.library.lastCheck", t.ToLocalTime().ToString("g", L.Culture))
            : L.T("settings.library.never");
    }

    private void OnLibraryEnabled(object sender, RoutedEventArgs e)
    {
        _host.Library.SetEnabled(LibraryEnabled.IsChecked == true);
        ShowLibraryStatus();
    }

    private void OnLibraryUrl(object sender, RoutedEventArgs e)
    {
        if (LibraryUrl.IsReadOnly || LibraryUrl.Text.Trim() == Library.LibraryService.BaseUrl) return;
        _host.Library.SetBaseUrl(LibraryUrl.Text);
        ShowLibraryStatus();
    }

    private void OnLibraryUrlKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) OnLibraryUrl(sender, e);
    }

    private void OnLibraryClear(object sender, RoutedEventArgs e)
    {
        try
        {
            _host.Library.ClearCache();
            // Sau handler Changed (cũng xếp hàng trên Dispatcher), để câu "đã xóa" không bị ghi đè.
            Dispatcher.InvokeAsync(() => LibraryStatus.Text = L.T("settings.library.cleared"));
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException) { LibraryStatus.Text = L.F("settings.saveError", x.Message); }
    }

    private const int KeepAliveMinutes = 60;

    /// <summary>Giữ phiên SSO khi app mở: lưu ngay sso.keepAliveMinutes (60 hoặc 0 = tắt).</summary>
    private void OnKeepAlive(object sender, RoutedEventArgs e) => Config.Set("sso.keepAliveMinutes", KeepAlive.IsChecked == true ? KeepAliveMinutes : 0);

    /// <summary>Log chẩn đoán: có hiệu lực ngay, tự tắt sau DiagnosticLog.Days ngày.</summary>
    private void OnDebugLog(object sender, RoutedEventArgs e)
    {
        DiagnosticLog.Set(DebugLog.IsChecked == true);
        LibraryUrl.IsReadOnly = !DiagnosticLog.Active();
    }

    /// <summary>Mở Explorer, chọn sẵn app.log để người dùng kéo vào issue/tin nhắn báo lỗi.</summary>
    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(Log.LogFile)) Log.Info("Mở file log");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), $"/select,\"{Log.LogFile}\"")
        { UseShellExecute = false });
    }

    private const int RememberDays = 30;

    /// <summary>Ghi nhớ đăng nhập: lưu ngay sso.rememberDays (30 hoặc 0). Nếu tắt thì từ lần login sau, tắt app là mất session.</summary>
    private void OnRemember(object sender, RoutedEventArgs e)
    {
        var c = (JsonObject)Config.Current.DeepClone();
        (c["sso"] as JsonObject ?? (JsonObject)(c["sso"] = new JsonObject()))["rememberDays"] = Remember.IsChecked == true ? RememberDays : 0;
        try { Config.Save(c); }
        catch (IOException x) { SaveText.Text = L.F("settings.saveError", x.Message); }
    }

    /// <summary>Tự lưu quiz LMS: lưu ngay (cùng giá trị với công tắc ở Kho quiz), không chờ nút Lưu.</summary>
    private void OnSaveQuizzes(object sender, RoutedEventArgs e) => Config.Set("sources.lms.saveQuizzes", SaveQuizzes.IsChecked == true);

    public void Refresh()
    {
        SaveQuizzes.IsChecked = Config.Bool("sources.lms.saveQuizzes", true);   // có thể vừa đổi ở Kho quiz
        var s = _host.State;
        var need = s.Account;
        AccountText.Text = need == AccountNeed.None ? (s.Lms?.User is { } u ? L.F("settings.signedInAs", u) : L.T("settings.signedIn")) : L.T("settings.needLogin");
        AccountDetail.Text = L.F("settings.accountDetail",
            L.T(need is AccountNeed.Lms or AccountNeed.Both ? "settings.lms.need" : "settings.lms.ok"),
            L.T(need is AccountNeed.Mybk or AccountNeed.Both ? "settings.mybk.expired" : s.Mybk is null ? "settings.mybk.never" : "settings.mybk.synced"));
        LoginButton.Content = L.T(need == AccountNeed.None ? "settings.relogin" : "common.loginHcmut");
        LogoutButton.IsEnabled = true;   // luôn cho logout vì có thể vẫn còn session SSO/MyBK dù chưa có dữ liệu LMS
    }

    private void OnLogin(object sender, RoutedEventArgs e) => _host.Login();

    // ------------------------------------------------------------------ ngôn ngữ

    /// <summary>Chọn ngôn ngữ: lưu ngay vào app.language, UI đổi sau khi restart app.</summary>
    private void OnLanguage(object sender, SelectionChangedEventArgs e)
    {
        if (!_languageReady || LanguageBox.SelectedItem is not LanguageItem item) return;
        var c = (JsonObject)Config.Current.DeepClone();
        var app = c["app"] as JsonObject ?? (JsonObject)(c["app"] = new JsonObject());
        app["language"] = item.Code;
        try { Config.Save(c); }
        catch (IOException x) { SaveText.Text = L.F("settings.saveError", x.Message); }
        ShowRestart();
    }

    private void ShowRestart()
    {
        var pending = Config.Str("app.language", L.Base) != L.Code;
        LanguageNote.Visibility = RestartButton.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Launch bản mới với --restart (bản mới đợi bản này nhả mutex single-instance, xem Program.cs) rồi thoát.</summary>
    private void OnRestart(object sender, RoutedEventArgs e)
    {
        if (Environment.ProcessPath is not { } exe) return;
        try { Process.Start(new ProcessStartInfo(exe, "--restart") { UseShellExecute = false }); }
        catch (System.ComponentModel.Win32Exception x)
        {
            Log.Error("Restart app lỗi", x);
            return;
        }
        Application.Current.Shutdown();
    }

    private void OnLogout(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this)!, L.T("settings.logoutConfirm"), AppInfo.Name,
                MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK)
            _host.Logout();
    }

    private void OnBrowseRoot(object sender, RoutedEventArgs e)
    {
        var d = new Microsoft.Win32.OpenFolderDialog { Title = L.T("settings.rootPick"), InitialDirectory = Paths.StudyRoot };
        if (d.ShowDialog(Window.GetWindow(this)) == true) Root.Text = d.FolderName;
    }

    // Bản Store: user có thể đã tắt trong Task Manager, nên hiện lại trạng thái thật sau khi đổi.
    private void OnAutoStart(object sender, RoutedEventArgs e) => AutoStart.IsChecked = Startup.Set(AutoStart.IsChecked == true);

    private void OnCloseMode(object sender, RoutedEventArgs e)
    {
        var c = (JsonObject)Config.Current.DeepClone();
        (c["app"] as JsonObject ?? (JsonObject)(c["app"] = new JsonObject()))["closeToTray"] = CloseToTray.IsChecked == true;
        Config.Save(c);
    }

    private void OnBrowsePdf(object sender, RoutedEventArgs e)
    {
        var d = new Microsoft.Win32.OpenFileDialog { Filter = L.T("settings.exeFilter"), Title = L.T("settings.pickPdf") };
        if (d.ShowDialog(Window.GetWindow(this)) == true) PdfApp.Text = d.FileName;
    }

    /// <summary>MyBK đồng bộ cách nhau ít nhất 6 giờ: dữ liệu MyBK (lịch, điểm) đổi chậm, đọc dày hơn chỉ tốn request của trường.</summary>
    private const int MybkMinHours = 6;

    private void OnSave(object sender, RoutedEventArgs e)
    {
        static int Pos(string s) => int.TryParse(s, out var v) && v > 0 ? v : 1;
        // 0 = tắt lần nhắc đó (DeadlineNotifier bỏ các mốc <= 0); số âm hay chữ thì coi như 0.
        static int NonNeg(string s) => int.TryParse(s, out var v) && v > 0 ? v : 0;
        var c = (JsonObject)Config.Current.DeepClone();
        void Set(string dotted, JsonNode value)
        {
            var parts = dotted.Split('.');
            var o = c;
            foreach (var p in parts[..^1]) o = o[p] as JsonObject ?? (JsonObject)(o[p] = new JsonObject());
            o[parts[^1]] = value;
        }
        Set("viewer.pdfApp", PdfApp.Text.Trim().Trim('"'));
        Set("folders.root", Root.Text.Trim().Trim('"'));
        Set("sources.lms.syncHours", Pos(LmsHours.Text));
        Set("sources.mybk.syncHours", Math.Max(MybkMinHours, Pos(MybkHours.Text)));
        Set("notify.hoursBefore", NonNeg(NotifyHours.Text));
        Set("notify.lastHours", NonNeg(LastHours.Text));
        Set("sources.lms.maxFileMB", Pos(MaxMb.Text));
        Set("sources.lms.autoDownload", AutoDownload.IsChecked == true);
        Set("archives.extract", AutoExtract.IsChecked == true);
        try
        {
            Config.Save(c);
            Load();
            SaveText.Text = L.T("settings.saved");
        }
        catch (IOException x) { SaveText.Text = L.F("settings.saveError", x.Message); }
    }

    private void OnResync(object sender, RoutedEventArgs e)
    {
        _host.Hub.Start("lms", force: true);
        SaveText.Text = L.T("settings.resyncing");
    }

    private void OnPlan(object sender, RoutedEventArgs e)
    {
        var lines = Organizer.ImportDownloads(apply: false);
        PlanText.Visibility = Visibility.Visible;
        PlanText.Text = lines.Count == 0 ? L.T("settings.planEmpty") : string.Join(Environment.NewLine, lines);
        ApplyButton.IsEnabled = lines.Count > 0;
        ApplyButton.Content = lines.Count > 0 ? L.F("settings.applyN", lines.Count) : L.T("settings.apply");
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        var lines = Organizer.ImportDownloads(apply: true);
        PlanText.Text = L.F("settings.applied", lines.Count);
        ApplyButton.IsEnabled = false;
    }
}
