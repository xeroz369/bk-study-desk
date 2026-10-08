using System.Windows;
using System.Windows.Controls;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Sources;

namespace SoHocTap.Ui.SettingsCards;

public partial class SyncSection : UserControl, ISettingsSection
{
    /// <summary>MyBK đồng bộ cách nhau ít nhất 6 giờ: dữ liệu MyBK (lịch, điểm) đổi chậm, đọc dày hơn chỉ tốn request của trường.</summary>
    private const int MybkMinHours = 6;

    private readonly AppHost _host;
    private readonly NumberSetting[] _numbers;

    internal SyncSection(AppHost host)
    {
        InitializeComponent();
        _host = host;
        _numbers =
        [
            new(LmsHours, LmsHoursError, 1, () => Settings.Sync.LmsHours, v => Settings.Sync.LmsHours = v),
            new(MybkHours, MybkHoursError, MybkMinHours, () => Settings.Sync.MybkHours, v => Settings.Sync.MybkHours = v),
            new(MaxMb, MaxMbError, 1, () => Settings.Sync.MaxFileMB, v => Settings.Sync.MaxFileMB = v),
        ];
        Load();
    }

    public string TitleKey => "settings.sync";

    public void Load()
    {
        foreach (var n in _numbers) n.Load();
        AutoDownload.IsChecked = Settings.Sync.AutoDownload;
        AutoExtract.IsChecked = Settings.Archives.Extract;
        SaveQuizzes.IsChecked = Settings.Sync.SaveQuizzes;   // có thể vừa đổi ở Kho quiz
    }

    public void Flush()
    {
        foreach (var n in _numbers) n.Flush();
    }

    private void OnAutoDownload(object sender, RoutedEventArgs e) => SettingsUi.Save(() => Settings.Sync.AutoDownload = AutoDownload.IsChecked == true, Error);

    private void OnAutoExtract(object sender, RoutedEventArgs e) => SettingsUi.Save(() => Settings.Archives.Extract = AutoExtract.IsChecked == true, Error);

    /// <summary>Tự lưu quiz LMS: cùng giá trị với công tắc ở Kho quiz.</summary>
    private void OnSaveQuizzes(object sender, RoutedEventArgs e) => SettingsUi.Save(() => Settings.Sync.SaveQuizzes = SaveQuizzes.IsChecked == true, Error);

    private void OnResync(object sender, RoutedEventArgs e)
    {
        _host.Hub.Start(SourceIds.Lms, force: true);
        ResyncText.Text = L.T("settings.resyncing");
    }
}
