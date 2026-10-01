using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using SoHocTap.Core;
using SoHocTap.Files;
using SoHocTap.Shell;

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
        Keys.Columns.Add(Grids.Text(L.T("col.key"), nameof(KeyRow.Keys), 220));
        Keys.Columns.Add(Grids.Text(L.T("col.what"), nameof(KeyRow.What), star: true));
        Keys.ItemsSource = new List<KeyRow>
        {
            new($"Ctrl+1 … Ctrl+{MainWindow.PageKeys}", L.T("settings.keys.pages")),
            new("F5", L.T("settings.keys.sync")),
            new("Alt+←", L.T("settings.keys.back")),
            new("↑ ↓ · Home · End", L.T("settings.keys.rows")),
            new(L.T("settings.keys.enter"), L.T("settings.keys.open")),
            new(L.T("settings.keys.menu"), L.T("settings.keys.menuWhat")),
            new(L.T("settings.keys.click"), L.T("settings.keys.sort")),
            new("Backspace", L.T("settings.keys.up")),
        };
        Load();
        var langs = L.Available().Select(x => new LanguageItem(x.Code, x.Name)).ToList();
        LanguageBox.ItemsSource = langs;
        LanguageBox.SelectedItem = langs.FirstOrDefault(x => x.Code == Config.Str("app.language", L.Base)) ?? langs.FirstOrDefault(x => x.Code == L.Code);
        _languageReady = true;
        ShowRestart();
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
        AutoDownload.IsChecked = Config.Bool("sources.lms.autoDownload", true);
        AutoExtract.IsChecked = Config.Bool("archives.extract", true);
        Remember.Content = L.F("settings.remember", RememberDays);
        Remember.IsChecked = Config.Int("sso.rememberDays", RememberDays) > 0;
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

    public void Refresh()
    {
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

    private void OnSave(object sender, RoutedEventArgs e)
    {
        static int Pos(string s) => int.TryParse(s, out var v) && v > 0 ? v : 1;
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
        Set("sources.mybk.syncHours", Pos(MybkHours.Text));
        Set("notify.hoursBefore", Pos(NotifyHours.Text));
        Set("notify.lastHours", Pos(LastHours.Text));
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
