using Avalonia;
using Avalonia.Controls;
using SoHocTap.Core;

namespace BKStudyDesk.Desktop.Web;

/// <summary>
/// Trang web chạy ẩn (MyBK, giữ phiên SSO): một cửa sổ không lên taskbar, không lấy focus, đặt ngoài màn hình, chứa một WebPage.
/// Trình duyệt nhúng chỉ chạy khi gắn vào một cửa sổ thật nên không dùng control ẩn. Đóng sau mỗi lượt để tắt process trình duyệt
/// (đỡ tốn pin); lần sau tự tạo lại, cookie vẫn còn trong profile.
/// Trình duyệt nhúng bị hủy mà app không đóng (cửa sổ bị đóng, control rời cửa sổ: thấy một lần giữa lượt MyBK 09/10/2026, chưa rõ
/// nguyên nhân) thì bỏ trang đó và ghi log kèm stack: lần Open sau tạo trang mới thay vì chạy script trên trang đã chết.
/// </summary>
internal sealed class HiddenWeb
{
    private Window? _window;
    private WebPage? _page;

    /// <summary>Trang đang mở, hay tạo mới.</summary>
    public WebPage Open(Func<Uri, bool>? allow)
    {
        if (_page is not null) return _page;
        var page = _page = new WebPage { Allow = allow };
        var window = _window = new Window
        {
            Title = "BK Study Desk (nền)", Width = 1024, Height = 768, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Content = page.View,
            Opacity = 0,   // trong suốt: dù nằm đâu cũng không thấy
            Classes = { "offscreen" },   // App: không kẹp vào màn hình như cửa sổ phụ
        };
        // Avalonia 12 bỏ qua Position đặt trước Show (cửa sổ hiện ở góc 0,0: người dùng thấy cửa sổ trắng lóe lên mỗi lượt
        // MyBK, giữ phiên; đo 10/10/2026). Đặt ra ngoài màn hình ngay khi mở; Opacity 0 che khung hình đầu.
        window.Opened += (_, _) => window.Position = new PixelPoint(-30000, -30000);
        // Rời cửa sổ báo ngay trong lời gọi gây ra (stack có người gọi); hủy trình duyệt thì Avalonia làm sau, qua dispatcher.
        page.View.DetachedFromVisualTree += (_, _) => Lost(page, "trình duyệt nhúng rời cửa sổ", closeWindow: true);
        page.View.AdapterDestroyed += (_, _) => Lost(page, "trình duyệt nhúng bị hủy", closeWindow: true);
        window.Closed += (_, _) => Lost(page, "cửa sổ bị đóng", closeWindow: false);
        window.Show();
        return page;
    }

    public bool IsOpen => _page is not null;

    /// <summary>Trang này còn là trang đang mở (chưa đóng, chưa bị hủy).</summary>
    public bool Holds(WebPage page) => ReferenceEquals(_page, page);

    public void Close()
    {
        var window = _window;
        _window = null;
        _page = null;   // bỏ trước khi đóng: Lost bên dưới biết đây là app tự đóng
        window?.Close();
    }

    private void Lost(WebPage page, string why, bool closeWindow)
    {
        if (!Holds(page)) return;
        Log.Warn($"Web ẩn: {why} ngoài ý app, lần sau mở trang mới\n{Environment.StackTrace}");
        if (closeWindow) Close();
        else { _window = null; _page = null; }
    }
}
