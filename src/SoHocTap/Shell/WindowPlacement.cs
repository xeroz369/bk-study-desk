using System.Windows;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Shell;

/// <summary>Nhớ vị trí, kích thước và trạng thái maximize của window chính (data/window.json).</summary>
internal sealed class WindowPlacement
{
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 800;
    public bool Maximized { get; set; }
    /// <summary>Page đang mở lúc đóng app, lần sau mở lại đúng page đó.</summary>
    public string Page { get; set; } = "";

    private static string FilePath => Paths.DataFile("window.json");

    public static WindowPlacement Load()
    {
        var n = JsonStore.Read(FilePath);
        try { return n is null ? new() : System.Text.Json.JsonSerializer.Deserialize<WindowPlacement>(n, JsonStore.Options) ?? new(); }
        catch (System.Text.Json.JsonException) { return new(); }
    }

    public void Save() => JsonStore.Write(FilePath, this);

    public void Apply(Window w)
    {
        if (double.IsNaN(X) || double.IsNaN(Y) || double.IsNaN(Width) || double.IsNaN(Height))
        {
            // Lần đầu: giữa màn hình chính, không lớn hơn vùng làm việc.
            var area = SystemParameters.WorkArea;
            w.Width = Math.Max(Math.Min(Width, area.Width), w.MinWidth);
            w.Height = Math.Max(Math.Min(Height, area.Height), w.MinHeight);
            w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        else
        {
            // Kẹp theo màn hình đang chứa vị trí đã lưu (MonitorFromRect), không theo màn hình chính: mở trên màn hình phụ thì vẫn ở đó.
            // Màn hình đó đã rút ra thì MONITOR_DEFAULTTONEAREST trả màn hình gần nhất, cửa sổ được kéo vào trong, không mất ngoài vùng nhìn.
            var fit = ScreenFit.Clamp(new Box(X, Y, Width, Height), WorkAreaNear(new Box(X, Y, Width, Height)), w.MinWidth, w.MinHeight);
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            (w.Left, w.Top, w.Width, w.Height) = (fit.X, fit.Y, fit.Width, fit.Height);
        }
        if (Maximized) w.WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// Cửa sổ phụ cỡ cố định (LMS, MyBK, đăng nhập, tải tài liệu): không lớn hơn vùng làm việc của màn hình đang có cửa sổ chính,
    /// chừa lề <paramref name="margin"/>. Màn hình nhỏ hoặc scale lớn (vd. 3072x1800 ở 250% chỉ còn 1229x720 DIP) mà mở 1200x820 thì
    /// thanh tiêu đề hay mép dưới bị khuất. Gọi trong constructor, trước khi hiện (WindowStartupLocation căn giữa theo cỡ đã sửa).
    /// </summary>
    public static void FitToWorkArea(Window w, double margin = 24)
    {
        var main = Application.Current?.MainWindow;
        var near = main is { IsLoaded: true } && !double.IsNaN(main.Left) ? new Box(main.Left, main.Top, main.ActualWidth, main.ActualHeight) : new Box(0, 0, 1, 1);
        var area = WorkAreaNear(near);
        var maxW = Math.Max(320, area.Width - 2 * margin);
        var maxH = Math.Max(240, area.Height - 2 * margin);
        if (w.MinWidth > maxW) w.MinWidth = maxW;
        if (w.MinHeight > maxH) w.MinHeight = maxH;
        if (!double.IsNaN(w.Width)) w.Width = Math.Min(w.Width, maxW);
        if (!double.IsNaN(w.Height)) w.Height = Math.Min(w.Height, maxH);
        w.MaxHeight = Math.Min(w.MaxHeight, area.Height);   // SizeToContent cũng không cao quá màn hình
    }

    /// <summary>
    /// Vùng làm việc (trừ taskbar) của màn hình chứa phần lớn <paramref name="dip"/>, hoặc màn hình gần nhất. Left/Top của cửa sổ WPF là DIP
    /// theo DPI hệ thống, còn Win32 dùng pixel: đổi qua lại bằng GetDpiForSystem.
    /// </summary>
    private static Box WorkAreaNear(Box dip)
    {
        var scale = GetDpiForSystem() / 96.0;
        if (scale <= 0) scale = 1;
        var px = new RECT((int)Math.Round(dip.X * scale), (int)Math.Round(dip.Y * scale), (int)Math.Round(dip.Right * scale), (int)Math.Round(dip.Bottom * scale));
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        var monitor = MonitorFromRect(ref px, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            var a = SystemParameters.WorkArea;
            return new Box(a.Left, a.Top, a.Width, a.Height);
        }
        var r = info.rcWork;
        return new Box(r.Left / scale, r.Top / scale, (r.Right - r.Left) / scale, (r.Bottom - r.Top) / scale);
    }

    private const uint MonitorDefaultToNearest = 2;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT(int left, int top, int right, int bottom)
    {
        public int Left = left, Top = top, Right = right, Bottom = bottom;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    public void Capture(Window w)
    {
        Maximized = w.WindowState == WindowState.Maximized;
        var b = w.WindowState == WindowState.Normal ? new Rect(w.Left, w.Top, w.Width, w.Height) : w.RestoreBounds;
        (X, Y, Width, Height) = (b.X, b.Y, b.Width, b.Height);
    }
}

/// <summary>
/// Chỉ cho chạy một instance. Instance thứ hai set event có tên "<id app>.show" rồi thoát; instance đang chạy (kể cả khi chỉ nằm
/// dưới tray, window đang ẩn) nghe event đó và hiện window lên.
/// </summary>
internal static class SingleInstance
{
    private static string SignalName => AppInfo.InstanceKey + ".show";

    public static void ActivateExisting()
    {
        try
        {
            using var signal = EventWaitHandle.OpenExisting(SignalName);
            signal.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { }
    }

    /// <summary>Instance đang chạy: gọi show mỗi khi có instance thứ hai được mở.</summary>
    public static void Listen(Action show)
    {
        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName);
        var t = new Thread(() =>
        {
            while (signal.WaitOne()) show();
        })
        { IsBackground = true, Name = "single-instance" };
        t.Start();
    }
}
