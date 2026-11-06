using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using static SoHocTap.Sources.Lms.LmsClient;

namespace SoHocTap.Sources.Lms;

/// <summary>
/// Ảnh trong bài thông báo, trả lời diễn đàn, tin nhắn, trang LMS: tải về data/lms-media khi đồng bộ để xem được khi LMS sập.
/// Chỉ ảnh trên LMS của trường (pluginfile, cần đăng nhập; ảnh ngoài thì để nguyên link). Tên file là băm của địa chỉ ảnh
/// (bỏ token, quy về /pluginfile.php/), nên một ảnh dùng ở nhiều chỗ chỉ tải một lần.
/// </summary>
public static partial class LmsImages
{
    public static string Dir => Paths.DataFile("lms-media");

    [GeneratedRegex(@"<img\b[^>]*?\bsrc\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)] private static partial Regex ImgSrc();
    [GeneratedRegex(@"([?&])token=[^&#]*&?", RegexOptions.IgnoreCase)] private static partial Regex TokenArg();

    /// <summary>Địa chỉ chuẩn của ảnh: giải mã HTML, bỏ token, /webservice/pluginfile.php/ thành /pluginfile.php/.</summary>
    public static string Normalize(string url)
    {
        var u = WebUtility.HtmlDecode(url).Trim();
        u = TokenArg().Replace(u, "$1").TrimEnd('?', '&');
        return u.Replace("/webservice/pluginfile.php/", "/pluginfile.php/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ảnh của LMS trong <paramref name="html"/> (địa chỉ chuẩn), bỏ trùng.</summary>
    public static IEnumerable<string> UrlsIn(string? html) =>
        string.IsNullOrEmpty(html) ? []
            : ImgSrc().Matches(html).Select(m => Normalize(m.Groups[1].Value))
                .Where(u => IsLmsUrl(u) && u.Contains("/pluginfile.php/", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.Ordinal);

    /// <summary>File trên máy của ảnh (đường dẫn đầy đủ); null nếu chưa tải.</summary>
    public static string? LocalFile(string url)
    {
        var path = PathOf(Normalize(url));
        return File.Exists(path) ? path : null;
    }

    /// <summary>Đường dẫn file trên máy cho ảnh (dù đã tải hay chưa).</summary>
    public static string PathFor(string url) => PathOf(Normalize(url));

    private static string PathOf(string normalized)
    {
        var ext = Path.GetExtension(new Uri(normalized).AbsolutePath).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp")) ext = ".img";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..20].ToLowerInvariant();
        return Path.Combine(Dir, hash + ext);
    }

    /// <summary>
    /// Tải ảnh còn thiếu của các đoạn HTML (tối đa sources.lms.imagesPerSync ảnh mỗi lượt, mỗi ảnh tối đa sources.lms.imageMb MB).
    /// Ảnh lỗi thì bỏ qua, lượt sau thử lại. Trả số ảnh đã tải.
    /// </summary>
    public static async Task<int> SaveAsync(IEnumerable<string?> htmls, CancellationToken ct)
    {
        if (!Config.Bool("sources.lms.images", true)) return 0;
        var max = Config.Int("sources.lms.imageMb", 5) * 1024L * 1024L;
        var missing = htmls.SelectMany(UrlsIn).Distinct(StringComparer.Ordinal).Where(u => !File.Exists(PathOf(u)))
            .Take(Config.Int("sources.lms.imagesPerSync", 60)).ToList();
        var got = 0;
        foreach (var u in missing)
        {
            ct.ThrowIfCancellationRequested();
            var ws = u.Replace("/pluginfile.php/", "/webservice/pluginfile.php/", StringComparison.OrdinalIgnoreCase);
            try { await DownloadAsync(ws, PathOf(u), ct, max); got++; }
            catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                Log.Debug($"Ảnh LMS {Log.Where(u)}: {e.Message}");
            }
        }
        if (got > 0) Log.Info($"Ảnh LMS: tải {got}/{missing.Count} ảnh");
        return got;
    }

    /// <summary>Mọi đoạn HTML có thể chứa ảnh: bài thông báo và trả lời (lms.json), tin nhắn, trang.</summary>
    public static IEnumerable<string?> HtmlOf(JsonArray? announcements, JsonObject? messages, JsonObject? pages)
    {
        foreach (var a in (announcements ?? []).OfType<JsonObject>())
        {
            yield return a["message"]?.GetValue<string>();
            foreach (var p in (a["posts"] as JsonArray ?? []).OfType<JsonObject>()) yield return p["message"]?.GetValue<string>();
        }
        foreach (var c in (messages?["conversations"] as JsonArray ?? []).OfType<JsonObject>())
            foreach (var m in (c["messages"] as JsonArray ?? []).OfType<JsonObject>()) yield return m["text"]?.GetValue<string>();
        foreach (var (_, p) in pages?["pages"] as JsonObject ?? [])
            if (p is JsonObject o) { yield return o["html"]?.GetValue<string>(); yield return o["intro"]?.GetValue<string>(); }
    }
}
