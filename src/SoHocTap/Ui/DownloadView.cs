using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SoHocTap.Updates;

namespace SoHocTap.Ui;

/// <summary>
/// Vẽ tiến độ tải bản cập nhật (<see cref="UpdateService.Download"/>) lên một ProgressBar Fluent có sẵn, theo DESIGN.md "Thanh tiến trình":
/// số thật của Velopack, vô định chỉ trước callback đầu tiên, không lùi, 100% chỉ khi đã tải xong và kiểm SHA; hiện sau 1 giây, giữ ít
/// nhất 800 ms (<see cref="ProgressGate"/>, tắt mục "đứng yên thì vô định"). Hủy hay lỗi thì ẩn ngay, không đầy thanh.
/// Dùng ở cửa sổ cập nhật, thẻ Cập nhật trong Cài đặt và thanh trạng thái. Gọi <see cref="Render"/> mỗi lần UpdateService.Changed.
/// </summary>
internal sealed class DownloadView
{
    private readonly UpdateService _service;
    private readonly ProgressBar _bar;
    private readonly ProgressGate _gate = new(stallAfter: null);
    private readonly DispatcherTimer _timer;

    public DownloadView(UpdateService service, ProgressBar bar)
    {
        _service = service;
        _bar = bar;
        System.Windows.Automation.AutomationProperties.SetName(bar, L.T("update.progress"));
        _timer = new DispatcherTimer(DispatcherPriority.Normal, bar.Dispatcher);
        // Hẹn vẽ lại khi không có sự kiện mới: tới lúc hiện (1 giây), hết lúc giữ (800 ms).
        _timer.Tick += (_, _) => { _timer.Stop(); Render(); };
    }

    /// <summary>Cập nhật thanh; trả chữ tiến độ ("Đang tải bản 1.2.0... 42% (12,3/29,1 MB)") khi đang tải, null khi không.</summary>
    public string? Render()
    {
        var s = _service.Download;
        BarState b;
        if (s is null)
        {
            _gate.Reset();
            b = new(false, false, 0, null);
        }
        else b = _gate.Update(busy: !s.Verified, s.Percent / 100.0, DateTime.UtcNow);

        _bar.IsIndeterminate = b.Indeterminate;
        if (!b.Indeterminate) _bar.Value = b.Value;
        // Đang tải mà chưa tới lúc hiện: giữ chỗ (Hidden) để nút, chữ bên cạnh không nhảy khi thanh hiện ra.
        _bar.Visibility = b.Visible ? Visibility.Visible : _service.Downloading ? Visibility.Hidden : Visibility.Collapsed;
        _timer.Stop();
        if (b.Recheck is { } again)
        {
            _timer.Interval = again < TimeSpan.FromMilliseconds(20) ? TimeSpan.FromMilliseconds(20) : again;
            _timer.Start();
        }
        var text = s is { Verified: false } ? Text(s) : null;
        System.Windows.Automation.AutomationProperties.SetHelpText(_bar, text ?? "");
        return text;
    }

    public void Stop() => _timer.Stop();

    public static string Text(DownloadState s) =>
        DownloadText.Describe(s, L.T("update.downloadStarting"), L.T("update.downloadingSize"), L.T("update.downloading"), L.Culture);
}
