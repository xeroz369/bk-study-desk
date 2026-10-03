using System.Windows;
using System.Windows.Controls;

namespace SoHocTap.Ui;

/// <summary>
/// Dãy tab một hàng (style TabControl trong App.xaml): lăn chuột trên dãy tab thì cuộn ngang, đổi tab thì tab đang chọn tự cuộn vào khung.
/// Thanh cuộn ngang để ẩn cho gọn, nên phải có hai cách này mới tới được tab bị khuất.
/// </summary>
public static class TabScroll
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(TabScroll),
        new PropertyMetadata(false, OnEnabled));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);

    private static void OnEnabled(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv || e.NewValue is not true) return;
        sv.PreviewMouseWheel += (_, w) =>
        {
            if (sv.ScrollableWidth <= 0) return;
            sv.ScrollToHorizontalOffset(sv.HorizontalOffset - w.Delta / 2.0);
            w.Handled = true;
        };
        sv.Loaded += (_, _) =>
        {
            if (sv.TemplatedParent is not TabControl tabs) return;
            tabs.SelectionChanged -= OnSelection;
            tabs.SelectionChanged += OnSelection;
            Reveal(tabs);
        };
    }

    private static void OnSelection(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged của ListBox, ComboBox bên trong nội dung tab cũng nổi lên tới đây.
        if (ReferenceEquals(e.OriginalSource, sender) && sender is TabControl tabs) Reveal(tabs);
    }

    private static void Reveal(TabControl tabs) =>
        tabs.Dispatcher.BeginInvoke(() =>
        {
            if (tabs.ItemContainerGenerator.ContainerFromIndex(tabs.SelectedIndex) is TabItem item) item.BringIntoView();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
}
