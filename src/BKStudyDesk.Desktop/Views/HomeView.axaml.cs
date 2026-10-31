using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Chỉ nối HomeModel vào XAML, đặt câu trống, đổi một cột hay hai cột theo độ rộng, mở link. Lọc, sắp, chữ trong dòng: HomePresenter.
/// </summary>
public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();

    private readonly Action _login = () => { };
    private readonly Action<string> _openInbox = _ => { };
    private IReadOnlyList<TodoItem> _todo = [];
    // Nhóm việc (khảo sát của nhiều môn...) người dùng đã bấm xổ ra; giữ qua lần dựng lại trang sau đồng bộ.
    private static readonly HashSet<string> Expanded = [];

    internal HomeView(AppState state, Action login, Action<string> openInbox) : this()
    {
        _login = login;
        _openInbox = openInbox;
        var inbox = Inbox.Recent(Inbox.Rows(SoHocTap.Data.LmsMessagesStore.Read(), state.Lms?.Announcements ?? [], InboxReads.Load(), Format.Now, HomePresenter.NewsDays),
            Format.Now, HomePresenter.NewsDays);
        InboxList.ItemsSource = inbox;
        InboxMeta.Text = L.F("home.inboxMeta", inbox.Count(r => r.Unread), HomePresenter.NewsDays);
        SyncLine.Text = HomePresenter.SyncLine(state.SyncedAt(SoHocTap.Core.SourceIds.Lms), state.SyncedAt(SoHocTap.Core.SourceIds.Mybk));
        var m = HomePresenter.Build(state.Timeline, state.Lms, state.Mybk, Format.Now);
        DataContext = m;
        _todo = m.Todo;
        TodoList.ItemsSource = TodoGroups.Collapse(_todo, Expanded);
        Empty(InboxEmpty, inbox.Count, m.HasLms ? L.F("home.inboxEmpty", HomePresenter.NewsDays) : L.T("home.noLms"));
        DateText.Text = Format.DateLong(Format.Today);
        var none = !m.HasLms && !m.HasMybk;
        Intro.IsVisible = none;
        Columns.IsVisible = !none;
        TodoMeta.Text = L.F("home.todoMeta", m.Todo.Count, HomePresenter.TodoDays);
        Empty(TodoEmpty, m.Todo.Count, m.HasLms ? L.F("home.todoEmpty", HomePresenter.TodoDays) : L.T("home.noLms"));
        Empty(UpcomingEmpty, m.Upcoming.Count, m.HasMybk ? L.F("home.upcomingEmpty", HomePresenter.ExamDays) : L.T("home.noMybk"));
        Empty(RegEmpty, m.Registrations.Count, m.HasMybk ? L.T("home.regEmpty") : L.T("home.noMybk"));
    }

    private static void Empty(TextBlock box, int count, string text)
    {
        box.Text = text;
        box.IsVisible = count == 0;
    }

    /// <summary>Hai cột khi đủ chỗ cho mức tối thiểu của cả hai (MainMinWidth, SideMinWidth trong Tokens.axaml); không thì cột phải xuống dưới.</summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var cols = Columns.ColumnDefinitions;
        var two = e.NewSize.Width >= (double)this.FindResource("MainMinWidth")! + 24 + (double)this.FindResource("SideMinWidth")!;
        cols[0].MinWidth = two ? (double)this.FindResource("MainMinWidth")! : 0;
        cols[1].Width = new GridLength(two ? 24 : 0);
        cols[2].Width = two ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        cols[2].MinWidth = two ? (double)this.FindResource("SideMinWidth")! : 0;
        Grid.SetColumn(Side, two ? 2 : 0);
        Grid.SetRow(Side, two ? 0 : 1);
        Side.Margin = two ? default : new Thickness(0, 24, 0, 0);
    }

    private void OnLogin(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _login();

    /// <summary>
    /// Bấm một dòng có link: trang trường mở trong cửa sổ app (dùng chung phiên đăng nhập), trang khác bằng trình duyệt.
    /// Đợt đăng ký môn mở trang Đăng ký môn học của MyBK (danh sách các đợt; MyBK không có link riêng cho từng đợt).
    /// </summary>
    private void OnInboxTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is InboxRow r) _openInbox(r.Key);
    }

    private async void OnRowTapped(object? sender, TappedEventArgs e)
    {
        // Dòng gom nhiều môn (khảo sát...): bấm để xổ ra hay thu lại.
        if ((sender as Control)?.DataContext is TodoItem { Group: true } g)
        {
            if (!Expanded.Remove(g.Id)) Expanded.Add(g.Id);
            TodoList.ItemsSource = TodoGroups.Collapse(_todo, Expanded);
            return;
        }
        if (UrlOf((sender as Control)?.DataContext) is { Length: > 0 } url)
            await Files.Links.OpenAsync(this, url);
    }

    private static string? UrlOf(object? row) => row switch
    {
        TodoItem t => t.Url,
        NewsItem n => n.Url,
        RegItem => SoHocTap.Core.Config.Str("sources.mybk.registration"),
        _ => null,
    };
}
