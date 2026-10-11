namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Trang có tab: MainWindow ghi tab đang mở khi rời trang (config ui.tab.&lt;route&gt;) và mở lại đúng tab đó lần sau, kể cả sau khi tắt app.
/// </summary>
public interface ITabbedPage
{
    int Tab { get; set; }
}
