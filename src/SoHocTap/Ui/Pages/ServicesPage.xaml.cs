using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using System.Windows.Input;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace SoHocTap.Ui.Pages;

internal sealed record ServiceRow(string Group, string Name, string Url)
{
    /// <summary>
    /// Chỉ hiện tên hệ thống (MyBK, BK-LMS…), không hiện đường dẫn: đỡ lộ endpoint cho người thích mò rồi spam server trường.
    /// </summary>
    public string Site => Uri.TryCreate(Url, UriKind.Absolute, out var u) ? u.Host.Split('.')[0].ToLowerInvariant() switch
    {
        "mybk" => "MyBK",
        "lms" => "BK-LMS",
        "bkpay" => "BKPay",
        "account" => L.T("services.siteAccount"),
        "wiki" => "Wiki",
        var h => h,
    } : "";
}

public partial class ServicesPage : UserControl, IPage
{
    private readonly AppHost _host;
    private List<ServiceRow> _all = [];

    internal ServicesPage(AppHost host, MainWindow main)
    {
        InitializeComponent();
        _host = host;
        List.Columns.Add(Grids.Text(L.T("col.service"), nameof(ServiceRow.Name), 320));
        List.Columns.Add(Grids.Text(L.T("col.site"), nameof(ServiceRow.Site), star: true));
        List.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        Grids.Setup<ServiceRow>(List, s => _host.OpenWeb(s.Url, s.Name), s =>
        [
            new(L.T("services.openBrowser"), () => Process.Start(new ProcessStartInfo(s.Url) { UseShellExecute = true }), Separator: true),
        ]);
        // Ctrl+F: lọc; ↓ từ ô lọc xuống bảng.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { Search.Focus(); Search.SelectAll(); e.Handled = true; }
            else if (e.Key == Key.Down && Search.IsKeyboardFocused && List.Items.Count > 0) { List.SelectedIndex = 0; List.Focus(); e.Handled = true; }
            else if (e.Key == Key.Enter && Search.IsKeyboardFocused && List.Items.Count > 0) { var s = (ServiceRow)List.Items[0]; _host.OpenWeb(s.Url, s.Name); e.Handled = true; }
        };
    }

    public string Title => L.T("nav.services");
    public string Subtitle => L.T("services.subtitle");

    public void Refresh()
    {
        _all = (Config.Node("sources.mybk.services") as JsonArray ?? []).OfType<JsonObject>()
            .Select(o => new ServiceRow(o["group"]?.GetValue<string>() ?? L.T("services.other"), o["name"]?.GetValue<string>() ?? "", o["url"]?.GetValue<string>() ?? ""))
            .Where(s => s.Url.Length > 0).ToList();
        Filter();
    }

    private void OnSearch(object sender, TextChangedEventArgs e) => Filter();

    private void Filter()
    {
        var q = Search.Text.Trim();
        var rows = q.Length == 0 ? _all : _all.Where(s => s.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || s.Group.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
        List.ItemsSource = Grids.Grouped(rows, nameof(ServiceRow.Group));
    }
}
