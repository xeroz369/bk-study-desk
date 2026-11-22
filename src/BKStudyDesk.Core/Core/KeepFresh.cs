namespace SoHocTap.Core;

public static class KeepFresh
{
    public static bool Due(long? last, long now, int days) => now - (last ?? 0) >= Math.Max(1, days) * 86400L;

    public static long? Latest(params long?[] times) => times.Where(t => t is > 0).Max();

    public static long? Oldest(params long?[] times) => times.Any(t => t is null or <= 0) ? null : times.Min();

    public static bool AllSignedOut(IReadOnlyCollection<string> due, IReadOnlyCollection<string> signedOut) =>
        due.Count > 0 && due.All(signedOut.Contains);
}
