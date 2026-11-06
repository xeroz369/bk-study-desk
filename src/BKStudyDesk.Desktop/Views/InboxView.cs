using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using BKStudyDesk.Desktop.Files;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Hộp thư dạng ngăn trượt ra từ bên phải (như ngăn tin nhắn của LMS, người dùng chốt 05/11/2026): một cột, danh sách tin; bấm một tin
/// thì nội dung thay chỗ danh sách, có nút quay lại và nút đóng ngăn. Nội dung:
/// tin nhắn riêng LMS, thông báo của các môn và thông báo hệ thống của LMS, đều từ bản lưu trên máy nên đọc được
/// khi LMS sập hay mất mạng. Trái: lọc theo loại, ô tìm, danh sách (chưa đọc in đậm). Phải: hội thoại như khung tin nhắn của LMS
/// (đầu khung cố định, vạch ngày, tin liền nhau của một người gộp lại, tin của mình lệch phải, link nằm trong chữ) hay nội dung bài thông báo. Mở một dòng là đánh dấu đã đọc trong app (InboxReads), không đánh dấu gì trên LMS. Chỉ đọc.
/// </summary>
internal sealed class InboxView : UserControl
{
    /// <summary>Độ rộng ngăn; bong bóng tin nhắn co theo.</summary>
    public const double DrawerWidth = 460;
    private readonly Panel _listPane = new();
    private readonly DockPanel _detailPane = new() { IsVisible = false };
    private Button? _backButton;
    private bool _detailOpen;   // đang xem một tin (ngăn hiện nội dung thay cho danh sách)

