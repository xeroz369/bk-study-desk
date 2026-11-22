using Microsoft.Win32;
using SoHocTap.Core;
using SoHocTap.Shell;
using Windows.UI.Notifications;

namespace BKStudyDesk.Desktop.Platform.Windows;

internal static class Toast
{
    private static bool _registered;

    public static string Setting() => Notifier().Setting.ToString();

    private static ToastNotifier Notifier()
    {
        if (AppPackage.IsPackaged) return ToastNotificationManager.CreateToastNotifier();
        Register();
        return ToastNotificationManager.CreateToastNotifier(AppInfo.Id);
    }

    public static bool Show(string title, string body, Action onClick)
    {
        try
        {
            var xml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var texts = xml.GetElementsByTagName("text");
            texts[0].AppendChild(xml.CreateTextNode(title));
            texts[1].AppendChild(xml.CreateTextNode(body));
            var toast = new ToastNotification(xml);
            toast.Activated += (_, _) => onClick();
            Notifier().Show(toast);
            return true;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Warn($"Thông báo Windows: {e.Message}");
            return false;
        }
    }

    private static void Register()
    {
        if (_registered) return;
        using var k = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{AppInfo.Id}");
        k.SetValue("DisplayName", AppInfo.Name);
        _registered = true;
    }
}
