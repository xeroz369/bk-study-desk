using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace BKStudyDesk.Desktop.Platform;

internal sealed class Notifier(Window main, Action<string> open)
{
    private WindowNotificationManager? _inApp;
    private readonly List<(string Title, string Body, string Page)> _pending = [];

    public void Show(string title, string body, string page, bool outside = false)
    {
        if (!outside && main.IsVisible && main.WindowState != WindowState.Minimized) { InApp(title, body, page); return; }
        if (OperatingSystem.IsLinux() && Run("notify-send", "-a", AppInfo.Name, title, body)) return;
        if (OperatingSystem.IsMacOS() && Run("osascript", "-e", $"display notification {Quote(body)} with title {Quote(title)}")) return;
#if WINDOWS
        if (Windows.Toast.Show(title, body, () => Avalonia.Threading.Dispatcher.UIThread.Post(() => open(page)))) return;
#endif
        _pending.Add((title, body, page));
    }

    public void Attach() => _inApp ??= new WindowNotificationManager(TopLevel.GetTopLevel(main)) { Position = NotificationPosition.BottomRight, MaxItems = 3 };

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

    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
