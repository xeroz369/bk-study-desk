using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Api;
using SoHocTap.Core;

namespace SoHocTap.Web;

/// <summary>
/// Máy chủ cục bộ cho khung Luyện tập (Svelte, ui/) của bản đa nền tảng. Trình duyệt nhúng của Avalonia không cho app tự trả nội dung cho
/// một host ảo như WebView2 (bản 1.x, Shell/WebUi), nên app mở một HttpListener chỉ ở 127.0.0.1, cổng ngẫu nhiên, mọi đường dẫn nằm
/// dưới một khóa bí mật ngẫu nhiên (http://127.0.0.1:port/&lt;khóa&gt;/...): trang web khác trên máy không đoán được để gọi vào.
/// Khung dùng đường dẫn tương đối (Vite base './') nên chạy được dưới khóa đó mà không sửa gì.
/// - /&lt;khóa&gt;/api/app/message: khung báo app (phím tắt, chuyển trang), thay cho chrome.webview.postMessage của WebView2.
/// - /&lt;khóa&gt;/api/*: ApiRouter (cùng bản 1.x), phải có header X-App và Host đúng 127.0.0.1 (chặn trang khác, DNS rebinding).
/// - còn lại: file của ui/ và content/ (StaticRoutes).
/// </summary>
public sealed partial class LocalServer(ApiRouter router) : IDisposable
{
    private HttpListener? _listener;
    private string _host = "";

