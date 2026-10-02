using System.Diagnostics;
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

    // Giãn cách request tới LMS, dùng chung mọi luồng: API cách nhau sources.lms.gapMs, mỗi file tài liệu cách nhau sources.lms.fileGapMs.
    private static readonly Pace ApiPace = new(() => TimeSpan.FromMilliseconds(Config.Int("sources.lms.gapMs", 300)));
    private static readonly Pace FilePace = new(() => TimeSpan.FromMilliseconds(Config.Int("sources.lms.fileGapMs", 1000)));

    /// <summary>Một request API chờ tối đa bấy nhiêu giây; quá thì báo lỗi rõ thay vì để người dùng chờ mãi.</summary>
    private static TimeSpan CallTimeout => TimeSpan.FromSeconds(Config.Int("sources.lms.timeoutSeconds", 45));

    public static async Task<JsonNode> CallAsync(string function, IEnumerable<KeyValuePair<string, string>> args, CancellationToken ct)
    {
        var token = Token ?? throw new LmsException("Chưa đăng nhập LMS.");
        var form = new List<KeyValuePair<string, string>>
        {
            new("wstoken", token), new("wsfunction", function), new("moodlewsrestformat", "json"),
        };
        form.AddRange(args);
        // Server báo 429/503 thì nghỉ theo Retry-After (hoặc full jitter) rồi thử lại, tối đa 3 lần; vẫn bị thì dừng và báo lỗi.
        for (var attempt = 0; ; attempt++)
        {
            await ApiPace.WaitAsync(ct);
            Interlocked.Increment(ref _calls);
            var sw = Stopwatch.StartNew();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(CallTimeout);
            string body;
            int status;
            try
            {
                using var content = new FormUrlEncodedContent(form);
                using var resp = await Http.PostAsync($"{Site}/webservice/rest/server.php", content, timeout.Token);
                status = (int)resp.StatusCode;
                if (Pace.IsThrottle(status))
                {
                    var wait = Pace.Backoff(Pace.RetryAfter(resp.Headers.RetryAfter, resp.Headers.Date, DateTimeOffset.UtcNow), attempt);
                    ApiPace.PauseFor(wait);
                    Log.Warn($"LMS {function}: HTTP {status}, tạm dừng gọi LMS {wait.TotalSeconds:0} giây");
                    if (attempt < 2) continue;
                    throw new HttpRequestException($"LMS đang giới hạn truy cập (HTTP {status}). App tạm dừng, lần đồng bộ sau sẽ thử lại.");
                }
                body = await resp.Content.ReadAsStringAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Log.Warn($"LMS {function}: quá {CallTimeout.TotalSeconds:0} giây không phản hồi");
                throw new TimeoutException($"LMS không phản hồi sau {CallTimeout.TotalSeconds:0} giây. Máy chủ trường có thể đang chậm, thử lại sau.");
            }
            catch (HttpRequestException e) when (e.StatusCode is null && e.InnerException is not null)
            {
                Log.Warn($"LMS {function}: không kết nối được ({e.HttpRequestError})");
                throw new HttpRequestException("Không kết nối được LMS. Kiểm tra mạng rồi thử lại.", e);
            }

            JsonNode? node;
            try { node = JsonNode.Parse(body); }
            catch (System.Text.Json.JsonException)
            {
                // Trang HTML (bảo trì, lỗi proxy...) thay vì JSON.
                Log.Warn($"LMS {function}: HTTP {status}, trả về không phải JSON");
                throw new HttpRequestException($"LMS trả về trang lỗi (HTTP {status}), có thể đang bảo trì. Thử lại sau.");
            }
            if (node is null) throw new LmsException($"{function}: trả về rỗng");
            if (node is JsonObject o && o["exception"] is not null)
            {
                var code = o["errorcode"]?.GetValue<string>();
                Log.Debug($"LMS {function}: lỗi {code}, {sw.ElapsedMilliseconds} ms");
                throw new LmsException(code is "invalidtoken" or "accessexception"
                    ? "Phiên LMS hết hạn, cần đăng nhập lại."
                    : $"{function}: {o["message"]}", code);
            }
            Log.Debug($"LMS {function}: {body.Length / 1024} KB, {sw.ElapsedMilliseconds} ms");
            return node;
        }
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
        await FilePace.WaitAsync(ct);
        Interlocked.Increment(ref _calls);
        var sw = Stopwatch.StartNew();
        using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            if (Pace.IsThrottle((int)resp.StatusCode))
            {
                var wait = Pace.Backoff(Pace.RetryAfter(resp.Headers.RetryAfter, resp.Headers.Date, DateTimeOffset.UtcNow));
                FilePace.PauseFor(wait);
                Log.Warn($"Tải file LMS: HTTP {(int)resp.StatusCode}, tạm dừng tải {wait.TotalSeconds:0} giây");
                throw new HttpRequestException($"LMS đang giới hạn tải file (HTTP {(int)resp.StatusCode}), app tạm dừng {wait.TotalSeconds:0} giây.");
            }
            resp.EnsureSuccessStatusCode();
            if (resp.Content.Headers.ContentLength > maxBytes) throw new HttpRequestException("File quá lớn, không tải.");
            await using var fs = File.Create(part);
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            if (!await CopyLimitedAsync(src, fs, maxBytes, ct)) { fs.Close(); File.Delete(part); throw new HttpRequestException("File quá lớn, không tải."); }
            Log.Debug($"Tải {Log.Where(fileUrl)}: {fs.Length / 1024} KB, {sw.ElapsedMilliseconds} ms");
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
            await ApiPace.WaitAsync(ct);
            Interlocked.Increment(ref _calls);
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (Pace.IsThrottle((int)resp.StatusCode)) ApiPace.PauseFor(Pace.Backoff(Pace.RetryAfter(resp.Headers.RetryAfter, resp.Headers.Date, DateTimeOffset.UtcNow)));
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
