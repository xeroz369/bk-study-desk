using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Web;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Trang Luyện tập: khung Svelte có sẵn (ui/, giữ nguyên với bản 1.x) trong trình duyệt nhúng, nạp từ máy chủ cục bộ (LocalServer).
/// Chỉ ở trong máy chủ của app; link ra ngoài mở bằng trình duyệt mặc định. Không có XAML: cả trang là một NativeWebView.
/// Dựng một lần rồi giữ (MainWindow giữ instance) để chuyển trang không mất bài đang làm; nhớ #route để dựng lại thì về đúng chỗ.
/// </summary>
public sealed class PracticeView : UserControl, IFillPage
{
    private readonly LocalServer _server = null!;
    private readonly NativeWebView _web = new();
    private string _route = "";

    public PracticeView() { }

    /// <param name="message">Tin từ khung: phím tắt của app (type key) và route của trang khác (type navigate). Gọi trên UI thread.</param>
    internal PracticeView(LocalServer server, Action<JsonObject> message)
    {
        _server = server;
        Content = _web;
        _web.EnvironmentRequested += (_, e) =>
        {
            e.EnableDevTools = System.Diagnostics.Debugger.IsAttached;
            if (e is WindowsWebView2EnvironmentRequestedEventArgs w) w.UserDataFolder = Paths.WebViewProfile;
            else if (e is GtkWebViewEnvironmentRequestedEventArgs g) g.BaseDataDirectory = Paths.WebViewProfile;
        };
        _web.NewWindowRequested += (_, e) => e.Handled = true;
        _web.NavigationStarted += (_, e) =>
        {
            if (e.Request is not { } u || u.AbsoluteUri.StartsWith(_server.Root, StringComparison.Ordinal) || u.Scheme == "about") return;
            e.Cancel = true;   // trang ngoài: mở bằng trình duyệt, khung vẫn ở trong app
            if (u.Scheme is "http" or "https" && TopLevel.GetTopLevel(this) is { } top) _ = top.Launcher.LaunchUriAsync(u);
        };
        _web.NavigationCompleted += (_, _) =>
        {
            if (_web.Source?.Fragment is { Length: > 1 } f) _route = Uri.UnescapeDataString(f[1..]);
        };
        server.Message += m => Dispatcher.UIThread.Post(() => message(m));
        AttachedToVisualTree += (_, _) => _web.Navigate(new Uri(Url()));
        // Đổi chế độ màu ở Cài đặt khi khung đang mở: đổi ngay, không tải lại trang (không mất bài đang làm).
        ActualThemeVariantChanged += async (_, _) =>
        {
            try { await _web.InvokeScript($"window.applyAppMode?.('{Mode()}')"); }
            catch (Exception e) when (e is InvalidOperationException) { Log.Debug($"Luyện tập: đổi chế độ màu: {e.Message}"); }
        };
    }

    /// <summary>Địa chỉ của khung: embed (bố cục nhúng), host=2 (gửi tin qua máy chủ), chế độ màu, tên app, #route đang xem.</summary>
    private string Url() =>
        $"{_server.Root}index.html?embed=1&host=2&mode={Mode()}&app={Uri.EscapeDataString(AppInfo.Name)}#{_route}";

    /// <summary>Chế độ màu đang dùng của app (khung Luyện tập theo app, không theo hệ điều hành).</summary>
    private string Mode() => ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light ? "light" : "dark";
}
