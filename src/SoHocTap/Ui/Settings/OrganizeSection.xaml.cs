using System.Windows;
using System.Windows.Controls;
using SoHocTap.Files;

namespace SoHocTap.Ui.SettingsCards;

/// <summary>Thẻ này không có cấu hình để lưu: chỉ có hai lệnh Xem trước và Chuyển.</summary>
public partial class OrganizeSection : UserControl, ISettingsSection
{
    public OrganizeSection() => InitializeComponent();

    public string TitleKey => "settings.organize";

    public void Load() { }

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
