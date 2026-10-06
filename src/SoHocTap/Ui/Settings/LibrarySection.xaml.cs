using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SoHocTap.Core;
using SoHocTap.Library;
using SoHocTap.Shell;

namespace SoHocTap.Ui.SettingsCards;

/// <summary>Đổi địa chỉ đi qua LibraryService (dọn cache, báo Changed), không ghi config trực tiếp.</summary>
public partial class LibrarySection : UserControl, ISettingsSection
{
    private readonly AppHost _host;

    internal LibrarySection(AppHost host)
    {
        InitializeComponent();
        _host = host;
        _host.Library.Changed += () => Dispatcher.InvokeAsync(ShowStatus);
        Load();
    }

    public string TitleKey => "settings.library";

    public void Load()
    {
        SettingsUi.ShowText(LibraryUrl, LibraryService.BaseUrl);
        // Địa chỉ thư viện chỉ đổi khi đang bật log chẩn đoán (dev, thử thư viện trên máy); bình thường là chữ chỉ đọc.
        LibraryUrl.IsReadOnly = !DiagnosticLog.Active();
        ShowStatus();
    }

    public void Flush() => OnLibraryUrl(this, new RoutedEventArgs());

    private void ShowStatus()
    {
        var lib = _host.Library;
        LibraryStatus.Text = LibraryService.BaseUrl.Length == 0 ? L.T("settings.library.noUrl")
            : lib.LastChecked is { } t ? L.F("settings.library.lastCheck", t.ToLocalTime().ToString("g", L.Culture))
            : L.T("settings.library.never");
    }

    /// <summary>Thẻ Nhật ký lỗi có thể vừa bật log chẩn đoán: xét lại quyền sửa địa chỉ mỗi lần vào ô.</summary>
    private void OnLibraryUrlFocus(object sender, KeyboardFocusChangedEventArgs e) => LibraryUrl.IsReadOnly = !DiagnosticLog.Active();

    private void OnLibraryUrl(object sender, RoutedEventArgs e)
    {
        if (LibraryUrl.IsReadOnly || LibraryUrl.Text.Trim() == LibraryService.BaseUrl) return;
        _host.Library.SetBaseUrl(LibraryUrl.Text);
        ShowStatus();
    }

    private void OnLibraryUrlLeave(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!SettingsUi.ToMenu(e)) OnLibraryUrl(sender, e);
    }

    private void OnLibraryUrlKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnLibraryUrl(sender, e);
    }

    /// <summary>Mở web thư viện theo địa chỉ đang đặt (chỉ https).</summary>
    private void OnLibraryOpen(object sender, RoutedEventArgs e)
    {
        if (Uri.TryCreate(Settings.Library.BaseUrl, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps) Links.Open(u.AbsoluteUri);
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
}
