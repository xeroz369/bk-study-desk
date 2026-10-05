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
    private readonly DownloadView? _view;
    // Đóng cửa sổ (X, Để sau) khi đang tải: vẫn tải tiếp ở nền (thanh trạng thái hiện tiến độ), không khởi động lại app;
    // tải xong thì thanh trạng thái báo, người dùng tự bấm. Muốn dừng tải thì bấm Hủy tải.
    private bool _closed;
    // Đã gọi Update.exe: chỉ còn câu "Đang cài bản...", không vẽ lại theo trạng thái tải nữa.
    private bool _applying;
    private bool _progressShown;

    /// <summary>Lần mở đầu: hỏi chế độ. Đóng bằng X thì vẫn là "chưa chọn" (không gọi mạng), lần mở sau hỏi lại.</summary>
    public UpdateWindow()
    {
        InitializeComponent();
        Shell.WindowPlacement.FitToWorkArea(this);
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
        _view = new DownloadView(service, Bar);
        // Mở cửa sổ khi chế độ Tự động đang tải ở nền: hiện luôn tiến độ của lượt tải đó.
        service.Changed += OnServiceChanged;
        Closed += (_, _) => { service.Changed -= OnServiceChanged; _view.Stop(); };
        Render();
    }

    private void OnServiceChanged() => Dispatcher.InvokeAsync(Render);

    /// <summary>Theo trạng thái tải của UpdateService: thanh, chữ %, nút Hủy tải; tải xong thì nút chính là "Khởi động lại để cập nhật".</summary>
    private void Render()
    {
        if (_service is null || _offer is null || _view is null || _applying || _closed) return;
        var text = _view.Render();
        var downloading = _service.Downloading;
        if (text is not null) Status.Text = text;
        else if (_progressShown) Status.Text = "";   // tải xong (hay hủy, lỗi: OnInstall ghi lý do ngay sau): bỏ câu "Đang tải..."
        _progressShown = text is not null;
        CancelButton.Visibility = downloading ? Visibility.Visible : Visibility.Collapsed;
        InstallButton.IsEnabled = SkipButton.IsEnabled = !downloading;
        if (_service.Downloaded && _offer.Native is not null) InstallButton.Content = L.T("update.restart");
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

    private void OnCancel(object sender, RoutedEventArgs e) => _service?.CancelDownload();

    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        if (_service is null || _offer is null) return;
        if (_offer.Native is null) { Links.Open(_offer.Url); Close(); return; }
        if (!_service.Downloaded)
        {
            var ok = await _service.DownloadAsync(userAsked: true, default);
            if (_closed) return;
            if (!ok)
            {
                // Hủy thì LastError null; lỗi mạng, gói đổi... thì có lý do. Render sau đó không được xóa câu này.
                _progressShown = false;
                Status.Text = _service.LastError is { } err ? L.F("update.failed", err) : L.T("update.canceled");
                Render();
                return;
            }
        }
        if (_closed) return;
        _applying = true;
        Status.Text = L.F("update.installing", _offer.Version);
        Bar.Visibility = CancelButton.Visibility = Visibility.Collapsed;
        InstallButton.IsEnabled = SkipButton.IsEnabled = LaterButton.IsEnabled = false;
        // Update.exe chạy silent; AppHost báo ở thanh trạng thái rồi đóng app (cả cửa sổ này) sau vài giây.
        if (_service.ApplyAndRestart()) return;
        _applying = false;
        Status.Text = L.F("update.applyError", _service.LastError ?? "");
        LaterButton.IsEnabled = true;
        Render();
    }
}
