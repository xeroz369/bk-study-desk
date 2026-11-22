namespace SoHocTap.Ui;

internal readonly record struct Box(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

internal static class ScreenFit
{
    public static Box Clamp(Box window, Box work, double minWidth, double minHeight)
    {
        var w = Math.Max(Math.Min(window.Width, work.Width), minWidth);
        var h = Math.Max(Math.Min(window.Height, work.Height), minHeight);
        var x = Math.Max(work.X, Math.Min(window.X, work.Right - w));
        var y = Math.Max(work.Y, Math.Min(window.Y, work.Bottom - h));
        return new Box(x, y, w, h);
    }
}
