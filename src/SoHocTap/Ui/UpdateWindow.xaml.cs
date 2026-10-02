using System.Windows;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Updates;

namespace SoHocTap.Ui;

/// <summary>Hộp thoại cập nhật: chọn chế độ (lần đầu) hoặc báo có bản mới. Xem UpdateService.</summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateService? _service;
    private readonly UpdateOffer? _offer;
    // Đóng cửa sổ (X, Để sau) khi đang tải: không khởi động lại app; tải xong thì thanh trạng thái báo, người dùng tự bấm.
    private bool _closed;

    /// <summary>Lần mở đầu: hỏi chế độ. Đóng bằng X thì vẫn là "chưa chọn" (không gọi mạng), lần mở sau hỏi lại.</summary>
    public UpdateWindow()
    {
        InitializeComponent();
        Closing += (_, _) => _closed = true;
        // Bản zip không tự cài được: chỉ có "báo" hoặc "không kiểm tra".
        if (!UpdateService.CanSelfUpdate) ModeAuto.Visibility = Visibility.Collapsed;
    }

    /// <summary>Có bản mới: phiên bản, dung lượng, thay đổi; bản zip thì nút chính là "Mở trang tải".</summary>
    public UpdateWindow(UpdateService service, UpdateOffer offer) : this()
    {
        _service = service;
        _offer = offer;
        AskPanel.Visibility = Visibility.Collapsed;
        OfferPanel.Visibility = Visibility.Visible;
        Heading.Text = L.F("update.available", offer.Version);
        SizeText.Text = offer.Bytes > 0 ? L.F("update.size", Format.Size(offer.Bytes)) : "";
        Notes.Text = offer.Notes.Trim();
        if (offer.Native is null) InstallButton.Content = L.T("update.openPage");
        if (service.Downloaded) InstallButton.Content = L.T("update.restart");
    }

    private void OnSaveMode(object sender, RoutedEventArgs e)
    {
        UpdateService.SetMode(ModeAuto.IsChecked == true ? UpdateMode.Auto : ModeOff.IsChecked == true ? UpdateMode.Off : UpdateMode.Notify);
        Close();
    }

    private void OnLater(object sender, RoutedEventArgs e) => Close();

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        if (_offer is not null) UpdateService.Skip(_offer);
        Close();
    }

    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        if (_service is null || _offer is null) return;
        if (_offer.Native is null) { Links.Open(_offer.Url); Close(); return; }
        if (!_service.Downloaded)
        {
            InstallButton.IsEnabled = SkipButton.IsEnabled = false;
            Status.Text = L.F("update.downloading", 0);
            var ok = await _service.DownloadAsync(userAsked: true, p => Dispatcher.InvokeAsync(() => Status.Text = L.F("update.downloading", p)), default);
            if (_closed) return;
            if (!ok)
            {
                Status.Text = L.F("update.failed", _service.LastError ?? "");
                InstallButton.IsEnabled = SkipButton.IsEnabled = true;
                return;
            }
        }
        if (!_closed) _service.ApplyAndRestart();
    }
}
