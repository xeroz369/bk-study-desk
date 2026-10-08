using Microsoft.Win32;
using SoHocTap.Core;
using SoHocTap.Shell;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace BKStudyDesk.Desktop.Platform.Windows;

/// <summary>
/// Thông báo hệ thống của Windows (toast) cho bản không đóng gói: đăng ký tên hiện (AppUserModelId) dưới HKCU\Software\Classes, cách
/// Windows App SDK làm cho app không đóng gói, rồi gửi toast hai dòng. Chỉ biên dịch cho net10.0-windows. Bấm vào thông báo khi app
/// đang chạy thì mở đúng trang.
/// </summary>
internal static class Toast
{
    private static bool _registered;

    /// <summary>Windows cho hiện thông báo của app không (người dùng, chính sách có thể tắt). Không gửi gì.</summary>
    public static string Setting()
    {
        Register();
        return ToastNotificationManager.CreateToastNotifier(AppInfo.Id).Setting.ToString();
    }

    public static bool Show(string title, string body, Action onClick)
    {
        try
        {
            Register();
            var xml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var texts = xml.GetElementsByTagName("text");
            texts[0].AppendChild(xml.CreateTextNode(title));
            texts[1].AppendChild(xml.CreateTextNode(body));
            var toast = new ToastNotification(xml);
            toast.Activated += (_, _) => onClick();
            ToastNotificationManager.CreateToastNotifier(AppInfo.Id).Show(toast);
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
