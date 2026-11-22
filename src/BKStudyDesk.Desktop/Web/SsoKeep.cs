using SoHocTap.Core;
using SoHocTap.Web;

namespace BKStudyDesk.Desktop.Web;

internal static class SsoKeep
{
    public static async Task PersistAsync(WebPage page)
    {
        try
        {
            var days = Settings.Sso.RememberDays;
            if (days <= 0) return;
            var changed = SsoCookies.Extend(await page.CookiesAsync(), Config.List("sso.hosts"), days, DateTime.Now);
            foreach (var c in changed) page.SetCookie(c);
            if (changed.Count > 0) Log.Debug($"Giữ session: gia hạn {changed.Count} cookie của trường, {days} ngày");
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Error("Giữ session", e); }
    }
}
