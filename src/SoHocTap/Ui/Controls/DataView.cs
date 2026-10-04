using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace SoHocTap.Ui.Controls;

/// <summary>Nguồn dữ liệu của một bảng, để DataView nói đúng lý do khi trống (đang tải lần đầu, lỗi, chưa đăng nhập).</summary>
internal static class Src
{
    public static readonly (string Name, string Label) Lms = ("lms", "LMS");
    public static readonly (string Name, string Label) Mybk = ("mybk", "MyBK");
}

/// <summary>Bảng đang hiện gì: các dòng, hay một câu thay cho bảng (đang tải lần đầu, trống, lỗi).</summary>
internal enum DataState { Rows, Loading, Empty, Error }

/// <summary>
/// DataGrid kiểu Details kèm trạng thái đang tải, trống, lỗi. Bảng trống thì ẩn hẳn (DataGrid trống trong Fluent co cột về gần 0, header
/// không có chữ) và hiện một câu nói rõ vì sao. Dữ liệu mới về sau một lượt sync thì giữ cột đang sắp, vị trí cuộn, dòng đang chọn
/// và focus bàn phím, để người dùng không bị đẩy về đầu bảng giữa lúc đang đọc.
/// </summary>
public sealed class DataView : UserControl
{
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly SeverityIcon _icon = new() { Severity = Severity.Error, Size = 14, Margin = new Thickness(0, 0, 8, 0), Visibility = Visibility.Collapsed };
    private readonly ProgressBar _bar = new() { IsIndeterminate = false, Width = 120, Height = 4, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
    private readonly StackPanel _status = new() { Margin = new Thickness(8, 4, 8, 4), Visibility = Visibility.Collapsed };
    private (string Path, ListSortDirection Dir)? _userSort;

    public DataView()
    {
        Focusable = false;
        IsTabStop = false;
        _text.SetResourceReference(StyleProperty, "Muted");
        _text.SetResourceReference(TextBlock.FontSizeProperty, "AppFontSizeSub");
        var line = new DockPanel();
        DockPanel.SetDock(_icon, Dock.Left);
        line.Children.Add(_icon);
        line.Children.Add(_text);
        _status.Children.Add(line);
        _status.Children.Add(_bar);
        var root = new Grid();
        root.Children.Add(Grid);
        root.Children.Add(_status);
        Content = root;
        // DataGrid đổi chiều sắp sau event này: đọc chiều cũ để biết chiều mới. Chỉ nhớ khi người dùng tự bấm tiêu đề cột.
        Grid.Sorting += (_, e) => _userSort = (e.Column.SortMemberPath, e.Column.SortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending);
        Grids.NameRows(Grid);
    }

    /// <summary>DataGrid bên trong: page tự thêm cột, group, menu (Grids.Setup).</summary>
    public DataGrid Grid { get; } = new();

    /// <summary>Khóa để nhận ra cùng một dòng sau khi dữ liệu mới về (vd. Id của mốc). Không đặt thì so bằng Equals của record.</summary>
    public Func<object, object?>? KeyOf { get; set; }

    internal DataState State { get; private set; } = DataState.Empty;

    /// <summary>Câu đang hiện thay cho bảng (để test và trình đọc màn hình đọc được).</summary>
    public string StatusText => _text.Text;

    /// <summary>
    /// Hiện dữ liệu. Không có dòng nào thì hiện câu thay cho bảng: nguồn trong <paramref name="sources"/> chưa có dữ liệu thì nói đang tải,
    /// lỗi gì, hay chưa đăng nhập (AppState.NoDataReason); có dữ liệu rồi thì là <paramref name="emptyText"/>.
    /// </summary>
    internal void Show(IEnumerable? items, string emptyText, AppState? state = null, params (string Name, string Label)[] sources)
    {
        if (HasRows(items))
        {
            ShowRows(items!);
            return;
        }
        var (st, text) = (DataState.Empty, emptyText);
        if (state is not null)
            foreach (var (name, label) in sources)
                if (state.NoDataReason(name, label) is { } reason)
                {
                    (st, text) = (state.Syncing(name) ? DataState.Loading : state.Error(name) is not null ? DataState.Error : DataState.Empty, reason);
                    break;
                }
        Grid.ItemsSource = items;
        ShowStatus(st, text);
    }

    /// <summary>Hiện một câu thay cho bảng (vd. đọc danh sách lỗi).</summary>
    internal void ShowStatus(DataState state, string text)
    {
        State = state;
        _text.Text = text;
        _icon.Visibility = state == DataState.Error ? Visibility.Visible : Visibility.Collapsed;
        _bar.IsIndeterminate = state == DataState.Loading;
        _bar.Visibility = state == DataState.Loading ? Visibility.Visible : Visibility.Collapsed;
        _status.Visibility = Visibility.Visible;
        Grid.Visibility = Visibility.Collapsed;
        System.Windows.Automation.AutomationProperties.SetName(this, text);
    }

    private void ShowRows(IEnumerable items)
    {
        // Lần đầu (chưa có dòng nào) thì không có gì để giữ.
        var keep = State == DataState.Rows && Grid.ItemsSource is not null ? Capture() : null;
        Grid.ItemsSource = items;
        ApplyUserSort(items);
        State = DataState.Rows;
        _status.Visibility = Visibility.Collapsed;
        Grid.Visibility = Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetName(this, "");
        if (keep is not null) Restore(keep, items);
    }

    private static bool HasRows(IEnumerable? items) => items switch
    {
        null => false,
        ICollectionView v => !v.IsEmpty,
        ICollection c => c.Count > 0,
        _ => items.GetEnumerator().MoveNext(),
    };

    private sealed record Kept(object? Key, double Vertical, double Horizontal, bool Focused);

    private object? KeyFor(object? item) => item is null ? null : KeyOf is { } k ? k(item) : item;

    private Kept Capture()
    {
        var sv = Scroller();
        return new Kept(KeyFor(Grid.SelectedItem), sv?.VerticalOffset ?? 0, sv?.HorizontalOffset ?? 0, Grid.IsKeyboardFocusWithin);
    }

    private void Restore(Kept keep, IEnumerable items)
    {
        object? match = null;
        if (keep.Key is not null)
            foreach (var x in items)
                if (Equals(KeyFor(x), keep.Key)) { match = x; break; }
        if (match is not null) Grid.SelectedItem = match;
        // Cuộn và focus sau lượt layout: lúc này các dòng mới chưa được dựng.
        Dispatcher.BeginInvoke(() =>
        {
            if (Scroller() is { } sv)
            {
                sv.ScrollToVerticalOffset(keep.Vertical);
                sv.ScrollToHorizontalOffset(keep.Horizontal);
            }
            if (!keep.Focused) return;
            if (match is not null && Grid.ItemContainerGenerator.ContainerFromItem(match) is DataGridRow row && Grid.Columns.Count > 0)
            {
                var column = Grid.Columns.OrderBy(c => c.DisplayIndex).First(c => c.Visibility == Visibility.Visible);
                Grid.CurrentCell = new DataGridCellInfo(match, column);
                if (column.GetCellContent(row)?.Parent is DataGridCell cell) { cell.Focus(); return; }
            }
            Grid.Focus();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Người dùng đã bấm sắp theo cột: áp lại cho dữ liệu mới (page dựng view mới với thứ tự mặc định).</summary>
    private void ApplyUserSort(IEnumerable items)
    {
        if (_userSort is not { } s || Grid.Columns.FirstOrDefault(c => c.SortMemberPath == s.Path) is not { } column) return;
        var view = CollectionViewSource.GetDefaultView(items);
        if (view is null || !view.CanSort) return;
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(s.Path, s.Dir));
        }
        foreach (var c in Grid.Columns) c.SortDirection = c == column ? s.Dir : null;
    }

    private ScrollViewer? Scroller() => Find<ScrollViewer>(Grid);

    private static T? Find<T>(DependencyObject d) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
        {
            var child = VisualTreeHelper.GetChild(d, i);
            if (child is T t) return t;
            if (Find<T>(child) is { } found) return found;
        }
        return null;
    }
}
