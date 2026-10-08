using Avalonia;
using Avalonia.Controls;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Platform;

/// <summary>
/// Nhớ vị trí, cỡ, trạng thái phóng to của cửa sổ chính và trang đang xem (data/window.json, cùng file và cùng đơn vị DIP với 1.x,
/// Shell/WindowPlacement). Mở lại thì kẹp vào vùng làm việc của màn hình đang chứa vị trí đã lưu (ScreenFit.Clamp): màn hình phụ đã rút
/// thì cửa sổ được kéo vào màn hình còn lại, không mất ngoài vùng nhìn.
/// </summary>
internal sealed class WindowPlacement
{
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public double Width { get; set; } = double.NaN;
    public double Height { get; set; } = double.NaN;
    public bool Maximized { get; set; }
    /// <summary>Route của trang đang mở lúc đóng app ("lich", "thu-vien"...), lần sau mở lại đúng trang đó.</summary>
    public string Page { get; set; } = "";

    private static string FilePath => Paths.DataFile("window.json");

    public static WindowPlacement Load()
    {
        var n = JsonStore.Read(FilePath);
        try { return n is null ? new() : System.Text.Json.JsonSerializer.Deserialize<WindowPlacement>(n, JsonStore.Options) ?? new(); }
        catch (System.Text.Json.JsonException) { return new(); }
    }

    public void Save()
    {
        try { JsonStore.Write(FilePath, this); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Warn($"Lưu vị trí cửa sổ: {e.Message}"); }
    }

    /// <summary>Trước khi hiện cửa sổ. Lần đầu (chưa lưu) thì giữ cỡ trong XAML, căn giữa màn hình, không lớn hơn vùng làm việc.</summary>
    public void Apply(Window w)
    {
        var known = !double.IsNaN(X) && !double.IsNaN(Y) && !double.IsNaN(Width) && !double.IsNaN(Height);
        var size = known ? new Box(X, Y, Width, Height) : new Box(0, 0, w.Width, w.Height);
        // Màn hình chứa góc trên trái đã lưu (so theo DIP của từng màn hình); không còn màn hình đó thì màn hình chính.
        var screen = (known ? w.Screens.All.FirstOrDefault(sc =>
        {
            var (b, s) = (sc.Bounds, sc.Scaling);
            return X >= b.X / s && X < (b.X + b.Width) / s && Y >= b.Y / s && Y < (b.Y + b.Height) / s;
        }) : null) ?? w.Screens.Primary;
        if (screen is not null)
        {
            var s = screen.Scaling;
            var area = screen.WorkingArea;
            var work = new Box(area.X / s, area.Y / s, area.Width / s, area.Height / s);
            var fit = ScreenFit.Clamp(size, work, w.MinWidth, w.MinHeight);
            (w.Width, w.Height) = (fit.Width, fit.Height);
            if (known)
            {
                w.WindowStartupLocation = WindowStartupLocation.Manual;
                w.Position = new PixelPoint((int)Math.Round(fit.X * s), (int)Math.Round(fit.Y * s));
            }
            else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        if (Maximized) w.WindowState = WindowState.Maximized;
    }

    private PixelPoint? _moved;

    /// <summary>
    /// Theo dõi vị trí: Window.Position của Avalonia không đổi theo khi người dùng kéo cửa sổ (đo trên Windows 11: dời tới 300,200 mà
    /// Position vẫn 0,0), nên lấy từ PositionChanged, chỉ khi cửa sổ ở trạng thái bình thường.
    /// </summary>
    public void Track(Window w) => w.PositionChanged += (_, e) => { if (w.WindowState == WindowState.Normal) _moved = e.Point; };

    /// <summary>Lúc đóng hay thu xuống khay. Đang phóng to thì giữ cỡ, vị trí bình thường lần trước (khôi phục về đúng chỗ đó).</summary>
    public void Capture(Window w, string page)
    {
        Page = page;
        Maximized = w.WindowState == WindowState.Maximized;
        if (w.WindowState != WindowState.Normal) return;
        var s = w.DesktopScaling;
        var at = _moved ?? w.Position;
        (X, Y, Width, Height) = (at.X / s, at.Y / s, w.Width, w.Height);
    }
}
