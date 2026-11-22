using System.Net;
using SoHocTap.Core;

namespace SoHocTap.Web;

public static class SsoCookies
{
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
