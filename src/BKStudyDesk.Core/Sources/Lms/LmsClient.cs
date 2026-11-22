using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;

namespace SoHocTap.Sources.Lms;

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

    public static string? Token
    {
        get
        {
            var stamp = File.Exists(TokenFile) ? File.GetLastWriteTimeUtc(TokenFile) : DateTime.MinValue;
            lock (TokenGate)
            {
                if (stamp == _tokenStamp && _tokenRead) return _token;
            }
            var token = stamp == DateTime.MinValue ? null : SecretStore.Read(TokenFile)?["token"]?.GetValue<string>();
            lock (TokenGate) { _token = token; _tokenStamp = stamp; _tokenRead = true; }
            return token;
        }
    }

    public static bool HasToken => Token is not null;

    private static readonly object TokenGate = new();
    private static string? _token;
    private static DateTime _tokenStamp;
    private static bool _tokenRead;

    private static void ForgetToken()
    {
        lock (TokenGate) { _tokenRead = false; _token = null; }
    }

    private static long _autoLoginAt;

    public static async Task<string?> AutoLoginUrlAsync(string url, CancellationToken ct)
    {
        if (!IsLmsUrl(url) || SecretStore.Read(TokenFile)?["private"]?.GetValue<string>() is not { Length: > 0 } privateToken) return null;
        var gap = Config.Int("sources.lms.autoLoginMinutes", 6) * 60L;
        if (Now() - Interlocked.Read(ref _autoLoginAt) < gap) return null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));
            var info = await CallAsync("core_webservice_get_site_info", [], cts.Token);
            var key = await CallAsync("tool_mobile_get_autologin_key", [Arg("privatetoken", privateToken)], cts.Token);
            if (info["userid"]?.GetValue<long>() is not { } uid || key["key"]?.GetValue<string>() is not { Length: > 0 } k
                || key["autologinurl"]?.GetValue<string>() is not { Length: > 0 } entry || !IsLmsUrl(entry)) return null;
            Interlocked.Exchange(ref _autoLoginAt, Now());
            return $"{entry}?userid={uid}&key={Uri.EscapeDataString(k)}&urltogo={Uri.EscapeDataString(url)}";
        }
        catch (Exception e) when (e is LmsException or SessionExpiredException or HttpRequestException or TimeoutException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            Log.Info($"Mở LMS bằng trình duyệt: không xin được khóa tự đăng nhập ({e.GetType().Name}), mở địa chỉ thường");
            return null;
        }
    }

    public static string NewLaunchUrl()
    {
        var passport = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        SecretStore.Write(PendingFile, new JsonObject { ["passport"] = passport, ["at"] = Now() });
        return $"{Site}/admin/tool/mobile/launch.php?service={Uri.EscapeDataString(Config.Str("sources.lms.service"))}" +
               $"&passport={passport}&urlscheme={Uri.EscapeDataString(Scheme)}";
    }

    [GeneratedRegex(@"token=([A-Za-z0-9+/=_-]+)")] private static partial Regex TokenRx();

    public static async Task<string> FinishLoginAsync(string callbackUrl, CancellationToken ct)
    {
        var passport = SecretStore.Read(PendingFile)?["passport"]?.GetValue<string>()
                       ?? throw new LmsException("Không có yêu cầu đăng nhập nào đang chờ.");
        var m = TokenRx().Match(callbackUrl);
        if (!m.Success) throw new LmsException("Link trả về không có token đăng nhập.");
        var raw = m.Groups[1].Value;
        raw += new string('=', (4 - raw.Length % 4) % 4);
        var parts = Encoding.UTF8.GetString(Convert.FromBase64String(raw)).Split(":::");
#pragma warning disable CA5351
        var expected = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(Site + passport)));
