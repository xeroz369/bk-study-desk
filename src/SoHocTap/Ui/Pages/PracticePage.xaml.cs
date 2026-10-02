using System.Text.Json.Nodes;
using System.Windows.Controls;
using SoHocTap.Shell;

namespace SoHocTap.Ui.Pages;

public partial class PracticePage : UserControl, IPage
{
    private readonly AppHost _host;
    private string _pending = "luyen-tap";

    internal PracticePage(AppHost host, MainWindow main)
    {
        InitializeComponent();
        _host = host;
        Loaded += async (_, _) =>
        {
            if (Web.CoreWebView2 is not null) return;
            await Web.EnsureCoreWebView2Async(await WebHost.EnvironmentAsync());
            WebUi.Attach(Web.CoreWebView2!, _host.Router, _host);
            // Link trong page trỏ sang phần khác của app (Môn học, Lịch…) thì để WPF chuyển page.
            Web.CoreWebView2!.WebMessageReceived += (_, e) =>
            {
                if (!e.Source.StartsWith($"https://{WebUi.VirtualHost}/", StringComparison.OrdinalIgnoreCase)) return;   // chỉ trang của app
                if (JsonNode.Parse(e.WebMessageAsJson) is not JsonObject m) return;
                // Shortcut của app lúc khung HTML đang giữ focus (WPF không nhận được): Ctrl+số, F5, Alt+←.
                if (m["type"]?.GetValue<string>() == "key")
                {
                    var key = m["key"]?.GetValue<string>() ?? "";
                    if (key == "F5") main.Sync();
                    else if (key == "Alt+Left") main.Back();
                    else if (key.StartsWith("Ctrl+", StringComparison.Ordinal))
                        main.GoIndex(int.TryParse(key[5..], out var n) ? n : 0);
                    return;
                }
                if (m["type"]?.GetValue<string>() != "navigate") return;
                var route = m["route"]?.GetValue<string>() ?? "";
                var parts = route.Split('/', 2);
                main.Go(parts[0] switch
                {
                    "mon" or "tl" => "mon" + (parts[0] == "mon" && parts.Length > 1 ? "/" + parts[1] : ""),
                    "lich" => route,
                    "mybk" => "diem",
                    "cai-dat" => "cai-dat",
                    _ => "hom-nay",
                });
            };
            Web.CoreWebView2.Navigate(WebUi.Url(_pending, Accent()));
        };
    }

    /// <summary>Màu nhấn của theme hiện tại (Fluent: AccentFillColorDefault), dạng #RRGGBB.</summary>
    private string Accent() => TryFindResource("AccentFillColorDefaultBrush") is System.Windows.Media.SolidColorBrush b ? $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}" : "";

    public string Title => L.T("nav.practice");
    public string Subtitle => "";

    /// <summary>arg: path của page Luyện tập (vd "bai/ppt-02", "thi/…", "on-cau-sai").</summary>
    public void Open(string arg)
    {
        _pending = arg.Length > 0 ? arg : "luyen-tap";
        if (Web.CoreWebView2 is { } core) core.Navigate(WebUi.Url(_pending, Accent()));
    }

    public void Refresh() { }
}
