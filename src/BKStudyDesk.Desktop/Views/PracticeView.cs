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
            if (u.Scheme is "http" or "https") _ = Files.Links.OpenAsync(this, u.AbsoluteUri);
        };
        _web.NavigationCompleted += (_, _) =>
        {
            if (_web.Source?.Fragment is { Length: > 1 } f) _route = Uri.UnescapeDataString(f[1..]);
        };
        server.Message += m => Dispatcher.UIThread.Post(() => message(m));
        AttachedToVisualTree += (_, _) => _web.Navigate(new Uri(Url()));
        // Đổi chế độ màu, màu nhấn, phông ở Cài đặt khi khung đang mở: đổi ngay, không tải lại trang (không mất bài đang làm).
        ActualThemeVariantChanged += (_, _) => Run($"window.applyAppMode?.('{Mode()}')");
        App.AppearanceChanged += () => Run(
            $"window.applyAppAccent?.({JsonValue.Create(App.AccentHex).ToJsonString()});window.applyAppFont?.({JsonValue.Create(App.Font.Family).ToJsonString()},{FontScale()})");
    }

    /// <summary>
    /// Rời trang thì NativeWebView bị hủy ngay (nhả bộ nhớ, không đợi như 1.x): trước đó cho khung ghi nốt kết quả đang gom (save() chờ
    /// 300 ms rồi mới gửi /api/state). InvokeScript không chờ promise trên mọi nền tảng, nên hỏi lại cờ mỗi 50 ms, tối đa app.practiceFlushMs.
    /// </summary>
    public async Task FlushAsync()
    {
        try
        {
            await _web.InvokeScript("window.__bkFlushed = false; Promise.resolve(window.Progress?.flushPending?.()).finally(() => { window.__bkFlushed = true; }); 0");
            var until = DateTime.UtcNow + TimeSpan.FromMilliseconds(Config.Int("app.practiceFlushMs", 2000));
            while (DateTime.UtcNow < until && await _web.InvokeScript("window.__bkFlushed === true") != "true") await Task.Delay(50);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Log.Debug($"Luyện tập: ghi nốt kết quả: {e.Message}"); }
    }

    private async void Run(string script)
    {
        try { await _web.InvokeScript(script); }
        catch (Exception e) when (e is InvalidOperationException) { Log.Debug($"Luyện tập: đổi giao diện: {e.Message}"); }
    }

    /// <summary>Địa chỉ của khung: embed (bố cục nhúng), host=2 (gửi tin qua máy chủ), chế độ màu, màu nhấn, phông, tên app, #route đang xem.</summary>
    private string Url() =>
        $"{_server.Root}index.html?embed=1&host=2&mode={Mode()}&accent={Uri.EscapeDataString(App.AccentHex)}" +
        $"&font={Uri.EscapeDataString(App.Font.Family)}&fs={FontScale()}&app={Uri.EscapeDataString(AppInfo.Name)}#{_route}";

    private static string FontScale() =>
        App.Font.WebScale(SoHocTap.Presentation.FontConfig.DesignBodySize).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Chế độ màu đang dùng của app (khung Luyện tập theo app, không theo hệ điều hành).</summary>
    private string Mode() => ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light ? "light" : "dark";
}
