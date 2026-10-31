using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Menu chuột phải cho bảng (DataGrid), dựng lại mỗi lần mở theo dòng đang chọn.
/// MenuFlyout rỗng thì Avalonia không mở: các menu khai báo rỗng trong XAML rồi điền ở Opening chưa từng hiện (thấy 31/10/2026),
/// nên menu ở đây có sẵn một mục giữ chỗ, hàm điền xóa đi rồi thêm mục thật. Điền xong mà không có mục nào thì đóng ngay.
/// Chuột phải chọn luôn dòng dưới con trỏ: menu làm việc với đúng dòng vừa bấm, không phải dòng đang chọn trước đó.
/// </summary>
internal static class RowMenus
{
    /// <param name="fill">Điền menu (xóa mục cũ trước); sender là MenuFlyout, như handler Opening trước đây.</param>
    public static void Attach(DataGrid grid, EventHandler fill)
    {
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuItem { Header = "..." });
        menu.Opening += (_, e) =>
        {
            fill(menu, e);
            if (menu.Items.Count == 0) Dispatcher.UIThread.Post(menu.Hide);
        };
        grid.ContextFlyout = menu;
        grid.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(grid).Properties.IsRightButtonPressed) return;
            var source = e.Source as Control;
            if (source?.FindAncestorOfType<DataGridRow>(includeSelf: true) is { } row) { grid.SelectedItem = row.DataContext; return; }
            // Tiêu đề cột, dải nhóm, thanh cuộn: không đổi dòng đang chọn. Chỉ chỗ trống của bảng mới bỏ chọn (menu của chỗ trống).
            if (source?.FindAncestorOfType<DataGridColumnHeader>(includeSelf: true) is null
                && source?.FindAncestorOfType<DataGridRowGroupHeader>(includeSelf: true) is null
                && source?.FindAncestorOfType<Avalonia.Controls.Primitives.ScrollBar>(includeSelf: true) is null)
                grid.SelectedItem = null;
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
    }
}
