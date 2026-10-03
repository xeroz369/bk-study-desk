using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Wpf;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace SoHocTap.Ui.Pages;

/// <summary>
/// Trang Luyện tập: khung HTML (src/ui) trong một WebView2. WebView chỉ sống khi cần: dựng lúc trang hiện, đóng (Dispose, tắt renderer)
/// khi trang ẩn quá app.practiceReleaseMinutes phút (mặc định 3, 0 = không tự đóng) hoặc khi app xuống khay. Mở lại thì dựng lại và về
/// đúng trang Luyện tập đang xem (giữ #hash). Trước khi đóng, cho khung HTML ghi nốt kết quả (save() gom 300 ms rồi mới gửi /api/state).
/// </summary>
public partial class PracticePage : UserControl, IPage
{
    private readonly AppHost _host;
    private readonly MainWindow _main;
    private readonly DispatcherTimer _idle = new();
    private string _pending = "luyen-tap";
    private WebView2? _web;
    /// <summary>Lần dựng WebView đang chạy: rời trang rồi quay lại giữa chừng thì chờ lần này, không dựng cái thứ hai.</summary>
    private Task? _init;
    /// <summary>Lần đóng WebView đang chạy (đang chờ lưu kết quả): quay lại trang thì chờ nó xong rồi mới quyết định dựng lại.</summary>
    private Task? _release;

    internal PracticePage(AppHost host, MainWindow main)
    {
        InitializeComponent();
        _host = host;
        _main = main;
        _idle.Tick += (_, _) => { _idle.Stop(); _ = ReleaseAsync(); };
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                _idle.Stop();
                _ = EnsureAsync();
            }
            else if (_web is not null && ReleaseMinutes > 0)
            {
                _idle.Interval = TimeSpan.FromMinutes(ReleaseMinutes);
                _idle.Start();
            }
        };
    }

    private static double ReleaseMinutes => Config.Int("app.practiceReleaseMinutes", 3);

    /// <summary>Có WebView và đang dựng/đã dựng xong (cho test và log).</summary>
    internal bool HasWebView => _web is not null;

    private async Task EnsureAsync()
    {
        if (_release is { IsCompleted: false } r) await r;
        if (!IsVisible) return;
        if (_web is null) _init = null;   // lần dựng trước đã hỏng hoặc đã đóng
        _init ??= CreateAsync();
        await _init;
    }

    private async Task CreateAsync()
    {
        var web = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.Transparent, Margin = new Thickness(-4, 0, -4, 0) };
        // Nền trong suốt để khung HTML vẽ thẳng lên nền window; card bên trong dùng cùng màu card Fluent (app.css, :root.embed).
        System.Windows.Automation.AutomationProperties.SetName(web, L.T("nav.practice"));
        _web = web;
        Host.Children.Add(web);
        try
        {
            await web.EnsureCoreWebView2Async(await WebHost.EnvironmentAsync());
        }
        catch (Exception x) when (x is ObjectDisposedException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // Bị đóng giữa chừng (hiếm), hoặc WebView2 Runtime lỗi: lần mở trang sau thử lại.
            Log.Warn("Luyện tập: không dựng được WebView: " + x.Message);
            if (_web == web) DisposeWeb(web);
            return;
        }
        if (_web != web) return;
        var core = web.CoreWebView2;
        WebUi.Attach(core, _host.Router, _host);
        // Nhớ trang Luyện tập đang xem (#hash) để dựng lại WebView thì về đúng chỗ.
        core.SourceChanged += (_, _) =>
        {
            var src = core.Source ?? "";
            var i = src.IndexOf('#', StringComparison.Ordinal);
            if (i >= 0 && i + 1 < src.Length) _pending = Uri.UnescapeDataString(src[(i + 1)..]);
        };
        // Link trong page trỏ sang phần khác của app (Môn học, Lịch...) thì để WPF chuyển page.
        core.WebMessageReceived += (_, e) =>
        {
            if (!e.Source.StartsWith($"https://{WebUi.VirtualHost}/", StringComparison.OrdinalIgnoreCase)) return;   // chỉ trang của app
            if (JsonNode.Parse(e.WebMessageAsJson) is not JsonObject m) return;
            // Shortcut của app lúc khung HTML đang giữ focus (WPF không nhận được): Ctrl+số, F5, Alt+Left.
            if (m["type"]?.GetValue<string>() == "key")
            {
                var key = m["key"]?.GetValue<string>() ?? "";
                if (key == "F5") _main.Sync();
                else if (key == "Alt+Left") _main.Back();
                else if (key.StartsWith("Ctrl+", StringComparison.Ordinal))
                    _main.GoIndex(int.TryParse(key[5..], out var n) ? n : 0);
                return;
            }
            // Route của khung HTML ("mon/...", "tl", "lich/thi", "mybk", "cai-dat"...): PageRegistry nhận ra, lạ thì về Hôm nay.
            if (m["type"]?.GetValue<string>() == "navigate") _main.Go(m["route"]?.GetValue<string>() ?? "");
        };
        core.Navigate(WebUi.Url(_pending, Accent()));
    }

    /// <summary>App xuống khay: đóng WebView ngay (cửa sổ ẩn nên trang cũng ẩn).</summary>
    public void Sleep()
    {
        _idle.Stop();
        _ = ReleaseAsync();
    }

    private Task ReleaseAsync() => _release is { IsCompleted: false } r ? r : _release = ReleaseCoreAsync();

    private async Task ReleaseCoreAsync()
    {
        if (_init is { } init) await init;
        if (_web is not { } web || IsVisible) return;
        if (web.CoreWebView2 is { } core)
        {
            // Ghi nốt kết quả đang chờ: gọi flushPending() (window.Progress của progress.svelte.ts), không thì chờ hết 300 ms debounce của save()
            // và request /api/state. Chờ cả hai trường hợp: ExecuteScriptAsync không đợi Promise của flush().
            try { await core.ExecuteScriptAsync("window.Progress?.flushPending?.()"); }
            catch (Exception x) when (x is InvalidOperationException or System.Runtime.InteropServices.COMException) { Log.Warn("Luyện tập: flush lỗi: " + x.Message); }
            await Task.Delay(500);
        }
        if (IsVisible || _web != web) return;   // người dùng quay lại trang trong lúc chờ: giữ WebView
        DisposeWeb(web);
        Log.Info("Luyện tập: đã đóng WebView (trang ẩn hoặc app xuống khay)");
    }

    private void DisposeWeb(WebView2 web)
    {
        _web = null;
        _init = null;
        Host.Children.Remove(web);
        web.Dispose();
    }

    /// <summary>Màu nhấn của theme hiện tại (Fluent: AccentFillColorDefault), dạng #RRGGBB.</summary>
    private string Accent() => TryFindResource("AccentFillColorDefaultBrush") is System.Windows.Media.SolidColorBrush b ? $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}" : "";

    public string Title => L.T("nav.practice");
    public string Subtitle => "";

    /// <summary>arg: path của page Luyện tập (vd "bai/ppt-02", "thi/...", "on-cau-sai").</summary>
    public void Open(string arg)
    {
        _pending = arg.Length > 0 ? arg : "luyen-tap";
        if (_web?.CoreWebView2 is { } core) core.Navigate(WebUi.Url(_pending, Accent()));
    }

    public void Refresh() { }
}
