using Avalonia;
using Avalonia.Controls;
using SoHocTap.Core;

namespace BKStudyDesk.Desktop.Web;

internal sealed class HiddenWeb
{
    private Window? _window;
    private WebPage? _page;

    public WebPage Open(Func<Uri, bool>? allow)
    {
        if (_page is not null) return _page;
        var page = _page = new WebPage { Allow = allow };
        var window = _window = new Window
        {
            Title = "BK Study Desk (nền)", Width = 1024, Height = 768, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Content = page.View,
            Opacity = 0,
            Classes = { "offscreen" },
        };
        window.Opened += (_, _) => window.Position = new PixelPoint(-30000, -30000);
        page.View.DetachedFromVisualTree += (_, _) => Lost(page, "trình duyệt nhúng rời cửa sổ", closeWindow: true);
        page.View.AdapterDestroyed += (_, _) => Lost(page, "trình duyệt nhúng bị hủy", closeWindow: true);
        window.Closed += (_, _) => Lost(page, "cửa sổ bị đóng", closeWindow: false);
        window.Show();
        return page;
    }

    public bool IsOpen => _page is not null;

    public bool Holds(WebPage page) => ReferenceEquals(_page, page);

    public void Close()
    {
        var window = _window;
        _window = null;
        _page = null;
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
