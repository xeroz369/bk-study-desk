using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace BKStudyDesk.Desktop.Platform;

/// <summary>
/// Hiện một lời nhắc (DeadlineNotifier chọn mốc, chữ). Cửa sổ đang mở: thông báo trong app (WindowNotificationManager có sẵn của Avalonia),
/// bấm vào thì mở đúng trang. Cửa sổ đang ở khay: thông báo của hệ điều hành (Linux notify-send, macOS osascript).
/// Windows: toast (Platform/Windows/Toast.cs, chỉ có ở bản net10.0-windows). Không gửi được thì giữ lại, hiện khi mở cửa sổ.
/// </summary>
internal sealed class Notifier(Window main, Action<string> open)
{
    private WindowNotificationManager? _inApp;
    private readonly List<(string Title, string Body, string Page)> _pending = [];

    public void Show(string title, string body, string page)
    {
        if (main.IsVisible) { InApp(title, body, page); return; }
        if (OperatingSystem.IsLinux() && Run("notify-send", "-a", AppInfo.Name, title, body)) return;
        if (OperatingSystem.IsMacOS() && Run("osascript", "-e", $"display notification {Quote(body)} with title {Quote(title)}")) return;
#if WINDOWS
        if (Windows.Toast.Show(title, body, () => Avalonia.Threading.Dispatcher.UIThread.Post(() => open(page)))) return;
#endif
        _pending.Add((title, body, page));
    }

    /// <summary>Gắn chỗ hiện thông báo trong app vào cửa sổ (gọi khi cửa sổ đã hiện: trình quản lý cần lớp phủ của cửa sổ mới vẽ được).</summary>
    public void Attach() => _inApp ??= new WindowNotificationManager(TopLevel.GetTopLevel(main)) { Position = NotificationPosition.BottomRight, MaxItems = 3 };

    /// <summary>Cửa sổ vừa mở lại từ khay: hiện các lời nhắc còn giữ.</summary>
    public void Flush()
    {
        foreach (var (t, b, p) in _pending) InApp(t, b, p);
        _pending.Clear();
    }

    private void InApp(string title, string body, string page)
    {
        Attach();
        _inApp!.Show(new Notification(title, body, NotificationType.Information, TimeSpan.FromSeconds(10), () => open(page)));
    }

    private static bool Run(string exe, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            return p is not null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Debug($"Nhắc hạn: không chạy được {exe}: {e.Message}");
            return false;
        }
    }

    /// <summary>Chuỗi AppleScript trong ngoặc kép (thoát \ và ").</summary>
    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
