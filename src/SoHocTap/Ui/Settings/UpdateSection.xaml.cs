using System.Windows;
using System.Windows.Controls;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Updates;

namespace SoHocTap.Ui.SettingsCards;

/// <summary>Thẻ Cập nhật, chỉ hiện ở bản public (UpdateService.Supported).</summary>
public partial class UpdateSection : UserControl, ISettingsSection
{
    private sealed record ModeItem(UpdateMode Mode, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record HoursItem(int Hours, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly AppHost _host;
    private readonly DownloadView? _view;
    private bool _ready;

    internal UpdateSection(AppHost host)
    {
        InitializeComponent();
        _host = host;
        if (!UpdateService.Supported)
        {
            Visibility = Visibility.Collapsed;
            return;
        }
        var modes = new List<ModeItem> { new(UpdateMode.Notify, L.T("update.mode.notify")), new(UpdateMode.Off, L.T("update.mode.off")) };
        if (UpdateService.CanSelfUpdate) modes.Insert(1, new(UpdateMode.Auto, L.T("update.mode.auto")));
        ImportZip.Visibility = Paths.Kind == InstallKind.Installed ? Visibility.Visible : Visibility.Collapsed;
        UpdateModeBox.ItemsSource = modes;
        UpdateIntervalBox.ItemsSource = UpdatePolicy.CheckHourChoices.Select(h => new HoursItem(h, L.F("update.intervalHours", h))).ToList();
        var view = _view = new DownloadView(_host.Updates, UpdateBar);
        // Chỉ nghe khi trang đang hiện (trang Cài đặt được giữ lại sau khi rời đi).
        Loaded += (_, _) => { _host.Updates.Changed += OnUpdatesChanged; ShowNote(); };
        Unloaded += (_, _) => { _host.Updates.Changed -= OnUpdatesChanged; view.Stop(); };
        Load();
    }

    public string TitleKey => "update.settings";

    public void Load()
    {
        if (!UpdateService.Supported) return;
        _ready = false;
        try
        {
            UpdateModeBox.SelectedItem = ((List<ModeItem>)UpdateModeBox.ItemsSource).FirstOrDefault(x => x.Mode == UpdateService.Mode);
            var nearest = UpdatePolicy.NearestChoice(Settings.Update.CheckHours);
            UpdateIntervalBox.SelectedItem = ((List<HoursItem>)UpdateIntervalBox.ItemsSource).First(x => x.Hours == nearest);
        }
        finally { _ready = true; }
        ShowIntervalRow();
        ShowNote();
    }

    private void OnUpdatesChanged() => Dispatcher.InvokeAsync(ShowNote);

    private void OnUpdateCancel(object sender, RoutedEventArgs e) => _host.Updates.CancelDownload();

    /// <summary>Chu kỳ chỉ có nghĩa khi app được phép kiểm tra (Báo, Tự động).</summary>
    private void ShowIntervalRow() =>
        UpdateIntervalRow.Visibility = UpdateService.Mode is UpdateMode.Notify or UpdateMode.Auto ? Visibility.Visible : Visibility.Collapsed;

    private void OnUpdateInterval(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && UpdateIntervalBox.SelectedItem is HoursItem h) SettingsUi.Save(() => Settings.Update.CheckHours = h.Hours, Error);
    }

    private void OnUpdateMode(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (UpdateModeBox.SelectedItem is ModeItem m) UpdateService.SetMode(m.Mode);
        ShowIntervalRow();
    }

    private void ShowNote()
    {
        var u = _host.Updates;
        var progress = _view?.Render();
        UpdateProgressRow.Visibility = u.Downloading ? Visibility.Visible : Visibility.Collapsed;
        UpdateNote.Text = progress ?? (u.LastError is { } err ? L.F("update.error", err)
            : u.Downloaded && u.Offer is not null ? L.T("update.restart")
            : u.Offer is { } o ? L.F("update.available", o.Version)
            : u.LastCheck is { } t ? L.F("update.latest", AppInfo.Version) + ", " + L.F("update.lastCheck", t.ToLocalTime().ToString("g", L.Culture))
            : !UpdateService.CanSelfUpdate ? L.T("update.zipNote") : "");
    }

    private async void OnUpdateCheck(object sender, RoutedEventArgs e)
    {
        UpdateCheck.IsEnabled = false;
        UpdateNote.Text = L.T("update.checking");
        UpdateOffer? offer;
        try { offer = await _host.Updates.CheckAsync(manual: true, default); }
        finally { UpdateCheck.IsEnabled = true; }
        ShowNote();
        if (offer is not null) new UpdateWindow(_host.Updates, offer) { Owner = Window.GetWindow(this) }.ShowDialog();
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
        SettingsUi.Restart();
    }
}
