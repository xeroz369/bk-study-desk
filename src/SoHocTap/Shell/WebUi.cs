using System.Text;
using Microsoft.Web.WebView2.Core;
using SoHocTap.Api;
using SoHocTap.Core;

namespace SoHocTap.Shell;

/// <summary>
/// Gắn một WebView2 vào trang HTML của app (https://&lt;app.virtualHost&gt;/): serve file trong ui/ và content/, còn request /api/* thì
/// chuyển vào <see cref="ApiRouter"/>, không mở port mạng nào. Chỉ khung Luyện tập dùng (bài học có công thức cần MathJax).
/// Tắt các hành vi kiểu trình duyệt: shortcut, zoom, menu chuột phải (trừ Cắt/Sao chép/Dán), autofill, thả file.
/// </summary>
internal static class WebUi
{
    // Tên host ảo phải là tên không ai đăng ký được (RFC 6761: .example/.invalid/.test), theo khuyến nghị của
    // SetVirtualHostNameToFolderMapping: nếu request lỡ không được app trả lời, nó không tới máy chủ của ai trên Internet.
    public static string VirtualHost => Config.Str("app.virtualHost", "sohoc.example");
    /// <summary>URL của khung HTML. accent là màu accent hiện tại của Windows (#RRGGBB) để khung đồng màu với phần WPF.</summary>
    public static string Url(string hash, string accent = "") =>
        $"https://{VirtualHost}/index.html?embed=1&app={Uri.EscapeDataString(AppInfo.Name)}{(accent.Length > 0 ? "&accent=" + Uri.EscapeDataString(accent) : "")}#{hash}";

    private static readonly HashSet<string> EditCommands = ["cut", "copy", "paste", "selectAll"];

    public static void Attach(CoreWebView2 core, ApiRouter router, IShellActions shell)
    {
        core.AddWebResourceRequestedFilter($"https://{VirtualHost}/*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += async (_, e) => await RespondAsync(e, router);
        core.NewWindowRequested += (_, e) => { e.Handled = true; shell.OpenWeb(e.Uri, ""); };
        // Khung Luyện tập chỉ ở trong host của app; link ngoài mở bằng trình duyệt (OpenWeb chỉ nhận http/https).
        core.NavigationStarting += (_, e) =>
        {
            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) && u.Host.Equals(VirtualHost, StringComparison.OrdinalIgnoreCase)) return;
            e.Cancel = true;
            shell.OpenWeb(e.Uri, "");
        };
        core.ContextMenuRequested += (_, e) =>
        {
            var items = e.MenuItems;
            for (var i = items.Count - 1; i >= 0; i--)
                if (!EditCommands.Contains(items[i].Name)) items.RemoveAt(i);
            if (items.Count == 0) e.Handled = true;
        };
        var s = core.Settings;
        s.AreDevToolsEnabled = System.Diagnostics.Debugger.IsAttached;
        s.IsStatusBarEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;
        s.IsZoomControlEnabled = false;
        s.IsPinchZoomEnabled = false;
        s.IsSwipeNavigationEnabled = false;
        s.IsGeneralAutofillEnabled = false;
        s.IsPasswordAutosaveEnabled = false;
        // Web message bật: khung Luyện tập gửi phím tắt của app (Ctrl+số, F5, Alt+Left) và lệnh mở trang WPF qua postMessage,
        // PracticePage chỉ nhận tin từ host của app. Không dùng host object, hộp thoại kiểu trình duyệt; không cấp quyền nào.
        s.IsWebMessageEnabled = true;
        s.AreHostObjectsAllowed = false;
        s.AreDefaultScriptDialogsEnabled = false;
        // Tắt hộp thoại mặc định thì confirm() luôn trả false (Gỡ quiz, Xóa câu không chạy được). Thay bằng hộp thoại Windows.
        // Mở hộp thoại sau khi handler trả về (deferral) để không lồng vòng lặp message trong event của WebView2.
        core.ScriptDialogOpening += (_, e) =>
        {
            if (e.Kind is not (CoreWebView2ScriptDialogKind.Confirm or CoreWebView2ScriptDialogKind.Alert)) return;
            var deferral = e.GetDeferral();
            var app = System.Windows.Application.Current;
            app.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var confirm = e.Kind == CoreWebView2ScriptDialogKind.Confirm;
                    var owner = app.MainWindow;
                    var r = System.Windows.MessageBox.Show(owner, e.Message, AppInfo.Name,
                        confirm ? System.Windows.MessageBoxButton.OKCancel : System.Windows.MessageBoxButton.OK,
                        confirm ? System.Windows.MessageBoxImage.Question : System.Windows.MessageBoxImage.Information);
                    if (r == System.Windows.MessageBoxResult.OK) e.Accept();
                }
                finally { deferral.Complete(); }
            });
        };
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
    }

    private static async Task RespondAsync(CoreWebView2WebResourceRequestedEventArgs e, ApiRouter router)
    {
        using var deferral = e.GetDeferral();
        try
        {
            var uri = new Uri(e.Request.Uri);
            var env = await WebHost.EnvironmentAsync();
            if (!uri.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal))
            {
                e.Response = StaticFiles.Respond(env, Uri.UnescapeDataString(uri.AbsolutePath));
                return;
            }
            // Chỉ trang của app gọi được /api: phải có header X-App (trang khác gửi header lạ thì trình duyệt chặn bằng preflight).
            if (!e.Request.Headers.Contains("X-App"))
            {
                e.Response = env.CreateWebResourceResponse(null, 403, "Forbidden", "");
                return;
            }
            string? body = null;
            if (e.Request.Content is { } stream)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                body = await reader.ReadToEndAsync();
            }
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var q = query.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => query[k] ?? "");
            // Đọc hết e.Request trên UI thread; chỉ có ApiRouter chạy ở background thread.
            var request = new ApiRequest(e.Request.Method, Uri.UnescapeDataString(uri.AbsolutePath), q, body);
            var res = await Task.Run(() => router.HandleAsync(request, CancellationToken.None));
            e.Response = env.CreateWebResourceResponse(new MemoryStream(res.Body), res.Status, res.Status == 200 ? "OK" : "Error",
                $"Content-Type: {res.ContentType}\nCache-Control: no-store");
        }
        catch (Exception ex)
        {
            // Luôn trả lời ở đây: request không có Response thì WebView2 gửi tiếp ra mạng thật.
            Log.Error("Request " + Log.Where(e.Request.Uri), ex);
            var env = await WebHost.EnvironmentAsync();
            e.Response = env.CreateWebResourceResponse(null, 500, "Error", "Cache-Control: no-store");
        }
    }
}
