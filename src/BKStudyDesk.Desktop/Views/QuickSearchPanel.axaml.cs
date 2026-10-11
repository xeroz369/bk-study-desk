using Avalonia.Controls;
using Avalonia.Input;
using SoHocTap.Presentation;

namespace BKStudyDesk.Desktop.Views;

/// <summary>Ô tìm nhanh: nhận danh sách mục (MainWindow dựng mỗi lần mở), báo mục được chọn qua Picked.</summary>
public partial class QuickSearchPanel : UserControl
{
    private IReadOnlyList<QuickItem> _all = [];

    public QuickSearchPanel() => InitializeComponent();

    public event Action<QuickItem>? Picked;

    public void Reset(IReadOnlyList<QuickItem> all)
    {
        _all = all;
        Query.Text = "";
        Show();
        // Flyout vừa mở chưa nhận focus ngay: đặt focus sau lượt layout.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Query.Focus(), Avalonia.Threading.DispatcherPriority.Background);
    }

    private void Show()
    {
        var hits = QuickSearch.Match(_all, Query.Text ?? "");
        Results.ItemsSource = hits;
        Results.SelectedIndex = hits.Count > 0 ? 0 : -1;
        NoMatch.IsVisible = hits.Count == 0;
    }

    private void OnQuery(object? sender, TextChangedEventArgs e) => Show();

    // Focus ở ô gõ suốt: lên xuống đổi mục chọn trong danh sách, Enter mở mục đang chọn.
    private void OnQueryKey(object? sender, KeyEventArgs e)
    {
        var n = Results.ItemCount;
        switch (e.Key)
        {
            case Key.Down when n > 0: Results.SelectedIndex = (Results.SelectedIndex + 1) % n; break;
            case Key.Up when n > 0: Results.SelectedIndex = (Results.SelectedIndex - 1 + n) % n; break;
            case Key.Enter when Results.SelectedItem is QuickItem i: Picked?.Invoke(i); break;
            default: return;
        }
        if (Results.SelectedItem is { } s) Results.ScrollIntoView(s);
        e.Handled = true;
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (Results.SelectedItem is QuickItem i) Picked?.Invoke(i);
    }
}
