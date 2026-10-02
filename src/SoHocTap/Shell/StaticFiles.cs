using Microsoft.Web.WebView2.Core;
using SoHocTap.Core;

namespace SoHocTap.Shell;

/// <summary>Serve file UI (ui/) và bài học (content/) cho WebView2. Chỉ đọc bên trong hai folder đó, không list folder.</summary>
internal static partial class StaticFiles
{
    [System.Text.RegularExpressions.GeneratedRegex(@"-[A-Za-z0-9_-]{8}\.(js|css|woff2?)$")]
    private static partial System.Text.RegularExpressions.Regex Hashed();

    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".otf"] = "font/otf",
        [".txt"] = "text/plain; charset=utf-8",
    };

    public static CoreWebView2WebResourceResponse Respond(CoreWebView2Environment env, string urlPath)
    {
        // /content/* là bài học (content/ ở gốc app, sửa xong không cần build lại); còn lại là UI đã build (ui/).
        var rel = urlPath.TrimStart('/');
        var root = Path.GetFullPath(Paths.Ui);
        if (rel.StartsWith("content/packs/", StringComparison.Ordinal))
        {
            root = Path.GetFullPath(Paths.Packs);
            rel = rel["content/packs/".Length..];
        }
        else if (rel.StartsWith("content/", StringComparison.Ordinal))
        {
            root = Path.GetFullPath(Paths.Content);
            rel = rel["content/".Length..];
        }
        if (rel.Length == 0) rel = "index.html";
        var full = Path.GetFullPath(Path.Combine(root, rel));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            return env.CreateWebResourceResponse(null, 404, "Not Found", "");
        var type = Types.GetValueOrDefault(Path.GetExtension(full), "application/octet-stream");
        // File Vite build có hash trong tên (name-AbC123xy.js) nên cache lâu được; file khác thì luôn check lại.
        var cache = Hashed().IsMatch(Path.GetFileName(full)) ? "max-age=31536000, immutable" : "no-cache";
        // Đọc hẳn vào RAM: WebView2 giữ stream khá lâu, file đang mở thì không ghi đè được (cài lại gói cùng id báo Access denied).
        // CSP cho trang: chỉ chạy script của chính app, ảnh data:/blob:, không nhúng trang ngoài.
        var csp = type.StartsWith("text/html", StringComparison.Ordinal)
            ? "\nContent-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; " +
              "font-src 'self' data:; connect-src 'self'; object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'"
            : "";
        return env.CreateWebResourceResponse(new MemoryStream(File.ReadAllBytes(full)), 200, "OK", $"Content-Type: {type}\nCache-Control: {cache}{csp}");
    }
}
