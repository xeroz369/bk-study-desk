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
/// Trang Hộp thư: tin nhắn riêng LMS, thông báo của các môn và thông báo hệ thống của LMS, đều từ bản lưu trên máy nên đọc được
/// khi LMS sập hay mất mạng. Trái: lọc theo loại, ô tìm, danh sách (chưa đọc in đậm). Phải: hội thoại (tin của mình lệch phải)
/// hay nội dung bài thông báo. Mở một dòng là đánh dấu đã đọc trong app (InboxReads), không đánh dấu gì trên LMS. Chỉ đọc.
/// </summary>
internal sealed class InboxView : UserControl, IFillPage
{
    private const int PageSize = 60;
    private readonly ListBox _list = new();
    private readonly TextBox _filter = new() { PlaceholderText = L.T("inbox.filter"), Margin = new Thickness(0, 0, 0, 8) };
    private readonly ComboBox _kind = new() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 8) };
    private readonly StackPanel _detail = new() { Spacing = 12 };
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

    public InboxView()
    {
        _scroll = new ScrollViewer { Content = _detail, Padding = new Thickness(16) };
        // Ô danh sách bị dọn (đổi danh sách, đổi cỡ cửa sổ) thì Avalonia gọi template với null: trả ô trống, không crash.
        _list.ItemTemplate = new FuncDataTemplate<InboxRow>((r, _) => r is null ? new Panel() : Row(r));
        _list.SelectionChanged += (_, _) => { if (!_filling) ShowDetail(); };
        _kind.ItemsSource = new[] { L.T("inbox.all"), L.T("inbox.kind.message"), L.T("inbox.kind.news"), L.T("inbox.kind.system") };
        _kind.SelectedIndex = 0;
        _kind.SelectionChanged += (_, _) => Fill();
        // Lọc khi ngừng gõ một chút, không lọc lại cả hộp thư mỗi phím.
        _typing.Tick += (_, _) => { _typing.Stop(); Fill(); };
        _filter.TextChanged += (_, _) => { _typing.Stop(); _typing.Start(); };

        var left = new DockPanel { Margin = new Thickness(0, 0, 16, 0) };
        DockPanel.SetDock(_kind, Dock.Top);
        DockPanel.SetDock(_filter, Dock.Top);
        left.Children.Add(_kind);
        left.Children.Add(_filter);
        left.Children.Add(_list);
        var right = new Border { Classes = { "card" }, Child = _scroll };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("340,*") };
        Grid.SetColumn(right, 1);
        body.Children.Add(left);
        body.Children.Add(right);

        var header = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 16) };
        header.Children.Add(new TextBlock { Text = L.T("inbox.title"), Classes = { "title" } });
        header.Children.Add(new TextBlock { Text = L.T("inbox.sub"), Classes = { "meta" } });
        var page = new DockPanel { Margin = new Thickness(24, 16) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_empty, Dock.Top);
        page.Children.Add(header);
        page.Children.Add(_empty);
        page.Children.Add(body);
        Content = page;
    }

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
        _filter.Text = "";
        _kind.SelectedIndex = 0;
        Fill();
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
            _list.SelectedItem = rows.FirstOrDefault(r => r.Key == keep) ?? rows.FirstOrDefault();
        }
        finally { _filling = false; }
        _empty.Text = L.T("inbox.empty");
        _empty.IsVisible = _rows.Count == 0;
        ShowDetail();
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
        var sub = r.Sub.Length > 0 ? $"{r.KindText}, {r.Sub}" : r.KindText;
        panel.Children.Add(new TextBlock { Text = sub, Classes = { "meta" }, TextTrimming = TextTrimming.CharacterEllipsis });
        if (r.Preview.Length > 0) panel.Children.Add(new TextBlock { Text = r.Preview, Classes = { "sub" }, TextTrimming = TextTrimming.CharacterEllipsis });
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
        if (row is null)
        {
            if (_rows.Count > 0) _detail.Children.Add(new TextBlock { Text = L.T("inbox.pick"), Classes = { "meta" } });
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
            try { _reads.Save(); } catch (IOException e) { Log.Warn($"Lưu đã đọc: {e.Message}"); }
        if (!row.Unread) return;
        var read = row with { Unread = false };
        _rows = [.. _rows.Select(r => r.Key == row.Key ? read : r)];
        _shown = (read.Key, read.Time, _shown?.Count ?? 0);
        var items = (_list.ItemsSource as IEnumerable<InboxRow>)?.Select(r => r.Key == row.Key ? read : r).ToList();
        _filling = true;
        try { _list.ItemsSource = items; _list.SelectedItem = read; }
        finally { _filling = false; }
    }

    private void ShowThread(InboxRow row, LmsConversation c)
    {
        var head = new DockPanel();
        if (c.Url is { Length: > 0 } url)
        {
            var reply = new Button { Content = L.T("messages.reply") };
            reply.Click += async (_, _) => await Links.OpenAsync(this, url, c.Name);
            DockPanel.SetDock(reply, Dock.Right);
            head.Children.Add(reply);
        }
        var title = new StackPanel { Spacing = 2 };
        title.Children.Add(new SelectableTextBlock { Text = row.Title, Classes = { "title" }, TextWrapping = TextWrapping.Wrap });
        if (_messages is { SyncedAt: > 0 } d) title.Children.Add(new TextBlock { Text = L.F("messages.synced", Format.DateTime(d.SyncedAt)), Classes = { "meta" } });
        head.Children.Add(title);
        _detail.Children.Add(head);
        // Chỉ dựng _limit tin mới nhất (hội thoại vài trăm tin dựng hết thì chậm); tin cũ hơn mở bằng nút.
        var all = MessagesPresenter.Messages(c);
        if (all.Count > _limit)
        {
            var older = new Button { Content = L.F("messages.older", all.Count - _limit), HorizontalAlignment = HorizontalAlignment.Center };
            older.Click += (_, _) => { _limit += PageSize; _shown = null; ShowDetail(); };
            _detail.Children.Add(older);
        }
        foreach (var m in all.Skip(Math.Max(0, all.Count - _limit))) _detail.Children.Add(Bubble(m));
        // Chờ dựng xong rồi mới cuộn: cuộn ngay thì chiều cao chưa tính, dừng giữa chừng.
        Avalonia.Threading.Dispatcher.UIThread.Post(_scroll.ScrollToEnd, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private Control Bubble(MessageRow m)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = $"{m.Who}, {m.When}", Classes = { "meta" } });
        foreach (var p in m.Paragraphs) panel.Children.Add(new SelectableTextBlock { Text = p, TextWrapping = TextWrapping.Wrap });
        foreach (var l in m.Links.Where(l => !string.IsNullOrEmpty(l.Url)))
        {
            var link = new HyperlinkButton { Content = new TextBlock { Text = l.Name ?? l.Url, TextWrapping = TextWrapping.Wrap }, Padding = new Thickness(0) };
            ToolTip.SetTip(link, l.Url);
            link.Click += async (_, _) => await Links.OpenAsync(this, l.Url!, l.Name ?? "");
            panel.Children.Add(link);
        }
        return new Border
        {
            Classes = { m.Mine ? "bubble-mine" : "bubble" },
            Child = panel,
            MaxWidth = 640,
            HorizontalAlignment = m.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };
    }
}
