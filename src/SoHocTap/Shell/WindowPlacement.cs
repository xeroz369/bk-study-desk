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
        w.Width = Math.Max(Width, w.MinWidth);
        w.Height = Math.Max(Height, w.MinHeight);
        // Vị trí cũ nằm ngoài mọi màn hình (vd. đã rút màn hình phụ) thì bỏ.
        bool visible = !double.IsNaN(X) && X >= SystemParameters.VirtualScreenLeft - 50 && Y >= SystemParameters.VirtualScreenTop - 50
                       && X < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100
                       && Y < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100;
        if (visible)
        {
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Left = X;
            w.Top = Y;
        }
        else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (Maximized) w.WindowState = WindowState.Maximized;
    }

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
    private static string SignalName => AppInfo.Id + ".show";

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
