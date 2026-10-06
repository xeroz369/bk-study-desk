using System.Net;

namespace SoHocTap.Web;

/// <summary>
/// Lấy link token LMS (&lt;scheme&gt;://token=...) mà launch.php trả trong header Location. Trình duyệt nhúng không cho bắt link scheme lạ
/// trên mọi hệ điều hành (WebView2 không báo qua NavigationStarted), nên app gọi launch.php bằng HttpClient với cookie phiên LMS
/// lấy từ trình duyệt nhúng, không tự theo chuyển hướng.
/// </summary>
public static class LmsTokenGrab
{
    /// <summary>Link callback, hay null khi LMS không chuyển hướng tới scheme của app (chưa đăng nhập LMS, server đổi cách làm).</summary>
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
        // "moodlemobile://token=..." không phải URI hợp lệ với .NET (host có dấu =): đọc header thô, không qua Headers.Location.
        var location = res.Headers.NonValidated.TryGetValues("Location", out var values) ? values.FirstOrDefault() : null;
        return location is not null && location.StartsWith(scheme + "://", StringComparison.OrdinalIgnoreCase) ? location : null;
    }

    /// <summary>Handler thật: không tự theo chuyển hướng, không dùng kho cookie riêng (cookie đi theo header).</summary>
    public static HttpMessageHandler NoRedirectHandler() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false };
}
