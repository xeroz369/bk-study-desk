using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace BKStudyDesk.Math.Tests;

internal static class Frames
{
    public static WriteableBitmap Render(Control control, int width = 400, int height = 200)
    {
        var window = new Window { Width = width, Height = height, Background = Brushes.White, Content = control };
        window.Show();
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("không chụp được frame");
        window.Close();
        var bmp = new WriteableBitmap(frame.PixelSize, frame.Dpi, frame.Format ?? Avalonia.Platform.PixelFormat.Bgra8888, frame.AlphaFormat ?? Avalonia.Platform.AlphaFormat.Premul);
        using (var fb = bmp.Lock())
            frame.CopyPixels(new PixelRect(frame.PixelSize), fb.Address, fb.RowBytes * fb.Size.Height, fb.RowBytes);
        return bmp;
    }

    public static (byte B, byte G, byte R)[,] Pixels(Control control, int width = 400, int height = 200)
    {
        using var bmp = Render(control, width, height);
        using var fb = bmp.Lock();
        var w = fb.Size.Width;
        var h = fb.Size.Height;
        var raw = new byte[fb.RowBytes * h];
        System.Runtime.InteropServices.Marshal.Copy(fb.Address, raw, 0, raw.Length);
        var rgba = fb.Format == Avalonia.Platform.PixelFormat.Rgba8888;
        var px = new (byte, byte, byte)[h, w];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = y * fb.RowBytes + x * 4;
                px[y, x] = rgba ? (raw[i + 2], raw[i + 1], raw[i]) : (raw[i], raw[i + 1], raw[i + 2]);
            }
        return px;
    }

    public static (int Top, int Bottom) InkRows((byte B, byte G, byte R)[,] px)
    {
        var rows = Enumerable.Range(0, px.GetLength(0)).Where(y => Enumerable.Range(0, px.GetLength(1)).Any(x => px[y, x].R < 128)).ToList();
        return rows.Count == 0 ? (0, -1) : (rows[0], rows[^1]);
    }

    public static int DarkPixels(Control control)
    {
        var px = Pixels(control);
        var n = 0;
        foreach (var p in px)
            if (p.R < 128) n++;
        return n;
    }
}