    /// <summary>Khóa trong đường dẫn (32 ký tự hex).</summary>
    public string Key { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    /// <summary>Gốc của khung (kết thúc bằng /), có sau Start.</summary>
    public string Root { get; private set; } = "";

    /// <summary>Tin khung gửi app (JSON của toApp trong desktop.svelte.ts). Gọi trên thread nền.</summary>
    public event Action<JsonObject>? Message;

    /// <summary>Mở cổng ngẫu nhiên còn trống trên 127.0.0.1 rồi phục vụ ở nền. Trả về Root.</summary>
    public string Start()
    {
        for (var attempt = 0; ; attempt++)
        {
            var port = FreePort();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/{Key}/");
            try { listener.Start(); }
            catch (HttpListenerException) when (attempt < 5) { listener.Close(); continue; }   // cổng vừa bị lấy mất: thử cổng khác
            _listener = listener;
            _host = $"127.0.0.1:{port}";
            Root = $"http://{_host}/{Key}/";
            _ = Task.Run(LoopAsync);
            return Root;
        }
    }

    private static int FreePort()
    {
        using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private async Task LoopAsync()
    {
        while (_listener is { IsListening: true } l)
        {
            HttpListenerContext ctx;
            try { ctx = await l.GetContextAsync(); }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var res = ctx.Response;
        try
        {
            var (status, type, cache, body) = await RespondAsync(ctx.Request);
            res.StatusCode = status;
            res.ContentType = type;
            res.Headers["Cache-Control"] = cache;
            res.Headers["X-Content-Type-Options"] = "nosniff";
            if (type.StartsWith("text/html", StringComparison.Ordinal)) res.Headers["Content-Security-Policy"] = StaticRoutes.Csp;
            res.ContentLength64 = body.Length;
            if (ctx.Request.HttpMethod != "HEAD") await res.OutputStream.WriteAsync(body);   // HEAD: chỉ header
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Error("Máy chủ Luyện tập " + Log.Where(ctx.Request.RawUrl ?? ""), e);
            try { res.StatusCode = 500; } catch (InvalidOperationException) { }
        }
        finally { try { res.Close(); } catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { } }
    }

    /// <summary>Trả lời một request: (mã, kiểu nội dung, Cache-Control, nội dung).</summary>
    internal async Task<(int Status, string Type, string Cache, byte[] Body)> RespondAsync(HttpListenerRequest req)
    {
        if (!string.Equals(req.Headers["Host"], _host, StringComparison.OrdinalIgnoreCase)) return Plain(421);
        var path = Uri.UnescapeDataString(req.Url!.AbsolutePath);
        var prefix = "/" + Key;
        if (!path.StartsWith(prefix + "/", StringComparison.Ordinal)) return Plain(404);
        path = path[prefix.Length..];
        if (!path.StartsWith("/api/", StringComparison.Ordinal))
        {
            if (req.HttpMethod != "GET" && req.HttpMethod != "HEAD") return Plain(405);
            return StaticRoutes.Resolve(path) is { } f ? (200, f.Type, f.Cache, await File.ReadAllBytesAsync(f.Full)) : Plain(404);
        }
        // Chỉ trang của app gọi được /api: trang khác gửi header lạ thì trình duyệt chặn bằng preflight (app không trả lời OPTIONS).
        if (req.Headers["X-App"] is null) return Plain(403);
        string? body = null;
        if (req.HasEntityBody)
        {
            using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
            body = await reader.ReadToEndAsync();
        }
        if (path == "/api/app/message")
        {
            if (body is not null && JsonNode.Parse(body) is JsonObject m) Message?.Invoke(m);
            return (204, "text/plain", "no-store", []);
        }
        var q = req.QueryString.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => req.QueryString[k] ?? "");
        var r = await router.HandleAsync(new ApiRequest(req.HttpMethod, path, q, body), CancellationToken.None);
        return (r.Status, r.ContentType, "no-store", r.Body);
    }

    private static (int, string, string, byte[]) Plain(int status) => (status, "text/plain; charset=utf-8", "no-store", []);

    public void Dispose()
    {
        try { _listener?.Close(); } catch (ObjectDisposedException) { }
        _listener = null;
    }
}

/// <summary>
/// Đường dẫn của khung Luyện tập thành file trên đĩa (cùng quy tắc với bản 1.x, Shell/StaticFiles): /content/packs/* là gói đã cài,
/// /content/* là bài học (content/), còn lại là UI đã build (ui/). Chỉ đọc bên trong các thư mục đó, không liệt kê thư mục.
/// </summary>
public static partial class StaticRoutes
{
    [GeneratedRegex(@"-[A-Za-z0-9_-]{8}\.(js|css|woff2?)$")]
    private static partial Regex Hashed();

    /// <summary>CSP của trang: chỉ chạy script của chính app, ảnh data:/blob:, không nhúng trang ngoài.</summary>
    public const string Csp = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; " +
        "font-src 'self' data:; connect-src 'self'; object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'";

    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8", [".js"] = "text/javascript; charset=utf-8", [".mjs"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8", [".json"] = "application/json; charset=utf-8", [".svg"] = "image/svg+xml", [".png"] = "image/png",
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".webp"] = "image/webp", [".ico"] = "image/x-icon", [".woff"] = "font/woff",
        [".woff2"] = "font/woff2", [".ttf"] = "font/ttf", [".otf"] = "font/otf", [".txt"] = "text/plain; charset=utf-8",
    };

    public sealed record StaticFile(string Full, string Type, string Cache);

    public static StaticFile? Resolve(string urlPath) => Resolve(urlPath, Paths.Ui, Paths.Content, Paths.Packs);

    /// <summary>Thư mục gốc truyền vào để test được; null khi ngoài thư mục cho phép hay không có file.</summary>
    public static StaticFile? Resolve(string urlPath, string ui, string content, string packs)
    {
        var rel = urlPath.TrimStart('/');
        var root = Path.GetFullPath(ui);
        if (rel.StartsWith("content/packs/", StringComparison.Ordinal)) { root = Path.GetFullPath(packs); rel = rel["content/packs/".Length..]; }
        else if (rel.StartsWith("content/", StringComparison.Ordinal)) { root = Path.GetFullPath(content); rel = rel["content/".Length..]; }
        if (rel.Length == 0) rel = "index.html";
        var full = Path.GetFullPath(Path.Combine(root, rel));
        var cmp = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, cmp) || !File.Exists(full)) return null;
        // File Vite build có hash trong tên (name-AbC123xy.js) nên cache lâu được; file khác thì luôn check lại.
        var cache = Hashed().IsMatch(Path.GetFileName(full)) ? "max-age=31536000, immutable" : "no-cache";
        return new StaticFile(full, Types.GetValueOrDefault(Path.GetExtension(full), "application/octet-stream"), cache);
    }
}
