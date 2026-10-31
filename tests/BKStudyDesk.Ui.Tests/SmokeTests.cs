using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using BKStudyDesk.Desktop.Views;
using SoHocTap.Core;
using SoHocTap.Presentation;
using SoHocTap.Sources;
using SoHocTap.Ui;

namespace BKStudyDesk.Ui.Tests;

public class SmokeTests
{
    [Fact]
    public void DataFolderIsIsolated()
    {
        Assert.StartsWith(Path.GetFullPath(AppContext.BaseDirectory), Path.GetFullPath(Paths.Data), StringComparison.OrdinalIgnoreCase);
    }

    private static ListBox ListOf(InboxView v) => v.GetVisualDescendants().OfType<ListBox>().First();

    private static InboxView Inbox(out Window w)
    {
        Sample.WriteData();
        var v = new InboxView();
        w = Ui.Show(v);
        v.Reload(Sample.Lms());
        Ui.Pump(w);
        return v;
    }

    [AvaloniaFact]
    public void Inbox_ShowsRowsAndDetail()
    {
        var v = Inbox(out var w);
        var list = ListOf(v);
        Assert.Equal(4, list.ItemCount);   // 2 hội thoại, 1 thông báo môn, 1 thông báo LMS
        Assert.True(Ui.HasText(v, "Nhom hoc mau"));
        Assert.True(Ui.HasText(v, "Tin mau so 130"));
        w.Close();
    }

    [AvaloniaFact]
    public void Inbox_SwitchSelection_AndOlderButton()
    {
        var v = Inbox(out var w);
        var list = ListOf(v);
        var older = v.GetVisualDescendants().OfType<Button>().First(b => b.Content is string s && s.Contains("70", StringComparison.Ordinal));
        older.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Ui.Pump(w);
        Assert.True(Ui.HasText(v, "Tin mau so 1"), "bấm tin cũ hơn phải dựng thêm tin");
        foreach (var i in Enumerable.Range(0, list.ItemCount).Concat(Enumerable.Range(0, list.ItemCount).Reverse()))
        {
            list.SelectedIndex = i;
            Ui.Pump(w);
        }
        Assert.NotNull(list.SelectedItem);
        w.Close();
    }

    [AvaloniaFact]
    public async Task Inbox_FilterAndKind()
    {
        var v = Inbox(out var w);
        var list = ListOf(v);
        var kind = v.GetVisualDescendants().OfType<ComboBox>().First();
        kind.SelectedIndex = 2;   // chỉ thông báo môn
        Ui.Pump(w);
        Assert.Equal(1, list.ItemCount);
        kind.SelectedIndex = 3;
        Ui.Pump(w);
        Assert.Equal(1, list.ItemCount);
        Assert.True(Ui.HasText(v, "LMS bao tri"));
        kind.SelectedIndex = 1;
        Ui.Pump(w);
        Assert.Equal(2, list.ItemCount);
        // Chữ lọc: bộ lọc chạy sau khi ngừng gõ (DispatcherTimer 250 ms), nên đặt chữ rồi chờ timer.
        kind.SelectedIndex = 0;
        var box = v.GetVisualDescendants().OfType<TextBox>().First(t => !string.IsNullOrEmpty(t.PlaceholderText));
        box.Text = "bao tri";
        for (var i = 0; i < 40 && list.ItemCount != 1; i++) { await Task.Delay(50); Ui.Pump(w); }
        Assert.Equal(1, list.ItemCount);
        v.Select("msg:10");   // bỏ lọc, chọn đúng hội thoại
        Ui.Pump(w);
        Assert.Equal(4, list.ItemCount);
        w.Close();
    }

    [AvaloniaFact]
    public void Inbox_NullDataTemplateAndRetemplate_DoNotThrow()
    {
        var v = Inbox(out var w);
        var list = ListOf(v);
        // Template của danh sách phải chịu được null (Avalonia gọi với null khi dọn ô).
        var template = Assert.IsAssignableFrom<IDataTemplate>(list.ItemTemplate);
        Assert.NotNull(template.Build(null));
        // Đổi danh sách, đổi cỡ cửa sổ, đổi template: dựng lại toàn bộ ô.
        var items = list.ItemsSource;
        list.ItemsSource = null;
        Ui.Pump(w);
        list.ItemsSource = items;
        Ui.Pump(w);
        foreach (var (width, height) in new[] { (500d, 400d), (1400d, 900d), (300d, 200d), (1100d, 700d) })
        {
            w.Width = width;
            w.Height = height;
            Ui.Pump(w);
        }
        var original = list.ItemTemplate;
        list.ItemTemplate = new FuncDataTemplate<object>((o, _) => new TextBlock { Text = o?.ToString() ?? "" });
        Ui.Pump(w);
        list.ItemTemplate = original;
        Ui.Pump(w);
        list.SelectedItem = null;
        Ui.Pump(w);
        Assert.True(Ui.HasText(v, "Nhom hoc mau"));
        w.Close();
    }

