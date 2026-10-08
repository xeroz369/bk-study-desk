using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SoHocTap.Core;

namespace SoHocTap.Ui.SettingsCards;

/// <summary>Log chẩn đoán đi qua DiagnosticLog (bật kèm hạn tự tắt app.debugLogUntil), không phải một khóa đơn trong Settings.</summary>
public partial class DiagnosticsSection : UserControl, ISettingsSection
{
    public DiagnosticsSection()
    {
        InitializeComponent();
        Load();
    }

    public string TitleKey => "settings.debug";

    public void Load() => DebugLog.IsChecked = DiagnosticLog.Active();

    /// <summary>Log chẩn đoán: có hiệu lực ngay, tự tắt sau DiagnosticLog.Days ngày.</summary>
    private void OnDebugLog(object sender, RoutedEventArgs e) => SettingsUi.Save(() => DiagnosticLog.Set(DebugLog.IsChecked == true), Error);

    /// <summary>Mở Explorer, chọn sẵn app.log để người dùng kéo vào issue/tin nhắn báo lỗi.</summary>
    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(Log.LogFile)) Log.Info("Mở file log");
        Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), $"/select,\"{Log.LogFile}\"")
        { UseShellExecute = false });
    }
}
