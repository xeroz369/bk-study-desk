using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Trang Cài đặt: vẽ SettingsCatalog (BKStudyDesk.Core/Presentation/SettingsCatalog.cs) thành các thẻ bằng control có sẵn: ToggleSwitch,
/// TextBox (số, chữ; lưu khi rời ô hay Enter, sai thì báo và giữ giá trị cũ), ComboBox, Button. Trái là danh sách mục (mục Nâng cao
/// xếp sau dòng Nâng cao), phải chỉ hiện thẻ của mục đang chọn, không phải cuộn cả trang dài. Gõ vào ô tìm thì hiện mọi thẻ có dòng khớp.
/// Thêm, bớt mục cài đặt thì sửa SettingsCatalog, không sửa file này.
/// </summary>
public sealed class SettingsView : UserControl, IFillPage
{
    public SettingsView() { }

    internal SettingsView(SettingsActions actions)
    {
        var groups = SettingsCatalog.Groups(actions);
        var ordered = groups.Where(g => !g.Advanced).Concat(groups.Where(g => g.Advanced)).ToList();
        var cards = new StackPanel { Spacing = 24 };
        foreach (var g in ordered) cards.Children.Add(Card(g));

        // Danh sách mục bên trái: dòng "Nâng cao" chỉ là tiêu đề, không chọn được.
        var items = new List<ListBoxItem>();
        foreach (var g in ordered)
        {
            if (g.Advanced && !items.Any(i => !i.IsEnabled))
                items.Add(new ListBoxItem { Content = new TextBlock { Text = L.T("set.advancedNav"), Classes = { "meta" } }, IsEnabled = false, Margin = new Thickness(0, 12, 0, 0) });
            var title = new TextBlock { Text = g.Title, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
            ToolTip.SetTip(title, g.Title);
            items.Add(new ListBoxItem { Content = title, Tag = g });
        }
        _nav.ItemsSource = items;
        _nav.SelectionChanged += (_, _) => { if (_nav.SelectedItem is ListBoxItem { Tag: SettingGroup }) Filter(_search.Text ?? ""); };
        _search.TextChanged += (_, _) => Filter(_search.Text ?? "");
        var left = new DockPanel { Margin = new Thickness(0, 0, 24, 0) };
        DockPanel.SetDock(_search, Dock.Top);
        left.Children.Add(_search);
        left.Children.Add(_nav);

        var right = new ScrollViewer { Content = new StackPanel { Spacing = 12, Children = { _noMatch, cards } } };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("240,*") };
        Grid.SetColumn(right, 1);
        body.Children.Add(left);
        body.Children.Add(right);

        var header = new TextBlock { Text = L.T("set.title"), Classes = { "title" }, Margin = new Thickness(0, 0, 0, 16) };
        var page = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        page.Children.Add(header);
        page.Children.Add(body);
        Content = page;
        _nav.SelectedIndex = 0;
    }

    private readonly ListBox _nav = new();
    private readonly TextBox _search = new() { PlaceholderText = L.T("set.search"), Margin = new Thickness(0, 0, 0, 8) };

    /// <summary>Chọn mục đầu tiên thỏa điều kiện ở danh sách bên trái (bỏ ô tìm).</summary>
    private void Pick(Func<SettingGroup, bool> which)
    {
        _search.Text = "";
        _nav.SelectedItem = (_nav.ItemsSource as IEnumerable<ListBoxItem>)?.FirstOrDefault(i => i.Tag is SettingGroup g && which(g));
    }

    // Không tìm: chỉ hiện thẻ của mục đang chọn. Đang tìm (SettingsCatalog.Matches): mọi thẻ có dòng khớp, ẩn dòng không khớp.
    private readonly List<(SettingGroup Group, Border Card, List<(SettingItem Item, Border Row)> Rows)> _cards = [];
    private readonly TextBlock _noMatch = new() { Text = L.T("set.searchNone"), Classes = { "meta" }, IsVisible = false };

    private void Filter(string query)
    {
        var searching = !string.IsNullOrWhiteSpace(query);
        var picked = (_nav.SelectedItem as ListBoxItem)?.Tag as SettingGroup;
        var any = false;
        foreach (var (g, card, rows) in _cards)
        {
            foreach (var (item, row) in rows) row.IsVisible = !searching || SettingsCatalog.Matches(g, item, query);
            card.IsVisible = searching ? rows.Any(r => r.Row.IsVisible) : ReferenceEquals(g, picked);
            any |= card.IsVisible;
        }
        _noMatch.IsVisible = searching && !any;
    }

    private Action? _openCredential;   // mở form tài khoản của mục CredentialItem (dải báo mời bật tự đăng nhập lại)

    /// <summary>Mở mục Nâng cao đầu tiên (tham số --tab=1 khi chụp kiểm tra).</summary>
    public void ExpandAdvanced() => Pick(g => g.Advanced);

    /// <summary>Mở sẵn form tự đăng nhập lại (nút Bật trên dải báo): chọn mục có dòng đó rồi mở form; máy không hỗ trợ thì không có gì.</summary>
    public void OpenAutoLogin()
    {
        Pick(g => g.Items.Any(i => i is CredentialItem));
        _openCredential?.Invoke();
    }

