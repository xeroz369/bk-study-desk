using System.Globalization;

namespace SoHocTap.Core;

public static class NotifyDigest
{
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

    public static bool GateOpen(long now, int[] slots, long lastDigest)
    {
        if (slots.Length == 0) return true;
        var today = VnTime.ToWall(now).Date;
        var latest = slots.Select(m => VnTime.FromWall(today.AddMinutes(m))).Where(t => t <= now).DefaultIfEmpty(VnTime.FromWall(today.AddDays(-1).AddMinutes(slots[^1]))).Max();
        return latest > lastDigest;
    }

    public static long NextMorning(long now, int morning)
    {
        var at = VnTime.FromWall(VnTime.ToWall(now).Date.AddMinutes(morning));
        return at > now ? at : VnTime.FromWall(VnTime.ToWall(now).Date.AddDays(1).AddMinutes(morning));
    }

    public static bool Hold(long due, long now, int immediateHours) => due - now >= immediateHours * 3600L;
}
