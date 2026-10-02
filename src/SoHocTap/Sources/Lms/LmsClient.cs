using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;

namespace SoHocTap.Sources.Lms;

public sealed class LmsException(string message, string? code = null) : Exception(message)
{
    /// <summary>errorcode của Moodle (vd. "noreviewattempt"), null nếu lỗi không đến từ web service.</summary>
    public string? Code { get; } = code;
}

/// <summary>
/// Gọi web service của Moodle mobile app (BK-LMS) bằng token đã lưu.
/// Flow login: launch.php → &lt;scheme&gt;://token=base64(md5(site+passport):::token:::privatetoken).
/// </summary>
public static partial class LmsClient
{
    private static readonly HttpClient Http = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
    {
        Timeout = TimeSpan.FromMinutes(5),
    };

    private static string TokenFile => Path.Combine(Paths.Secrets, "lms.json");
    private static string PendingFile => Path.Combine(Paths.Secrets, "lms-pending.json");

    public static string Site => Config.Str("sources.lms.site").TrimEnd('/');
    public static string Scheme => Config.Str("sources.lms.scheme");

    /// <summary>Token LMS, lưu trong data\secrets và đã encrypt bằng DPAPI (xem <see cref="SecretStore"/>).</summary>
    public static string? Token => SecretStore.Read(TokenFile)?["token"]?.GetValue<string>();

    // ------------------------------------------------------------------ login

    /// <summary>Tạo một lượt login mới, trả về URL launch.php.</summary>
    public static string NewLaunchUrl()
    {
        var passport = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        SecretStore.Write(PendingFile, new JsonObject { ["passport"] = passport, ["at"] = Now() });
        return $"{Site}/admin/tool/mobile/launch.php?service={Uri.EscapeDataString(Config.Str("sources.lms.service"))}" +
               $"&passport={passport}&urlscheme={Uri.EscapeDataString(Scheme)}";
    }

    [GeneratedRegex(@"token=([A-Za-z0-9+/=_-]+)")] private static partial Regex TokenRx();

