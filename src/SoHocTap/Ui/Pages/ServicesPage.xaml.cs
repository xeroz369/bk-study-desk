using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using System.Windows.Input;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Ui.Controls;

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
        var list = List.Grid;
        list.Columns.Add(Grids.Text(L.T("col.service"), nameof(ServiceRow.Name), star: true));
        list.Columns.Add(Grids.Flex(L.T("col.site"), nameof(ServiceRow.Site), 1, 100));
        list.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        List.KeyOf = o => ((ServiceRow)o).Url;
        Grids.Setup<ServiceRow>(list, s => _host.OpenWeb(s.Url, s.Name), s =>
        [
            new(L.T("services.openBrowser"), () => Links.Open(s.Url), Separator: true),
        ]);
        // Ctrl+F: lọc; ↓ từ ô lọc xuống bảng.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { Search.Focus(); Search.SelectAll(); e.Handled = true; }
            else if (e.Key == Key.Down && Search.IsKeyboardFocused && list.Items.Count > 0) { list.SelectedIndex = 0; list.Focus(); e.Handled = true; }
            else if (e.Key == Key.Enter && Search.IsKeyboardFocused && list.Items.Count > 0) { var s = (ServiceRow)list.Items[0]; _host.OpenWeb(s.Url, s.Name); e.Handled = true; }
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
        List.Show(Grids.Grouped(rows, nameof(ServiceRow.Group)), L.T(q.Length == 0 ? "services.empty" : "services.noMatch"));
    }
}
