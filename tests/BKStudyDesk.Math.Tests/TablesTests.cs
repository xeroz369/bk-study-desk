using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using BKStudyDesk.Desktop.Views;

namespace BKStudyDesk.Math.Tests;

public class TablesTests
{
    private sealed record Row(string Group, string Name);

    // Dòng liền nhau theo nhóm như tab Sắp tới (đã xếp theo nhóm): mỗi nhóm `size` dòng.
    private static List<Row> Rows(int size, params string[] groups) => [.. groups.SelectMany(g => Enumerable.Range(0, size).Select(i => new Row(g, $"{g} {i}")))];

    // Issue #43: bấm "Hiện buổi học", "Hiện việc đã làm" ở tab Sắp tới làm app tắt. Bảng đổi ItemsSource, dải nhóm cũ được tính lại
    // style; style có đặt SublevelIndent thì giá trị trả về mặc định và DataGrid ném NullReferenceException. TestApp có style như vậy
    // (như Tokens.axaml trước bản sửa): đổi dữ liệu nhiều lần không được ném lỗi, dòng dưới dải nhóm vẫn không thụt lề.
    [AvaloniaFact]
    public void Swapping_grouped_rows_never_throws_and_stays_flat()
    {
        var grid = new DataGrid { AutoGenerateColumns = true, Height = 400 };
        var empty = new TextBlock();
        var window = new Window { Width = 800, Height = 600, Content = new StackPanel { Children = { grid, empty } } };
        Run(grid, empty, window);
    }

    private static void Run(DataGrid grid, TextBlock empty, Window window)
    {
        window.Show();

        foreach (var rows in new[] { Rows(2, "Hôm nay", "Ngày mai"), Rows(4, "Quá hạn", "Hôm nay", "Thứ hai"), Rows(2, "Hôm nay", "Ngày mai"), Rows(0, "x") })
        {
            Tables.Show(grid, empty, Tables.Grouped(rows, nameof(Row.Group)), "Trống");
            window.CaptureRenderedFrame();
            Assert.All(grid.GetVisualDescendants().OfType<DataGridRowGroupHeader>(), h => Assert.Equal(0, h.SublevelIndent));
            Assert.Equal(rows.Count == 0, empty.IsVisible);
        }
    }
}
