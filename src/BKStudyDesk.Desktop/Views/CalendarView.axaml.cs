using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>Chỉ nối dòng của CalendarPresenter vào bảng, nhóm theo cột Group, mở link khi bấm đúp.</summary>
public partial class CalendarView : UserControl, IFillPage
{
    public CalendarView() => InitializeComponent();

    internal CalendarView(AppState state) : this()
    {
        var rows = CalendarPresenter.Build(state.Timeline, Format.Now);
        var view = new DataGridCollectionView(rows);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(CalendarRow.Group)));
        Grid.ItemsSource = view;
        CountText.Text = $"{rows.Count} mốc";
        Grid.IsVisible = rows.Count > 0;
        EmptyText.IsVisible = rows.Count == 0;
        EmptyText.Text = state.Lms is null && state.Mybk is null ? "Chưa có dữ liệu. Đăng nhập để bắt đầu." : "Không có mốc nào sắp tới.";
    }

    private void OnOpen(object? sender, TappedEventArgs e)
    {
        if (Grid.SelectedItem is CalendarRow { Url: { } url } && Uri.TryCreate(url, UriKind.Absolute, out var uri) && TopLevel.GetTopLevel(this) is { } top)
            _ = top.Launcher.LaunchUriAsync(uri);
    }
}