#pragma warning restore CA5351
        if (parts.Length < 2 || parts[0] != expected) throw new LmsException("Chữ ký không khớp, bỏ qua token này.");
        SecretStore.Write(TokenFile, new JsonObject { ["token"] = parts[1], ["private"] = parts.Length > 2 ? parts[2] : null, ["at"] = Now() });
        ForgetToken();
        File.Delete(PendingFile);
        var info = await CallAsync("core_webservice_get_site_info", [], ct);
        return info["fullname"]?.GetValue<string>() ?? "";
    }

    public static async Task<bool> TokenValidAsync()
    {
        if (Token is null) return false;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await CallAsync("core_webservice_get_site_info", [], cts.Token);
            return true;
        }
        catch (Exception e) when (e is LmsException or SessionExpiredException) { return false; }
        catch (Exception e)
        {
            Log.Debug($"Kiểm token LMS: {e.GetType().Name}, coi như còn");
            return true;
        }
    }

    public static void Logout()
    {
        foreach (var f in new[] { TokenFile, PendingFile }) if (File.Exists(f)) File.Delete(f);
        ForgetToken();
    }

    private static int _calls;

    public static int Calls => Volatile.Read(ref _calls);

    private static readonly Pace ApiPace = new(() => TimeSpan.FromMilliseconds(Config.Int("sources.lms.gapMs", 100)));
    private static readonly Pace FilePace = new(() => TimeSpan.FromMilliseconds(Config.Int("sources.lms.fileGapMs", 1000)));

    private static readonly TimeSpan GiveUpPause = TimeSpan.FromMinutes(2);

    private static TimeSpan CallTimeout => TimeSpan.FromSeconds(Config.Int("sources.lms.timeoutSeconds", 45));

    public static async Task<JsonNode> CallAsync(string function, IEnumerable<KeyValuePair<string, string>> args, CancellationToken ct)
    {
        var token = Token ?? throw new SessionExpiredException("Chưa đăng nhập LMS.");
        var form = new List<KeyValuePair<string, string>>
        {
            new("wstoken", token), new("wsfunction", function), new("moodlewsrestformat", "json"),
        };
        form.AddRange(args);
        if (ApiPace.PausedFor > TimeSpan.FromSeconds(30))
            throw new HttpRequestException("LMS đang quá tải, app tạm dừng gọi LMS. Lần đồng bộ sau sẽ thử lại.", null, HttpStatusCode.ServiceUnavailable);
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
                    ApiPace.PauseFor(GiveUpPause);
                    throw new HttpRequestException($"LMS đang giới hạn truy cập (HTTP {status}). App tạm dừng, lần đồng bộ sau sẽ thử lại.", null, (HttpStatusCode)status);
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
                Log.Warn($"LMS {function}: HTTP {status}, trả về không phải JSON");
                throw new HttpRequestException($"LMS trả về trang lỗi (HTTP {status}), có thể đang bảo trì. Thử lại sau.", null, (HttpStatusCode)status);
            }
            if (node is null) throw new LmsException($"{function}: trả về rỗng");
            if (node is JsonObject o && o["exception"] is not null)
            {
                var code = o["errorcode"]?.GetValue<string>();
                Log.Debug($"LMS {function}: lỗi {code}, {sw.ElapsedMilliseconds} ms");
                if (code is "invalidtoken" or "accessexception") throw new SessionExpiredException("Phiên LMS hết hạn, cần đăng nhập lại.");
                throw new LmsException($"{function}: {o["message"]}", code);
            }
            Log.Debug($"LMS {function}: {body.Length / 1024} KB, {sw.ElapsedMilliseconds} ms");
            return node;
        }
    }

    public static IEnumerable<KeyValuePair<string, string>> Arr(string name, IEnumerable<object> values) =>
        values.Select((v, i) => new KeyValuePair<string, string>($"{name}[{i}]", v.ToString()!));

    public static KeyValuePair<string, string> Arg(string name, object value) => new(name, value.ToString()!);

    public static bool IsLmsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
        && Uri.TryCreate(Site, UriKind.Absolute, out var site) && u.Host.Equals(site.Host, StringComparison.OrdinalIgnoreCase);

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
                throw new HttpRequestException($"LMS đang giới hạn tải file (HTTP {(int)resp.StatusCode}), app tạm dừng {wait.TotalSeconds:0} giây.", null, resp.StatusCode);
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