    /// <summary>Kiểm tra chữ ký của callback URL rồi lưu token. Trả về tên người dùng.</summary>
    public static async Task<string> FinishLoginAsync(string callbackUrl, CancellationToken ct)
    {
        var passport = SecretStore.Read(PendingFile)?["passport"]?.GetValue<string>()
                       ?? throw new LmsException("Không có yêu cầu đăng nhập nào đang chờ.");
        var m = TokenRx().Match(callbackUrl);
        if (!m.Success) throw new LmsException("Link trả về không có token đăng nhập.");
        var raw = m.Groups[1].Value;
        raw += new string('=', (4 - raw.Length % 4) % 4);
        var parts = Encoding.UTF8.GetString(Convert.FromBase64String(raw)).Split(":::");
        var expected = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(Site + passport)));
        if (parts.Length < 2 || parts[0] != expected) throw new LmsException("Chữ ký không khớp, bỏ qua token này.");
        SecretStore.Write(TokenFile, new JsonObject { ["token"] = parts[1], ["at"] = Now() });
        File.Delete(PendingFile);
        var info = await CallAsync("core_webservice_get_site_info", [], ct);
        return info["fullname"]?.GetValue<string>() ?? "";
    }

    /// <summary>Token LMS còn dùng được không (thử một request đọc nhẹ). Lỗi mạng thì coi như còn, để khỏi bắt login lại vô ích.</summary>
    public static async Task<bool> TokenValidAsync()
    {
        if (Token is null) return false;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await CallAsync("core_webservice_get_site_info", [], cts.Token);
            return true;
        }
        catch (LmsException) { return false; }
        catch (Exception) { return true; }
    }

    public static void Logout()
    {
        foreach (var f in new[] { TokenFile, PendingFile }) if (File.Exists(f)) File.Delete(f);
    }

    // ------------------------------------------------------------------ call API

    private static int _calls;

    /// <summary>Số request API LMS từ lúc mở app (ghi vào log mỗi lần sync để theo dõi lượng request).</summary>
    public static int Calls => Volatile.Read(ref _calls);

    public static async Task<JsonNode> CallAsync(string function, IEnumerable<KeyValuePair<string, string>> args, CancellationToken ct)
    {
        var token = Token ?? throw new LmsException("Chưa đăng nhập LMS.");
        var form = new List<KeyValuePair<string, string>>
        {
            new("wstoken", token), new("wsfunction", function), new("moodlewsrestformat", "json"),
        };
        form.AddRange(args);
        Interlocked.Increment(ref _calls);
        using var resp = await Http.PostAsync($"{Site}/webservice/rest/server.php", new FormUrlEncodedContent(form), ct);
        var node = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct)) ?? throw new LmsException($"{function}: trả về rỗng");
        if (node is JsonObject o && o["exception"] is not null)
        {
            var code = o["errorcode"]?.GetValue<string>();
            throw new LmsException(code is "invalidtoken" or "accessexception"
                ? "Phiên LMS hết hạn, cần đăng nhập lại."
                : $"{function}: {o["message"]}", code);
        }
        return node;
    }

    /// <summary>Param dạng mảng của Moodle: name[0]=a&amp;name[1]=b.</summary>
    public static IEnumerable<KeyValuePair<string, string>> Arr(string name, IEnumerable<object> values) =>
        values.Select((v, i) => new KeyValuePair<string, string>($"{name}[{i}]", v.ToString()!));

    public static KeyValuePair<string, string> Arg(string name, object value) => new(name, value.ToString()!);

    /// <summary>Chỉ URL https trên đúng host LMS mới được gắn token (link lạ trong nội dung LMS không được lấy token đi).</summary>
    public static bool IsLmsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
        && Uri.TryCreate(Site, UriKind.Absolute, out var site) && u.Host.Equals(site.Host, StringComparison.OrdinalIgnoreCase);

    /// <summary>Tải file LMS về <paramref name="dest"/>. Đếm byte khi tải: quá <paramref name="maxBytes"/> thì dừng (server trả sai kích thước).</summary>
    public static async Task DownloadAsync(string fileUrl, string dest, CancellationToken ct, long maxBytes = 4L << 30)
    {
        if (!IsLmsUrl(fileUrl)) throw new HttpRequestException("Link không thuộc LMS, không tải.");
        var url = fileUrl + (fileUrl.Contains('?') ? "&" : "?") + "token=" + Uri.EscapeDataString(Token ?? "");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var part = dest + ".part";
        using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            if (resp.Content.Headers.ContentLength > maxBytes) throw new HttpRequestException("File quá lớn, không tải.");
            await using var fs = File.Create(part);
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            if (!await CopyLimitedAsync(src, fs, maxBytes, ct)) { fs.Close(); File.Delete(part); throw new HttpRequestException("File quá lớn, không tải."); }
        }
        File.Move(part, dest, overwrite: true);
    }


    /// <summary>
    /// Small LMS image (pluginfile) as a data URI, or null if it is not an image / too large / fails.
    /// Plain pluginfile.php needs a browser session, so it is switched to webservice/pluginfile.php + token.
    /// </summary>
    public static async Task<string?> DataUriAsync(string fileUrl, CancellationToken ct, int maxBytes = 1_500_000)
    {
        if (!IsLmsUrl(fileUrl)) return null;
        try
        {
            var url = fileUrl.Replace("/pluginfile.php/", "/webservice/pluginfile.php/").Replace("/webservice/webservice/", "/webservice/");
            url += (url.Contains('?') ? "&" : "?") + "token=" + Uri.EscapeDataString(Token ?? "");
            Interlocked.Increment(ref _calls);
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            var type = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (!resp.IsSuccessStatusCode || type is not ("image/png" or "image/jpeg" or "image/gif" or "image/webp")) return null;
            if (resp.Content.Headers.ContentLength > maxBytes) return null;
            using var ms = new MemoryStream();
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            return await CopyLimitedAsync(src, ms, maxBytes, ct) ? $"data:{type};base64,{Convert.ToBase64String(ms.GetBuffer(), 0, (int)ms.Length)}" : null;
        }
        catch (HttpRequestException) { return null; }
    }

    /// <summary>Chép tối đa <paramref name="max"/> byte; false nếu nguồn còn dài hơn (không tin Content-Length).</summary>
    private static async Task<bool> CopyLimitedAsync(Stream src, Stream dst, long max, CancellationToken ct)
    {
        var buf = new byte[81920];
        long total = 0;
        int n;
        while ((n = await src.ReadAsync(buf, ct)) > 0)
        {
            if ((total += n) > max) return false;
            await dst.WriteAsync(buf.AsMemory(0, n), ct);
        }
        return true;
    }

    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
