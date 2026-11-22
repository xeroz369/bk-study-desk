using Avalonia;
using Avalonia.Controls;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Platform;

internal sealed class WindowPlacement
{
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public double Width { get; set; } = double.NaN;
    public double Height { get; set; } = double.NaN;
    public bool Maximized { get; set; }
    public string Page { get; set; } = "";

    private static string FilePath => Paths.DataFile("window.json");

    public static WindowPlacement Load()
    {
        var n = JsonStore.Read(FilePath);
        try { return n is null ? new() : Js.Deserialize<WindowPlacement>(n, DesktopJsonOptions.Store) ?? new(); }
        catch (System.Text.Json.JsonException) { return new(); }
    }

    public void Save()
    {
        try { JsonStore.Write(FilePath, Js.SerializeToNode(this, DesktopJsonOptions.Store)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Warn($"Lưu vị trí cửa sổ: {e.Message}"); }
    }

    public static void FitDialog(Window w)
    {
        if ((w.Screens.ScreenFromWindow(w) ?? w.Screens.Primary) is not { } screen) return;
        var (s, area) = (screen.Scaling, screen.WorkingArea);
        var now = new Box(w.Position.X / s, w.Position.Y / s, w.Bounds.Width, w.Bounds.Height);
        var fit = ScreenFit.Clamp(now, new Box(area.X / s, area.Y / s, area.Width / s, area.Height / s), w.MinWidth, w.MinHeight);
        if (fit == now) return;
        if (fit.Width < now.Width) w.Width = fit.Width;
        if (fit.Height < now.Height) w.Height = fit.Height;
        w.Position = new PixelPoint((int)Math.Round(fit.X * s), (int)Math.Round(fit.Y * s));
    }

    public void Apply(Window w)
    {
        var known = !double.IsNaN(X) && !double.IsNaN(Y) && !double.IsNaN(Width) && !double.IsNaN(Height);
        var size = known ? new Box(X, Y, Width, Height) : new Box(0, 0, w.Width, w.Height);
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

    public void Track(Window w) => w.PositionChanged += (_, e) => { if (w.WindowState == WindowState.Normal) _moved = e.Point; };

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
