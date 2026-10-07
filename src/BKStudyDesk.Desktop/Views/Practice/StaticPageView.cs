using Avalonia;
using Avalonia.Controls;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views.Practice;

/// <summary>Trang tĩnh của content/pages (ví dụ lộ trình ôn): tiêu đề, nút Quay lại, một thẻ nội dung. Không có câu hỏi.</summary>
public sealed class StaticPageView : UserControl
{
    public event Action? BackRequested;

    internal StaticPageView(string title, string? html)
    {
        var back = new Button { Content = L.T("practice.back"), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        back.Click += (_, _) => BackRequested?.Invoke();
        DockPanel.SetDock(back, Dock.Right);
        var head = new DockPanel { Children = { back, new TextBlock { Classes = { "title" }, Text = title, TextWrapping = Avalonia.Media.TextWrapping.Wrap } } };
        Control body = html is null
            ? new TextBlock { Classes = { "empty" }, Text = L.T("practice.page.empty") }
            : new Border { Padding = new Thickness(16), Child = ContentRenderer.Render(html) };
        var column = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star) { MaxWidth = (double)Application.Current!.FindResource("ReadingMaxWidth")! } } };
        column.Children.Add(new StackPanel { Spacing = 24, Children = { head, new Border { Classes = { "card" }, Child = body } } });
        Content = column;
    }
}
