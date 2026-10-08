using System.Net;
using SoHocTap.Core;

namespace SoHocTap.Web;

/// <summary>
/// Giữ phiên SSO sau mỗi lượt đi qua SSO (cùng quy tắc với bản 1.x, Shell/WebHost.SessionKeeper): cookie của các host trường được
/// đẩy hạn tới now + rememberDays (SsoLifetime.CookieExpiry), cookie trang khác không đụng tới. Hạn phiên thật do server quyết.
/// </summary>
public static class SsoCookies
{
    /// <summary>Cookie cần ghi lại vào trình duyệt nhúng (đã đặt Expires mới). Cookie phiên là Expires == DateTime.MinValue.</summary>
    public static IReadOnlyList<Cookie> Extend(IEnumerable<Cookie> cookies, IReadOnlyList<string> hosts, int days, DateTime now)
    {
        var changed = new List<Cookie>();
        foreach (var c in cookies)
        {
            var domain = c.Domain.TrimStart('.');
            if (!hosts.Any(h => domain.Equals(h, StringComparison.OrdinalIgnoreCase) || domain.EndsWith("." + h, StringComparison.OrdinalIgnoreCase))) continue;
            if (SsoLifetime.CookieExpiry(c.Expires == DateTime.MinValue, c.Expires, now, days) is not { } until) continue;
            c.Expires = until;
            changed.Add(c);
        }
        return changed;
    }
}
