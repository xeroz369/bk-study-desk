using Avalonia;
using Avalonia.Controls;

namespace BKStudyDesk.Desktop.Web;

/// <summary>
/// Trang web chạy ẩn (MyBK, giữ phiên SSO): một cửa sổ không lên taskbar, không lấy focus, đặt ngoài màn hình, chứa một WebPage.
/// Trình duyệt nhúng chỉ chạy khi gắn vào một cửa sổ thật nên không dùng control ẩn. Đóng sau mỗi lượt để tắt process trình duyệt
/// (đỡ tốn pin); lần sau tự tạo lại, cookie vẫn còn trong profile.
/// </summary>
internal sealed class HiddenWeb
{
    private Window? _window;
    private WebPage? _page;

    /// <summary>Trang đang mở, hay tạo mới.</summary>
    public WebPage Open(Func<Uri, bool>? allow)
    {
        if (_page is not null) return _page;
        _page = new WebPage { Allow = allow };
        _window = new Window
        {
            Title = "BK Study Desk (nền)", Width = 1024, Height = 768, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Position = new PixelPoint(-30000, -30000), Content = _page.View,
            Classes = { "offscreen" },   // App: không kẹp vào màn hình như cửa sổ phụ
        };
        _window.Show();
        return _page;
    }

    public bool IsOpen => _page is not null;

    public void Close()
    {
        _window?.Close();
        _window = null;
        _page = null;
    }
}
