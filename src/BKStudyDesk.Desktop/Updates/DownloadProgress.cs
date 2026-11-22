namespace SoHocTap.Updates;

public sealed record DownloadState(string Version, long TotalBytes, int? Percent, bool Verified)
{
    public bool Indeterminate => Percent is null;

    public long DoneBytes => Percent is { } p && TotalBytes > 0 ? TotalBytes * p / 100 : 0;
}

public sealed class DownloadTracker(string version, long totalBytes)
{
    public const int MaxBeforeVerified = 99;
    private readonly Lock _lock = new();

    public DownloadState State { get; private set; } = new(version, Math.Max(0, totalBytes), null, false);

    public bool Report(int percent)
    {
        lock (_lock)
        {
            if (State.Verified) return false;
            var p = Math.Clamp(percent, 0, MaxBeforeVerified);
            if (State.Percent is { } cur && p <= cur) return false;
            State = State with { Percent = p };
            return true;
        }
    }

    public void Complete()
    {
        lock (_lock) State = State with { Percent = 100, Verified = true };
    }
}

public static class DownloadText
{
    public static string Mb(long bytes, IFormatProvider culture) => (bytes / 1048576.0).ToString("0.0", culture);

    public static string Describe(DownloadState s, string starting, string withSize, string noSize, IFormatProvider culture) =>
        s.Percent is not { } p ? string.Format(culture, starting, s.Version)
        : s.TotalBytes > 0 ? string.Format(culture, withSize, s.Version, p, Mb(s.DoneBytes, culture), Mb(s.TotalBytes, culture))
        : string.Format(culture, noSize, s.Version, p);
}