    private Border Card(SettingGroup g)
    {
        var head = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = g.Title, Classes = { "card-title" } } } };
        if (g.Note is { Length: > 0 } note) head.Children.Add(new TextBlock { Text = note, Classes = { "meta" } });
        var body = new StackPanel();
        body.Children.Add(new Border { Classes = { "card-head" }, Child = head });
        var rows = g.Items.Select(i => (i, Row(i))).ToList();
        foreach (var (_, row) in rows) body.Children.Add(row);
        var card = new Border { Classes = { "card" }, Child = body };
        _cards.Add((g, card, rows));
        return card;
    }

    /// <summary>Một dòng: trái là tên và ghi chú (ghi chú cũng là chỗ báo lỗi, báo kết quả), phải là control.</summary>
    private Border Row(SettingItem item)
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
            CheckItem c => Check(c, Say),
            InfoItem i => new TextBlock { Text = i.Value, Classes = { "meta" } },
            CredentialItem c => new Button(),   // dựng ở Credential: nút và form đi cùng nhau
            _ => new TextBlock(),
        };
        right.VerticalAlignment = VerticalAlignment.Center;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,Auto") };
        grid.Children.Add(left);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        if (item is CredentialItem cred) return new Border { Classes = { "row" }, Child = Credential(cred, (Button)right, grid, Say) };
        return new Border { Classes = { "row" }, Child = grid };
    }

    /// <summary>
    /// Mục tự đăng nhập lại: nút Bật mở form ngay dưới dòng (tên đăng nhập, mật khẩu, Lưu và bật, Hủy); đang bật thì nút Tắt (xóa
    /// tài khoản). Ô mật khẩu luôn được xóa sau khi bấm Lưu, kể cả khi lưu lỗi.
    /// </summary>
    private StackPanel Credential(CredentialItem c, Button toggle, Grid row, Action<string?, bool> say)
    {
        var user = new TextBox { PlaceholderText = L.T("autoLogin.user") };
        var password = new TextBox { PlaceholderText = L.T("autoLogin.password"), PasswordChar = '●', Classes = { "revealPasswordButton" } };
        var save = new Button { Content = L.T("autoLogin.save"), Classes = { "accent" } };   // không IsDefault: Enter ở ô khác của trang không bấm nút này
        var cancel = new Button { Content = L.T("events.cancel") };
        var form = new StackPanel
        {
            Spacing = 8, Margin = new Thickness(0, 12, 0, 0), IsVisible = false,
            Children =
            {
                new TextBlock { Text = L.T("autoLogin.noteAnyOs"), Classes = { "sub" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, TextTrimming = Avalonia.Media.TextTrimming.None },
                user, password,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { save, cancel } },
            },
        };
        void Sync() => toggle.Content = L.T(c.IsOn() ? "set.turnOff" : "set.turnOn");
        void Open() { form.IsVisible = true; user.Focus(); }
        Sync();
        toggle.Click += (_, _) =>
        {
            if (!c.IsOn()) { Open(); return; }
            say(c.Off(), false);
            Sync();
        };
        void Save()
        {
            var error = c.Save(user.Text ?? "", password.Text ?? "");
            password.Text = "";
            if (error is not null) { say(error, true); return; }
            form.IsVisible = false;
            user.Text = "";
            say(L.T("set.autoLoginOnDone"), false);
            Sync();
        }
        save.Click += (_, _) => Save();
        password.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Save(); e.Handled = true; } };
        cancel.Click += (_, _) => { form.IsVisible = false; password.Text = ""; };
        _openCredential = Open;
        return new StackPanel { Children = { row, form } };
    }

    private static ToggleSwitch Toggle(ToggleItem t)
    {
        var s = new ToggleSwitch { IsChecked = t.Get(), OnContent = L.T("set.on"), OffContent = L.T("set.off") };
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

    /// <summary>Check button; the accent confirm button appears only when the check found work, and hides after it runs.</summary>
    private static StackPanel Check(CheckItem c, Action<string?, bool> say)
    {
        var check = new Button { Content = c.Button };
        var confirm = new Button { Classes = { "accent" }, IsVisible = false };
        Func<Task<(string Message, bool Error)>>? apply = null;
        check.Click += async (_, _) =>
        {
            check.IsEnabled = false;
            confirm.IsVisible = false;
            try
            {
                var step = await c.Check();
                say(step.Message, false);
                apply = step.Apply;
                if (step.Confirm is { } label && apply is not null) { confirm.Content = label; confirm.IsVisible = true; }
            }
            catch (Exception e) when (e is not OutOfMemoryException) { say(e.Message, true); }
            finally { check.IsEnabled = true; }
        };
        confirm.Click += async (_, _) =>
        {
            if (apply is null) return;
            check.IsEnabled = confirm.IsEnabled = false;
            try { var (text, error) = await apply(); say(text, error); }
            catch (Exception e) when (e is not OutOfMemoryException) { say(e.Message, true); }
            finally { apply = null; confirm.IsVisible = false; check.IsEnabled = confirm.IsEnabled = true; }
        };
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { confirm, check } };
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
