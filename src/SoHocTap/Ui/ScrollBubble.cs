using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SoHocTap.Ui;

/// <summary>
/// Lăn chuột trên ô nhập (TextBox) hay bảng (DataGrid) nằm trong một trang cuộn: WPF để control con giữ sự kiện dù nó không
/// cuộn được nữa, nên trang đứng im khi con trỏ đi qua ô nhập hay bảng. Ở đây control con hết chỗ cuộn thì chuyển cú lăn
/// cho ScrollViewer gần nhất bên ngoài. Đăng ký một lần cho cả app (App.OnStartup).
/// </summary>
internal static class ScrollBubble
{
    public static void Register()
    {
        foreach (var type in new[] { typeof(TextBox), typeof(DataGrid) })
            EventManager.RegisterClassHandler(type, UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnPreviewWheel));
    }

    private static void OnPreviewWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not DependencyObject element) return;
        var inner = Inner(element);
        if (inner is not null && (e.Delta > 0 ? inner.VerticalOffset > 0 : inner.VerticalOffset < inner.ScrollableHeight)) return;
        if (Outer(element) is not { } outer) return;
        e.Handled = true;
        outer.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = sender });
    }

    /// <summary>ScrollViewer riêng của control (template của TextBox, DataGrid).</summary>
    private static ScrollViewer? Inner(DependencyObject d)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
        {
            var c = VisualTreeHelper.GetChild(d, i);
            if (c is ScrollViewer sv) return sv;
            if (Inner(c) is { } found) return found;
        }
        return null;
    }

    /// <summary>ScrollViewer gần nhất bao ngoài control.</summary>
    private static ScrollViewer? Outer(DependencyObject d)
    {
        for (var p = VisualTreeHelper.GetParent(d); p is not null; p = VisualTreeHelper.GetParent(p))
            if (p is ScrollViewer sv) return sv;
        return null;
    }
}
