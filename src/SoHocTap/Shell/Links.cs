using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Shell;

/// <summary>
/// Mở link web ra trình duyệt. Trình duyệt mặc định có thể hỏng (vd. cài ở ổ đã rút, gỡ mà chưa đổi mặc định):
/// khi đó Windows báo "Application not found" và không có gì mở ra. Lúc ấy thử Edge (Windows nào cũng có), vẫn không được
/// thì chép link và báo cho người dùng.
/// </summary>
internal static class Links
{
    public static bool Open(string url)
    {
        // Chỉ mở link web: chặn file:, ổ đĩa, UNC, ms-*, search-ms… (link trong nội dung LMS hay gói quiz không được chạy file).
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https") || u.IsUnc)
        {
            Log.Warn($"Bỏ qua link không phải http/https: {url}");
            return false;
        }
        url = u.AbsoluteUri;   // đã mã hóa: không còn dấu cách, nháy kép để chèn tham số dòng lệnh
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Win32Exception e) { Log.Warn($"Trình duyệt mặc định không mở được link ({e.Message}), thử Edge."); }
        try
        {
            Process.Start(new ProcessStartInfo("msedge.exe", url) { UseShellExecute = true });
            return true;
        }
        catch (Win32Exception e) { Log.Warn($"Edge cũng không mở được: {e.Message}"); }
        try { Clipboard.SetText(url); }
        catch (System.Runtime.InteropServices.ExternalException) { }
        MessageBox.Show(L.F("link.failed", url), AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }
}
