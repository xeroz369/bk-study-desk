using System.Windows.Controls;

namespace SoHocTap.Ui.SettingsCards;

internal sealed record KeyRow(string Keys, string What);

public partial class KeysSection : UserControl, ISettingsSection
{
    public KeysSection()
    {
        InitializeComponent();
        Keys.Columns.Add(Grids.Flex(L.T("col.key"), nameof(KeyRow.Keys), 1, 160));
        Keys.Columns.Add(Grids.Text(L.T("col.what"), nameof(KeyRow.What), star: true));
        Grids.NameRows(Keys);
        // Tên phím viết bằng chữ (Ctrl+1 đến Ctrl+7, Alt+Mũi tên trái), không dùng gạch nối dài hay ký hiệu mũi tên.
        Keys.ItemsSource = new List<KeyRow>
        {
            new(L.F("settings.keys.range", "Ctrl+1", $"Ctrl+{MainWindow.PageKeys}"), L.T("settings.keys.pages")),
            new("F5", L.T("settings.keys.sync")),
            new(L.T("settings.keys.backKey"), L.T("settings.keys.back")),
            new(L.T("settings.keys.rowsKey"), L.T("settings.keys.rows")),
            new(L.T("settings.keys.enter"), L.T("settings.keys.open")),
            new(L.T("settings.keys.menu"), L.T("settings.keys.menuWhat")),
            new(L.T("settings.keys.click"), L.T("settings.keys.sort")),
            new("Backspace", L.T("settings.keys.up")),
        };
    }

    public string TitleKey => "settings.keys";

    public void Load() { }
}
