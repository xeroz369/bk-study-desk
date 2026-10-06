using System.Collections;
using Avalonia.Collections;
using Avalonia.Controls;

namespace BKStudyDesk.Desktop.Views;

/// <summary>Gán dữ liệu cho bảng (DataGrid) của các trang: một chỗ cho bảng có nhóm, bảng rỗng thì hiện dòng chữ thay bảng.</summary>
internal static class Tables
{
    /// <summary>Bảng nhóm theo một thuộc tính của dòng (dải nhóm của DataGrid).</summary>
    public static DataGridCollectionView Grouped(IEnumerable rows, string path)
    {
        var view = new DataGridCollectionView(rows);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(path));
        return view;
    }

    /// <summary>Hiện <paramref name="rows"/> trên bảng; không có dòng nào thì ẩn bảng, hiện <paramref name="emptyText"/>.</summary>
    public static void Show(DataGrid grid, TextBlock empty, IEnumerable rows, string emptyText)
    {
        var any = rows.Cast<object>().Any();
        grid.ItemsSource = rows;
        // DataGridCollectionView đặt CurrentItem là dòng đầu, DataGrid theo đó tô sẵn dòng đầu như đã chọn: bỏ để bảng mở ra không có dòng chọn.
        if (rows is DataGridCollectionView view) view.MoveCurrentToPosition(-1);
        grid.SelectedIndex = -1;
        grid.IsVisible = any;
        empty.Text = emptyText;
        empty.IsVisible = !any;
    }
}
