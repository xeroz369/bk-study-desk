namespace SoHocTap.Ui;

/// <summary>Hình chữ nhật (đơn vị tùy chỗ dùng: DIP hoặc pixel). Không dùng System.Windows.Rect để test chạy được không cần WPF.</summary>
internal readonly record struct Box(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

/// <summary>Đặt cửa sổ vào trong vùng làm việc của một màn hình (hàm thuần, test ở WindowFitTests).</summary>
internal static class ScreenFit
{
    /// <summary>
    /// Không lớn hơn vùng làm việc (nhưng không nhỏ hơn cỡ tối thiểu của cửa sổ), mép phải/dưới không tràn ra ngoài, mép trái/trên không
    /// lọt ra trước vùng làm việc. Cửa sổ nằm ngoài vùng (màn hình phụ đã rút) thì bị kéo hẳn vào trong.
    /// </summary>
    public static Box Clamp(Box window, Box work, double minWidth, double minHeight)
    {
        var w = Math.Max(Math.Min(window.Width, work.Width), minWidth);
        var h = Math.Max(Math.Min(window.Height, work.Height), minHeight);
        var x = Math.Max(work.X, Math.Min(window.X, work.Right - w));
        var y = Math.Max(work.Y, Math.Min(window.Y, work.Bottom - h));
        return new Box(x, y, w, h);
    }
}
