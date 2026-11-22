namespace SoHocTap.Core;

public enum UpdateMode { Ask, Notify, Auto, Off }

public enum ApplyOutcome { None, Updated, Failed }

public static class UpdatePolicy
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromHours(6);

    public static UpdateMode ParseMode(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "notify" => UpdateMode.Notify,
        "auto" => UpdateMode.Auto,
        "off" => UpdateMode.Off,
        _ => UpdateMode.Ask,
    };

    public static TimeSpan Interval(int checkHours) => TimeSpan.FromHours(checkHours) < MinInterval ? MinInterval : TimeSpan.FromHours(checkHours);

    public static bool ShouldCheck(UpdateMode mode, InstallKind kind, DateTimeOffset now, DateTimeOffset? lastCheck, int checkHours)
    {
        if (kind == InstallKind.Store || mode is UpdateMode.Ask or UpdateMode.Off) return false;
        if (lastCheck is not { } last || last > now) return true;
        return now - last >= Interval(checkHours);
    }

    public static bool IsNewer(string candidate, string current)
    {
        static Version? V(string s) => Version.TryParse(s.Trim().TrimStart('v', 'V').Split('-', '+')[0], out var v) ? v : null;
        return V(candidate) is { } c && (V(current) is not { } cur || c > cur);
    }

    public static bool IsValidSource(string src, Func<string, bool> dirExists) =>
        src.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
        || (Path.IsPathFullyQualified(src) && !src.Contains("://", StringComparison.Ordinal) && dirExists(src));

    public static bool IsLoopbackHttp(string src) =>
        Uri.TryCreate(src, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttp && u.IsLoopback && u.Host == "127.0.0.1";

    public static ApplyOutcome ApplyResult(string? applying, string current) =>
        string.IsNullOrWhiteSpace(applying) ? ApplyOutcome.None
        : IsNewer(applying, current) ? ApplyOutcome.Failed : ApplyOutcome.Updated;

    public static bool CanDownload(bool batterySaver) => !batterySaver;

    public static readonly IReadOnlyList<int> CheckHourChoices = [6, 12, 24];

    public static int NearestChoice(int hours) => CheckHourChoices.MinBy(h => Math.Abs(h - hours));

    public static readonly TimeSpan WakeGap = TimeSpan.FromHours(1);

    public static bool WakeCheckAllowed(DateTimeOffset? lastWakeCheck, DateTimeOffset now) =>
        lastWakeCheck is not { } last || last > now || now - last >= WakeGap;

    public static bool ApplyOnStartup(UpdateMode mode, InstallKind kind, IEnumerable<string> args, bool otherInstanceRunning, bool restartedByUpdater = false) =>
        mode == UpdateMode.Auto && kind == InstallKind.Installed && !otherInstanceRunning && !restartedByUpdater
        && !args.Any(a => a.StartsWith("--veloapp", StringComparison.OrdinalIgnoreCase));

    public static ApplyPlan? PlanApply(ApplyTrigger trigger, InstallKind kind, UpdateMode mode, bool downloaded, bool alreadyLaunched)
    {
        if (kind != InstallKind.Installed || !downloaded || alreadyLaunched) return null;
        return trigger switch
        {
            ApplyTrigger.UserRestart => new ApplyPlan(Silent: true, Restart: true),
            ApplyTrigger.Startup when mode == UpdateMode.Auto => new ApplyPlan(Silent: true, Restart: true),
            ApplyTrigger.AppExit when mode == UpdateMode.Auto => new ApplyPlan(Silent: true, Restart: false),
            _ => null,
        };
    }
}

public enum ApplyTrigger { UserRestart, AppExit, Startup }

public readonly record struct ApplyPlan(bool Silent, bool Restart);
