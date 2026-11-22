using System.Collections;
using Avalonia.Collections;
using Avalonia.Controls;

namespace BKStudyDesk.Desktop.Views;

internal static class Tables
{
    public static DataGridCollectionView Grouped(IEnumerable rows, string path)
    {
        var view = new DataGridCollectionView(rows);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(path));
        return view;
    }

    public static void Show(DataGrid grid, TextBlock empty, IEnumerable rows, string emptyText)
    {
        var any = rows.Cast<object>().Any();
        grid.LoadingRowGroup -= FlatGroup;
        grid.LoadingRowGroup += FlatGroup;
        grid.ItemsSource = rows;
        if (rows is DataGridCollectionView view) view.MoveCurrentToPosition(-1);
        grid.SelectedIndex = -1;
        grid.IsVisible = any;
        empty.Text = emptyText;
        empty.IsVisible = !any;
    }

    private static void FlatGroup(object? sender, DataGridRowGroupHeaderEventArgs e) => e.RowGroupHeader.SublevelIndent = 0;
}
