using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using SoHocTap.Presentation;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Trang Cài đặt: vẽ SettingsCatalog (BKStudyDesk.Core/Presentation/SettingsCatalog.cs) thành các thẻ bằng control có sẵn: ToggleSwitch,
/// TextBox (số, chữ; lưu khi rời ô hay Enter, sai thì báo và giữ giá trị cũ), ComboBox, Button. Phần Nâng cao nằm trong Expander đóng sẵn.
/// Thêm, bớt mục cài đặt thì sửa SettingsCatalog, không sửa file này.
/// </summary>
public sealed class SettingsView : UserControl
{
    public SettingsView() { }

    internal SettingsView(SettingsActions actions)
    {
        var groups = SettingsCatalog.Groups(actions);
        var page = new StackPanel { Spacing = 24 };
        page.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children = { new TextBlock { Text = "Cài đặt", Classes = { "title" } }, new TextBlock { Text = "Mỗi mục lưu ngay khi đổi.", Classes = { "meta" } } },
        });
        foreach (var g in groups.Where(g => !g.Advanced)) page.Children.Add(Card(g));
        var advanced = new StackPanel { Spacing = 24, Margin = new Thickness(0, 16, 0, 0) };
        foreach (var g in groups.Where(g => g.Advanced)) advanced.Children.Add(Card(g));
        page.Children.Add(_advanced = new Expander { Header = "Nâng cao: đồng bộ, mở tài liệu, thư viện, nhật ký", Content = advanced, HorizontalAlignment = HorizontalAlignment.Stretch });
        Content = page;
    }

    private readonly Expander? _advanced;

    /// <summary>Mở phần Nâng cao (tham số --tab=1 khi chụp kiểm tra).</summary>
    public void ExpandAdvanced()
    {
        if (_advanced is not null) _advanced.IsExpanded = true;
    }

    private static Border Card(SettingGroup g)
    {
        var head = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = g.Title, Classes = { "card-title" } } } };
        if (g.Note is { Length: > 0 } note) head.Children.Add(new TextBlock { Text = note, Classes = { "meta" } });
        var body = new StackPanel();
        body.Children.Add(new Border { Classes = { "card-head" }, Child = head });
        foreach (var item in g.Items) body.Children.Add(Row(item));
        return new Border { Classes = { "card" }, Child = body };
    }

    /// <summary>Một dòng: trái là tên và ghi chú (ghi chú cũng là chỗ báo lỗi, báo kết quả), phải là control.</summary>
    private static Border Row(SettingItem item)
    {
        var note = new TextBlock { Text = item.Note ?? "", Classes = { "sub" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, TextTrimming = Avalonia.Media.TextTrimming.None, IsVisible = item.Note is { Length: > 0 } };
        void Say(string? text, bool error = false)
        {
            note.Text = text ?? item.Note ?? "";
            note.Classes.Set("danger", error);
            note.IsVisible = note.Text.Length > 0;
        }
        var left = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = item.Label, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, note } };
        Control right = item switch
        {
            ToggleItem t => Toggle(t),
            NumberItem n => Box(n.Get().ToString(System.Globalization.CultureInfo.InvariantCulture), 96, s =>
            {
                if (n.Parse(s) is not { } v) { Say(n.Error, true); return null; }
                n.Set(v);
                Say(null);
                return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }),
            TextItem t => Box(t.Get(), 280, s =>
            {
                if (t.Normalize(s) is not { } v) { Say(t.Error, true); return null; }
                t.Set(v);
                Say(null);
                return v;
            }),
            ChoiceItem c => Choice(c),
            ActionItem a => Action(a, Say),
            _ => new TextBlock(),
        };
        right.VerticalAlignment = VerticalAlignment.Center;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,Auto") };
        grid.Children.Add(left);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return new Border { Classes = { "row" }, Child = grid };
    }

    private static ToggleSwitch Toggle(ToggleItem t)
    {
        var s = new ToggleSwitch { IsChecked = t.Get(), OnContent = "Bật", OffContent = "Tắt" };
        s.IsCheckedChanged += (_, _) => t.Set(s.IsChecked == true);
        return s;
    }

    /// <summary>Ô chữ lưu khi rời ô hay Enter; <paramref name="save"/> trả chữ chuẩn để hiện lại, null là sai (giữ chữ đang gõ để sửa).</summary>
    private static TextBox Box(string value, double width, Func<string, string?> save)
    {
        var box = new TextBox { Text = value, Width = width };
        var last = value;
        void Commit()
        {
            var text = box.Text ?? "";
            if (text == last) return;
            if (save(text) is { } shown) box.Text = last = shown;
        }
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); e.Handled = true; } };
        return box;
    }

    private static ComboBox Choice(ChoiceItem c)
    {
        var box = new ComboBox { ItemsSource = c.Options.Select(o => o.Text).ToList(), MinWidth = 180 };
        var current = c.Get();
        box.SelectedIndex = Math.Max(0, c.Options.ToList().FindIndex(o => o.Value == current));
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) c.Set(c.Options[box.SelectedIndex].Value); };
        return box;
    }

    private static Button Action(ActionItem a, Action<string?, bool> say)
    {
        var b = new Button { Content = a.Button };
        b.Click += async (_, _) =>
        {
            b.IsEnabled = false;
            try { if (await a.Run() is { } done) say(done, false); }
            catch (Exception e) when (e is not OutOfMemoryException) { say(e.Message, true); }
            finally { b.IsEnabled = true; }
        };
        return b;
    }
}