    /// <summary>Bấm nút đóng ngăn.</summary>
    public event Action? CloseRequested;
    private const int PageSize = 60;
    private readonly ListBox _list = new();
    private readonly TextBox _filter = new() { PlaceholderText = L.T("inbox.filter"), Margin = new Thickness(0, 0, 0, 8) };
    private readonly ComboBox _kind = new() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 8) };
    private readonly StackPanel _detail = new() { Spacing = 12 };
    private readonly Border _head = new() { Padding = new Thickness(16, 12), BorderThickness = new Thickness(0, 0, 0, 1), IsVisible = false };
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _empty = new() { Classes = { "empty" }, TextWrapping = TextWrapping.Wrap };
    private readonly Avalonia.Threading.DispatcherTimer _typing = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly InboxReads _reads = InboxReads.Load();
    private LmsMessages? _messages;
    private LmsData? _lms;
    private IReadOnlyList<InboxRow> _rows = [];
    private IReadOnlyDictionary<string, string> _index = new Dictionary<string, string>();
    private bool _filling;   // đang thay danh sách: SelectionChanged lúc này không phải người dùng chọn
    private int _limit = PageSize;   // số tin hiện của hội thoại đang xem (mới nhất); bấm Xem tin cũ hơn thì tăng
    private (string Key, long Time, int Count)? _shown;   // dòng đang hiện: không dựng lại, không cuộn lại nếu không đổi
    private static string? _lastKey;   // dòng đang đọc, giữ qua lần đọc lại dữ liệu

    /// <summary>Nút Tải tin nhắn ngay khi chưa có tin nhắn lưu trên máy (MainWindow nối vào AppHost.SyncMessagesNowAsync).</summary>
    public Func<Task<bool>>? SyncNow { get; set; }

    /// <summary>Vừa đánh dấu đã đọc một dòng: số chưa đọc trên icon Hộp thư cập nhật.</summary>
    public event Action? ReadChanged;

    public InboxView()
    {
        _scroll = new ScrollViewer { Content = _detail, Padding = new Thickness(16) };
        // Ô danh sách bị dọn (đổi danh sách, đổi cỡ cửa sổ) thì Avalonia gọi template với null: trả ô trống, không crash.
        _list.ItemTemplate = new FuncDataTemplate<InboxRow>((r, _) => r is null ? new Panel() : Row(r));
        _list.SelectionChanged += (_, _) =>
        {
            if (_filling || _list.SelectedItem is null) return;
            _detailOpen = true;
            Swap();
            ShowDetail();
        };
        _kind.ItemsSource = new[] { L.T("inbox.all"), L.T("inbox.kind.message"), L.T("inbox.kind.news"), L.T("inbox.kind.system") };
        _kind.SelectedIndex = 0;
        _kind.SelectionChanged += (_, _) => Fill();
        // Lọc khi ngừng gõ một chút, không lọc lại cả hộp thư mỗi phím.
        _typing.Tick += (_, _) => { _typing.Stop(); Fill(); };
        _filter.TextChanged += (_, _) => { _typing.Stop(); _typing.Start(); };

        // Danh sách: lọc theo loại, ô tìm, các tin.
        var list = new DockPanel { Margin = new Thickness(12, 8, 12, 0) };
        DockPanel.SetDock(_kind, Dock.Top);
        DockPanel.SetDock(_filter, Dock.Top);
        DockPanel.SetDock(_empty, Dock.Top);
        list.Children.Add(_kind);
        list.Children.Add(_filter);
        list.Children.Add(_empty);
        list.Children.Add(_list);
        _listPane.Children.Add(list);
        // Nội dung một tin: đầu khung (tên, Trả lời trên LMS) cố định, nội dung cuộn.
        _head.Bind(Border.BorderBrushProperty, _head.GetResourceObservable("Line"));
        DockPanel.SetDock(_head, Dock.Top);
        _detailPane.Children.Add(_head);
        _detailPane.Children.Add(_scroll);

        // Đầu ngăn: quay lại (khi đang xem một tin), tên ngăn, đóng.
        _backButton = Icons.Button("Back", L.T("inbox.back"));
        _backButton.IsVisible = false;
        _backButton.Click += (_, _) => BackToList();
        var close = Icons.Button("Close", L.T("common.close"));
        close.Click += (_, _) => CloseRequested?.Invoke();
        var top = new DockPanel { Margin = new Thickness(8, 6) };
        DockPanel.SetDock(_backButton, Dock.Left);
        DockPanel.SetDock(close, Dock.Right);
        top.Children.Add(_backButton);
        top.Children.Add(close);
        top.Children.Add(new TextBlock { Text = L.T("inbox.title"), Classes = { "card-title" }, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) });
        var topBorder = new Border { Child = top, BorderThickness = new Thickness(0, 0, 0, 1) };
        topBorder.Bind(Border.BorderBrushProperty, topBorder.GetResourceObservable("Line"));

        var body = new Panel { Children = { _listPane, _detailPane } };
        var root = new DockPanel();
        DockPanel.SetDock(topBorder, Dock.Top);
        root.Children.Add(topBorder);
        root.Children.Add(body);
        Content = root;
    }

    /// <summary>Đang xem nội dung một tin thì hiện nội dung và nút quay lại; không thì hiện danh sách.</summary>
    private void Swap()
    {
        _listPane.IsVisible = !_detailOpen;
        _detailPane.IsVisible = _detailOpen;
        if (_backButton is not null) _backButton.IsVisible = _detailOpen;
    }

    /// <summary>Về danh sách (nút quay lại, Alt+Mũi tên trái); bỏ chọn để bấm lại đúng tin đó vẫn mở được.</summary>
    public void BackToList()
    {
        _detailOpen = false;
        _filling = true;
        try { _list.SelectedItem = null; }
        finally { _filling = false; }
        _shown = null;
        Swap();
    }

    /// <summary>Đang xem một tin: Alt+Mũi tên trái về danh sách trước khi lùi trang.</summary>
    public bool CanGoBack => _detailOpen;

    /// <summary>Đọc lại dữ liệu (sau lượt đồng bộ), giữ dòng đang xem. Không đổi gì thì không dựng lại.</summary>
    public void Reload(LmsData? lms)
    {
        var messages = LmsMessagesStore.Read();
        if (ReferenceEquals(messages, _messages) && ReferenceEquals(lms, _lms) && _rows.Count > 0) return;
        (_messages, _lms) = (messages, lms);
        var news = lms?.Announcements ?? [];
        _rows = Inbox.Rows(_messages, news, _reads, Format.Now, Config.Int("app.inbox.newsDays", 14));
        _index = Inbox.SearchIndex(_messages, news);
        Fill();
    }

    /// <summary>Mở đúng một dòng (bấm từ thẻ ở Hôm nay, từ thông báo của máy): bỏ lọc để chắc chắn thấy.</summary>
    public void Select(string key)
    {
        _lastKey = key;
        _detailOpen = true;
        _filter.Text = "";
        _kind.SelectedIndex = 0;
        Fill();
        Swap();
    }

    private void Fill()
    {
        var keep = (_list.SelectedItem as InboxRow)?.Key ?? _lastKey;
        InboxKind? kind = _kind.SelectedIndex switch { 1 => InboxKind.Message, 2 => InboxKind.News, 3 => InboxKind.System, _ => null };
        var rows = Inbox.Filter(_rows, kind, _filter.Text ?? "", _index);
        _filling = true;
        try
        {
            _list.ItemsSource = rows;
            // Danh sách: không tự chọn (chọn là mở nội dung). Đang xem một tin thì giữ đúng tin đó.
            _list.SelectedItem = _detailOpen ? rows.FirstOrDefault(r => r.Key == keep) : null;
            if (_detailOpen && _list.SelectedItem is null) _detailOpen = false;
        }
        finally { _filling = false; }
        _empty.Text = L.T("inbox.empty");
        _empty.IsVisible = _rows.Count == 0;
        Swap();
        if (_detailOpen) ShowDetail();
    }

    private static Control Row(InboxRow r)
    {
        var name = new TextBlock { Text = r.Title, Classes = { "name" }, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = r.Unread ? FontWeight.Bold : FontWeight.Normal };
        var when = new TextBlock { Text = r.When, Classes = { "meta" }, HorizontalAlignment = HorizontalAlignment.Right };
        var top = new DockPanel();
        DockPanel.SetDock(when, Dock.Right);
        top.Children.Add(when);
        if (r.Unread)
        {
            var dot = new Border { Classes = { "badge" }, Margin = new Thickness(0, 0, 6, 0), Width = 8, Height = 8, Padding = new Thickness(0) };
            DockPanel.SetDock(dot, Dock.Right);
            top.Children.Add(dot);
        }
        top.Children.Add(name);
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(4, 6) };
        panel.Children.Add(top);
        // Dòng hai: tin nhắn thì chỉ đoạn đầu; thông báo thì ghi môn (hay loại) trước đoạn đầu.
        var second = r.Kind == InboxKind.Message ? r.Preview : string.Join(": ", new[] { r.Sub.Length > 0 ? r.Sub : r.KindText, r.Preview }.Where(x => x.Length > 0));
        if (second.Length > 0) panel.Children.Add(new TextBlock { Text = second, Classes = { "sub" }, TextTrimming = TextTrimming.CharacterEllipsis });
        return panel;
    }

    private void ShowDetail()
    {
        var row = _list.SelectedItem as InboxRow;
        if (row is not null) _lastKey = row.Key;
        var conv = row?.Kind == InboxKind.Message ? _messages?.Conversations.FirstOrDefault(c => "msg:" + c.Id == row.Key) : null;
        var key = row is null ? ((string, long, int)?)null : (row.Key, row.Time, conv?.Messages.Count ?? 0);
        if (key is not null && key == _shown) return;   // cùng dòng, không có gì mới: giữ nguyên chỗ đang đọc
        if (key?.Item1 != _shown?.Item1) _limit = PageSize;
        _shown = key;
        _detail.Children.Clear();
        _head.Child = null;
        _head.IsVisible = false;
        if (row is null)
        {
            if ((_list.ItemsSource as IEnumerable<InboxRow>)?.Any() == true) _detail.Children.Add(new TextBlock { Text = L.T("inbox.pick"), Classes = { "meta" } });
            else if (_rows.Count > 0) _detail.Children.Add(new TextBlock { Text = L.T("inbox.noMatch"), Classes = { "meta" }, TextWrapping = TextWrapping.Wrap });
            // Chưa có tin nhắn lưu trên máy: nói rõ vì sao trống, cho tải ngay thay vì chờ lượt đồng bộ LMS.
            if (_messages is null && SyncNow is { } sync)
            {
                _detail.Children.Add(new TextBlock { Text = L.T("inbox.noMessagesYet"), Classes = { "meta" }, TextWrapping = TextWrapping.Wrap });
                var now = new Button { Content = L.T("inbox.syncNow") };
                now.Click += async (_, _) =>
                {
                    now.IsEnabled = false;
                    now.Content = L.T("inbox.syncing");
                    var ok = await sync();
                    if (!ok) { now.IsEnabled = true; now.Content = L.T("inbox.syncNow"); }
                };
                _detail.Children.Add(now);
            }
            return;
        }
        MarkRead(row);
        if (conv is not null) ShowThread(row, conv);
        else if (_lms?.Announcements?.FirstOrDefault(a => "news:" + a.Id == row.Key) is { } a) _detail.Children.Add(ReaderWindow.Body(SubjectsPresenter.ReadingOf(a), this));
    }

    /// <summary>Đã đọc trong app tới tin mới nhất của dòng; dòng trong danh sách bỏ in đậm ngay.</summary>
    private void MarkRead(InboxRow row)
    {
        if (!row.Unread && _reads.ReadAt(row.Key) is not null) return;
        if (_reads.Mark(row.Key, row.Time))
        {
            try { _reads.Save(); } catch (IOException e) { Log.Warn($"Lưu đã đọc: {e.Message}"); }
            ReadChanged?.Invoke();
        }
        if (!row.Unread) return;
        var read = row with { Unread = false };
        _rows = [.. _rows.Select(r => r.Key == row.Key ? read : r)];
        _shown = (read.Key, read.Time, _shown?.Count ?? 0);
        var items = (_list.ItemsSource as IEnumerable<InboxRow>)?.Select(r => r.Key == row.Key ? read : r).ToList();
        _filling = true;
        try { _list.ItemsSource = items; _list.SelectedItem = read; }
        finally { _filling = false; }
    }

    /// <summary>
    /// Hội thoại như khung tin nhắn của LMS: đầu khung cố định (tên, nhóm hay riêng, lúc lưu, Trả lời trên LMS), vạch ngày, tin
    /// liền nhau của một người gộp lại (tên một lần, mỗi tin ghi giờ), tin của mình lệch phải.
    /// </summary>
    private void ShowThread(InboxRow row, LmsConversation c)
    {
        var head = new DockPanel();
        if (c.Url is { Length: > 0 } url)
        {
            var reply = new Button { Content = L.T("messages.reply"), VerticalAlignment = VerticalAlignment.Center };
            reply.Click += async (_, _) => await Links.OpenAsync(this, url, c.Name);
            DockPanel.SetDock(reply, Dock.Right);
            head.Children.Add(reply);
        }
        var title = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock { Text = row.Title, Classes = { "card-title" }, TextTrimming = TextTrimming.CharacterEllipsis });
        var kind = c.Group == true ? L.F("messages.groupOf", Math.Max(c.Members.Count, 1)) : L.T("messages.private");
        var synced = _messages is { SyncedAt: > 0 } d ? ", " + L.F("messages.synced", Format.DateTime(d.SyncedAt)) : "";
        title.Children.Add(new TextBlock { Text = kind + synced, Classes = { "meta" }, TextTrimming = TextTrimming.CharacterEllipsis });
        head.Children.Add(title);
        _head.Child = head;
        _head.IsVisible = true;
        // Chỉ dựng _limit tin mới nhất (hội thoại vài trăm tin dựng hết thì chậm); tin cũ hơn mở bằng nút.
        var (items, older) = MessagesPresenter.Thread(c, Format.Now, _limit);
        if (older > 0)
        {
            var more = new Button { Content = L.F("messages.older", older), HorizontalAlignment = HorizontalAlignment.Center };
            more.Click += (_, _) => { _limit += PageSize; _shown = null; ShowDetail(); };
            _detail.Children.Add(more);
        }
        foreach (var item in items) _detail.Children.Add(item is ThreadDay day ? DayMark(day.Text) : Group((ThreadGroup)item));
        // Chờ dựng xong rồi mới cuộn: cuộn ngay thì chiều cao chưa tính, dừng giữa chừng.
        Avalonia.Threading.Dispatcher.UIThread.Post(_scroll.ScrollToEnd, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Vạch ngày ở giữa khung, hai bên là đường kẻ.</summary>
    private static Control DayMark(string text)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), Margin = new Thickness(0, 8, 0, 0) };
        var label = new TextBlock { Text = text, Classes = { "meta" }, Margin = new Thickness(12, 0) };
        Grid.SetColumn(label, 1);
        var right = new Separator();
        Grid.SetColumn(right, 2);
        grid.Children.Add(new Separator());
        grid.Children.Add(label);
        grid.Children.Add(right);
        return grid;
    }

    /// <summary>Một nhóm tin của cùng một người: tên một lần (tin người khác), mỗi tin một bong bóng, giờ ở góc dưới bong bóng.</summary>
    private Control Group(ThreadGroup g)
    {
        var align = g.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        var stack = new StackPanel { Spacing = 4, HorizontalAlignment = align, MaxWidth = DrawerWidth - 80 };
        if (!g.Mine) stack.Children.Add(new TextBlock { Text = g.Who, Classes = { "meta" }, FontWeight = FontWeight.SemiBold });
        foreach (var m in g.Messages)
        {
            var panel = new StackPanel { Spacing = 4 };
            ReadingBlocks.Add(panel, m.Blocks, this);
            panel.Children.Add(new TextBlock { Text = m.Hm, Classes = { "sub" }, HorizontalAlignment = HorizontalAlignment.Right });
            ToolTip.SetTip(panel, Format.DateTime(m.Time));
            stack.Children.Add(new Border { Classes = { g.Mine ? "bubble-mine" : "bubble" }, Child = panel, HorizontalAlignment = align });
        }
        return stack;
    }
}
