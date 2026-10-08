using System.Windows;
using System.Windows.Controls;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace SoHocTap.Ui.SettingsCards;

public partial class StartupSection : UserControl, ISettingsSection
{
    public StartupSection()
    {
        InitializeComponent();
        // Startup.Enabled ở bản Store chờ StartupTask (chặn luồng UI): chỉ đọc lúc dựng thẻ và sau khi người dùng đổi, không đọc mỗi lần Refresh.
        AutoStart.IsChecked = Startup.Enabled;
        Load();
    }

    public string TitleKey => "settings.startup";

    public void Load()
    {
        var toTray = Settings.App.CloseToTray;
        CloseToTray.IsChecked = toTray;
        CloseExit.IsChecked = !toTray;
    }

    // Bản Store: user có thể đã tắt trong Task Manager, nên hiện lại trạng thái thật sau khi đổi.
    private void OnAutoStart(object sender, RoutedEventArgs e) => AutoStart.IsChecked = Startup.Set(AutoStart.IsChecked == true);

    private void OnCloseMode(object sender, RoutedEventArgs e) => SettingsUi.Save(() => Settings.App.CloseToTray = CloseToTray.IsChecked == true, Error);
}
