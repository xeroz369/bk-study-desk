using Avalonia.Controls;
using BKStudyDesk.Desktop.Views;
using SoHocTap.Web;

namespace BKStudyDesk.Desktop.Files;

/// <summary>
/// Mở một link từ bất kỳ trang nào (như Shell/Links và AppHost.OpenWeb của 1.x): trang của trường (LMS, MyBK, sso.hosts) mở trong cửa sổ
/// của app (SchoolWindow, dùng chung phiên đăng nhập của app); http trần tới trường đổi sang https; trang khác mở bằng trình duyệt mặc định.
/// </summary>
internal static class Links
{
    public static async Task OpenAsync(Control from, string url, string title = "")
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) return;
        url = SchoolUrls.Secure(url);
        if (SchoolUrls.IsSchoolHost(url) && TopLevel.GetTopLevel(from) is Window owner)
        {
            new SchoolWindow(url, title).Show(owner);
            return;
        }
        if (TopLevel.GetTopLevel(from) is { } top) await top.Launcher.LaunchUriAsync(new Uri(url));
    }
}
