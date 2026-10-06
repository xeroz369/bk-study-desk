using System.Globalization;

namespace SoHocTap.Core;

/// <summary>
/// Gom nhắc hạn vào vài giờ cố định trong ngày (notify.digestTimes, giờ VN) thay vì báo rời từng bài: nhận thông báo theo lịch
/// 3 lần mỗi ngày giúp đỡ căng thẳng, tập trung hơn; gom theo từng giờ thì gần như không có lợi (Fitz và cộng sự 2019).
/// Bài sắp tới hạn (dưới <c>immediateHours</c>) không chờ giờ gom. Hàm thuần, không đọc cấu hình.
/// </summary>
public static class NotifyDigest
{
    /// <summary>"07:00, 12:00, 19:00" thành phút trong ngày, đã sắp và bỏ trùng. Chuỗi rỗng là tắt gom (mảng rỗng); sai dạng thì null.</summary>
    public static int[]? Parse(string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var slots = new SortedSet<int>();
        foreach (var p in parts)
        {
            if (!TimeOnly.TryParseExact(p, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return null;
            slots.Add(t.Hour * 60 + t.Minute);
        }
        return [.. slots];
    }

    public static string Format(int[] slots) => string.Join(", ", slots.Select(m => $"{m / 60:00}:{m % 60:00}"));

    /// <summary>
    /// Tới lượt gom chưa: giờ gom gần nhất đã qua (hôm nay hoặc hôm qua) muộn hơn lần gom trước. Không có giờ gom thì luôn mở.
    /// Tắt app qua giờ gom thì lần kiểm tra đầu tiên sau đó gom bù.
    /// </summary>
    public static bool GateOpen(long now, int[] slots, long lastDigest)
    {
        if (slots.Length == 0) return true;
        var today = VnTime.ToWall(now).Date;
        var latest = slots.Select(m => VnTime.FromWall(today.AddMinutes(m))).Where(t => t <= now).DefaultIfEmpty(VnTime.FromWall(today.AddDays(-1).AddMinutes(slots[^1]))).Max();
        return latest > lastDigest;
    }

    /// <summary>Bài này chờ giờ gom (còn từ <paramref name="immediateHours"/> giờ trở lên), hay báo ngay.</summary>
    public static bool Hold(long due, long now, int immediateHours) => due - now >= immediateHours * 3600L;
}
