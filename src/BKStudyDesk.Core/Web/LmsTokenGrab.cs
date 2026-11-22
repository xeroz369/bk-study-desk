using System.Net;

namespace SoHocTap.Web;

public static class LmsTokenGrab
{
    public static async Task<string?> LaunchCallbackAsync(HttpMessageHandler handler, string launchUrl, IEnumerable<Cookie> cookies, string scheme, CancellationToken ct)
    {
        var target = new Uri(launchUrl);
        using var http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
        using var req = new HttpRequestMessage(HttpMethod.Get, target);
        var jar = new CookieContainer();
        foreach (var c in cookies)
            if (target.Host.Equals(c.Domain.TrimStart('.'), StringComparison.OrdinalIgnoreCase) || target.Host.EndsWith("." + c.Domain.TrimStart('.'), StringComparison.OrdinalIgnoreCase))
                jar.Add(target, new Cookie(c.Name, c.Value, "/"));
        var header = jar.GetCookieHeader(target);
        if (header.Length > 0) req.Headers.Add("Cookie", header);
        using var res = await http.SendAsync(req, ct);
        var location = res.Headers.NonValidated.TryGetValues("Location", out var values) ? values.FirstOrDefault() : null;
        return location is not null && location.StartsWith(scheme + "://", StringComparison.OrdinalIgnoreCase) ? location : null;
    }

    public static HttpMessageHandler NoRedirectHandler() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false };
}
