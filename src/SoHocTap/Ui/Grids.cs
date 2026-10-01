using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SoHocTap.Ui;

/// <summary>Một lệnh trong context menu. Primary là lệnh default (double-click, Enter): in đậm, đứng đầu.</summary>
internal sealed record MenuEntry(string Label, Action Run, bool Primary = false, bool Separator = false);

/// <summary>
/// Behavior chung cho DataGrid kiểu Details của Windows: double-click hoặc Enter để mở row, chuột phải/Shift+F10 ra
/// context menu của row, Ctrl+C copy dòng. Column dựng bằng code cho gọn (Text, Right).
/// </summary>
internal static class Grids
{
    public static void Setup<T>(DataGrid g, Action<T>? open = null, Func<T, IEnumerable<MenuEntry>>? menu = null) where T : class
    {
        g.MouseDoubleClick += (_, e) =>
        {
            if (open is not null && ItemAt(e.OriginalSource as DependencyObject) is T row) open(row);
        };
        g.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && open is not null && g.SelectedItem is T row)
            {
                e.Handled = true;
                open(row);
            }
        };
        g.ContextMenuOpening += (_, e) =>
        {
            var row = ItemAt(e.OriginalSource as DependencyObject) as T ?? g.SelectedItem as T;
            if (row is null) { e.Handled = true; return; }
            g.SelectedItem = row;
            var entries = new List<MenuEntry>();
            if (open is not null) entries.Add(new(L.T("common.open"), () => open(row), Primary: true));
            if (menu is not null) entries.AddRange(menu(row).Where(m => !(m.Primary && open is not null && m.Label == L.T("common.open"))));
            if (entries.Count == 0) { e.Handled = true; return; }
            g.ContextMenu = Build(entries);
        };
        // Phải gán ContextMenu (rỗng cũng được) thì WPF mới bắn ContextMenuOpening; menu thật dựng lúc mở.
        g.ContextMenu = new ContextMenu();
    }

    public static ContextMenu Build(IEnumerable<MenuEntry> entries)
    {
        var cm = new ContextMenu();
        foreach (var m in entries)
        {
            if (m.Separator && cm.Items.Count > 0) cm.Items.Add(new Separator());
            var mi = new MenuItem { Header = m.Label, FontWeight = m.Primary ? FontWeights.SemiBold : FontWeights.Normal };
            mi.Click += (_, _) => m.Run();
            cm.Items.Add(mi);
        }
        return cm;
    }

    private static object? ItemAt(DependencyObject? d)
    {
        while (d is not null and not DataGridRow) d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        return (d as DataGridRow)?.Item;
    }

    // ------------------------------------------------------------------ column

    /// <summary>Width gốc lúc thiết kế của column (set lại khi grid hiện lần đầu, xem App.xaml.cs).</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataGridColumn, object> Design = new();

    /// <summary>Nhớ width gốc cho column tự dựng (không qua Text/Right).</summary>
    public static T Remember<T>(T c) where T : DataGridColumn
    {
        Design.AddOrUpdate(c, c.Width);
        return c;
    }

    public static void RestoreWidths(DataGrid g) =>
        // Chạy sau lượt layout hiện tại; tạo DataGridLength mới (chưa có DisplayValue) để DataGrid tính lại từ đầu.
        g.Dispatcher.InvokeAsync(() =>
        {
            foreach (var c in g.Columns)
                if (Design.TryGetValue(c, out var o) && o is DataGridLength w)
                    c.Width = new DataGridLength(w.Value, w.UnitType);
        }, System.Windows.Threading.DispatcherPriority.Background);

    public static DataGridTextColumn Text(string header, string path, double width = double.NaN, bool star = false, string? sortPath = null)
    {
        var c = new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(path),
            ElementStyle = (Style)Application.Current.FindResource("WrapCell"),
            SortMemberPath = sortPath ?? path,
        };
        c.Width = star ? new DataGridLength(1, DataGridLengthUnitType.Star) : double.IsNaN(width) ? DataGridLength.Auto : new DataGridLength(width);
        Design.AddOrUpdate(c, c.Width);
        return c;
    }

    public static DataGridTextColumn Right(string header, string path, double width, string? sortPath = null)
    {
        var c = Text(header, path, width, sortPath: sortPath);
        c.ElementStyle = (Style)Application.Current.FindResource("RightCell");
        c.HeaderStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader),
            (Style)Application.Current.FindResource(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)))
        {
            Setters = { new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Right) },
        };
        return c;
    }

    /// <summary>Danh sách có nhóm (kiểu File Explorer), sắp sẵn theo <paramref name="sort"/>.</summary>
    public static ListCollectionView Grouped<T>(IEnumerable<T> items, string group, string? sort = null, ListSortDirection dir = ListSortDirection.Ascending)
    {
        var v = new ListCollectionView(items.ToList());
        v.GroupDescriptions.Add(new PropertyGroupDescription(group));
        if (sort is not null) v.SortDescriptions.Add(new SortDescription(sort, dir));
        return v;
    }

    public static void Copy(string text, MainWindow? main = null)
    {
        try { Clipboard.SetText(text); main?.Say(L.T("status.copied")); }
        catch (System.Runtime.InteropServices.COMException) { main?.Say(L.T("status.copyFailed")); }
    }
}
