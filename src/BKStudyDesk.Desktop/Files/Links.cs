using Avalonia.Controls;
using BKStudyDesk.Desktop.Views;
using SoHocTap.Web;

namespace BKStudyDesk.Desktop.Files;

/// <summary>
/// Mở một link từ bất kỳ trang nào: trang của trường (LMS, MyBK, sso.hosts) mặc định mở trong cửa sổ của app (SchoolWindow, dùng chung
/// phiên đăng nhập của app; trình duyệt ngoài có thể chưa đăng nhập). Cài đặt app.openSchoolIn = "browser" thì mở bằng trình duyệt của máy.
/// http trần tới trường đổi sang https.
/// </summary>
internal static class Links
{
    public static async Task OpenAsync(Control from, string url, string title = "")
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return;
        // Email giảng viên trong bài, tin nhắn: mở app mail của máy.
        if (u.Scheme == "mailto")
        {
            try { if (TopLevel.GetTopLevel(from) is { } mail) await mail.Launcher.LaunchUriAsync(u); }
            catch (Exception e) { SoHocTap.Core.Log.Warn($"Mở email: {e.Message}"); }   // máy không có app mail: bỏ qua, không làm sập app
            return;
        }
        if (u.Scheme is not ("http" or "https")) return;
        url = SchoolUrls.Secure(url);
        if (SoHocTap.Core.Settings.App.OpenSchoolIn == "app" && SchoolUrls.IsSchoolHost(url) && TopLevel.GetTopLevel(from) is Window owner)
        {
            new SchoolWindow(url, title).Show(owner);
            return;
        }
        // Trang LMS mở bằng trình duyệt: kèm khóa tự đăng nhập (như ứng dụng Moodle) để khỏi đăng nhập lại trong trình duyệt.
        // Offline thì không xin khóa (không gọi LMS), mở địa chỉ thường.
        if (!SoHocTap.Core.OfflineMode.On && await SoHocTap.Sources.Lms.LmsClient.AutoLoginUrlAsync(url, CancellationToken.None) is { } auto) url = auto;
        if (TopLevel.GetTopLevel(from) is { } top) await top.Launcher.LaunchUriAsync(new Uri(url));
    }
}
