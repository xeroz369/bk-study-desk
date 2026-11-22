using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public static class SyncStatus
{
    public readonly record struct Bar(bool Visible, double? Fraction);

    public static Bar Progress(IEnumerable<(bool Syncing, Sources.SyncProgress? Step)> sources)
    {
        var running = sources.Where(s => s.Syncing).ToList();
        if (running.Count == 0) return new(false, null);
        return running.All(s => s.Step?.Permille is not null)
            ? new(true, running.Average(s => s.Step!.Value.Permille!.Value) / 1000.0)
            : new(true, null);
    }

    public static string Text(bool syncing, bool failed, long? syncedAt, long now)
    {
        if (syncing) return L.T("sync.syncing");
        if (failed) return L.T("sync.failed");
        if (syncedAt is not { } t) return L.T("sync.never");
        var at = VnTime.ToWall(t);
        return L.F("sync.at", at.Date == VnTime.ToWall(now).Date ? at.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) : at.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture));
    }
}