    [AvaloniaFact]
    public void Inbox_EmptyData_Builds()
    {
        var v = new InboxView();
        var w = Ui.Show(v);
        v.Reload(null);
        Ui.Pump(w);
        w.Close();
    }

    [AvaloniaFact]
    public void ReaderBody_ShowsReading()
    {
        var host = new Border();
        var r = SubjectsPresenter.ReadingOf(Sample.Lms().Announcements![0]);
        host.Child = ReaderWindow.Body(r, host);
        var w = Ui.Show(host);
        Assert.True(Ui.HasText(host, "Thong bao nghi hoc"));
        Assert.True(Ui.HasText(host, "Lop nghi hoc tuan sau."));
        w.Close();
        // Reading rỗng (không đoạn, không link) cũng phải dựng được.
        var host2 = new Border();
        host2.Child = ReaderWindow.Body(new Reading("Trong", "", [], [], [], null), host2);
        var w2 = Ui.Show(host2);
        Assert.True(Ui.HasText(host2, "Trong"));
        w2.Close();
    }

    private sealed record Item(string Name, string Group);

    [AvaloniaFact]
    public void RowMenus_OpenFlyout_FillsMenu()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = new List<Item> { new("Mot", "A"), new("Hai", "A"), new("Ba", "B") },
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Ten", Binding = new Avalonia.Data.Binding(nameof(Item.Name)) });
        var fills = 0;
        RowMenus.Attach(grid, (sender, _) =>
        {
            fills++;
            var menu = (MenuFlyout)sender!;
            menu.Items.Clear();
            menu.Items.Add(new MenuItem { Header = "Mo" });
        });
        var w = Ui.Show(grid, 600, 400);
        grid.SelectedIndex = 1;
        var flyout = Assert.IsType<MenuFlyout>(grid.ContextFlyout);
        flyout.ShowAt(grid);
        Ui.Pump(w);
        Assert.Equal(1, fills);
        Assert.True(flyout.IsOpen);
        flyout.Hide();
        Ui.Pump(w);
        Assert.False(flyout.IsOpen);
        w.Close();
    }

    [AvaloniaFact]
    public void RowMenus_EmptyFill_ClosesWithoutThrowing()
    {
        var grid = new DataGrid { AutoGenerateColumns = false, ItemsSource = new List<Item> { new("Mot", "A") } };
        RowMenus.Attach(grid, (sender, _) => ((MenuFlyout)sender!).Items.Clear());
        var w = Ui.Show(grid, 600, 400);
        var flyout = (FlyoutBase)grid.ContextFlyout!;
        flyout.ShowAt(grid);
        Ui.Pump(w);
        Ui.Pump(w);
        Assert.False(flyout.IsOpen);
        w.Close();
    }

    private static AppState State(bool withData)
    {
        if (withData) Sample.WriteData();
        var state = new AppState(new SourceHub([]));
        state.Reload();
        return state;
    }

    [AvaloniaFact]
    public void HomeView_Builds_WithAndWithoutData()
    {
        var opened = new List<string>();
        var home = new HomeView(State(true), () => { }, opened.Add);
        var w = Ui.Show(home);
        Assert.True(Ui.HasText(home, "Thong bao nghi hoc") || Ui.HasText(home, "Nhom hoc mau"));
        foreach (var width in new[] { 500d, 1400d, 900d }) { w.Width = width; Ui.Pump(w); }
        w.Close();
        var bare = new HomeView(new AppState(new SourceHub([])), () => { }, _ => { });
        var w2 = Ui.Show(bare);
        w2.Close();
    }

    [AvaloniaFact]
    public void SubjectsView_Builds_AndSwitchesTabs()
    {
        var state = State(true);
        var view = new SubjectsView(state, null, null, null);
        var w = Ui.Show(view);
        Assert.True(Ui.HasText(view, "Giai tich 1"));
        for (var i = 0; i < 6; i++) { view.SelectTab(i); Ui.Pump(w); }
        view.SelectTab(0);
        // Dựng lại giữ môn, tab như khi đồng bộ xong.
        var again = new SubjectsView(state, view, (_, _) => { }, _ => { });
        w.Content = again;
        Ui.Pump(w);
        w.Close();
    }
}
