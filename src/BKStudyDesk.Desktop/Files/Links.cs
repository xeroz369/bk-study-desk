using Avalonia.Controls;
using BKStudyDesk.Desktop.Views;
using SoHocTap.Web;

namespace BKStudyDesk.Desktop.Files;

internal static class Links
{
    public static async Task OpenAsync(Control from, string url, string title = "")
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return;
        if (u.Scheme == "mailto")
        {
            try { if (TopLevel.GetTopLevel(from) is { } mail) await mail.Launcher.LaunchUriAsync(u); }
            catch (Exception e) { SoHocTap.Core.Log.Warn($"Mở email: {e.Message}"); }
            return;
        }
        if (u.Scheme is not ("http" or "https")) return;
        url = SchoolUrls.Secure(url);
        if (SoHocTap.Core.Settings.App.OpenSchoolIn == "app" && SchoolUrls.IsSchoolHost(url) && TopLevel.GetTopLevel(from) is Window owner)
        {
            new SchoolWindow(url, title).Show(owner);
            return;
        }
        if (!SoHocTap.Core.OfflineMode.On && await SoHocTap.Sources.Lms.LmsClient.AutoLoginUrlAsync(url, CancellationToken.None) is { } auto) url = auto;
        if (TopLevel.GetTopLevel(from) is { } top) await top.Launcher.LaunchUriAsync(new Uri(url));
    }
}
