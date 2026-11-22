using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace BKStudyDesk.Desktop.Views;

internal static class RowMenus
{
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
            if (source?.FindAncestorOfType<DataGridColumnHeader>(includeSelf: true) is null
                && source?.FindAncestorOfType<DataGridRowGroupHeader>(includeSelf: true) is null
                && source?.FindAncestorOfType<Avalonia.Controls.Primitives.ScrollBar>(includeSelf: true) is null)
                grid.SelectedItem = null;
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
    }
}
