namespace SoHocTap.Ui;

/// <summary>Phép tính vị trí trên lưới tuần, tách khỏi WPF để unit test được.</summary>
public static class WeekMath
{
    /// <summary>
    /// Vạch "bây giờ": cột ngày (0 = Thứ hai) và tọa độ y trong phần thân lưới, theo đúng phút hiện tại.
    /// null khi tuần đang xem không chứa <paramref name="now"/> hoặc giờ hiện tại nằm ngoài khung giờ [from, to] của lưới
    /// (vẽ ở mép trên/dưới thì dễ đọc nhầm thành 7:00 hay 17:00).
    /// </summary>
    public static (int Column, double Y)? NowLine(DateTime monday, DateTime now, int fromMin, int toMin, double pxPerMin, double pad)
    {
        var column = (int)Math.Floor((now.Date - monday.Date).TotalDays);
        if (column is < 0 or > 6) return null;
        var minute = now.Hour * 60 + now.Minute;
        if (minute < fromMin || minute > toMin) return null;
        return (column, pad + (minute - fromMin) * pxPerMin);
    }

    /// <summary>Thời gian chờ tới đầu phút kế tiếp (cộng chút dư để tick rơi vào phút mới chứ không phải 59.999 giây).</summary>
    public static TimeSpan UntilNextMinute(DateTime now) =>
        TimeSpan.FromMilliseconds(60_000 - (now.Second * 1000 + now.Millisecond) + 50);
}
