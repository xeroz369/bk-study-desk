namespace SoHocTap.Data;

public enum SyncErrorKind
{
    SessionExpired,
    Network,
    Server,
    Throttled,
    Timeout,
    FileBusy,
    Data,
    Bug,
}

public sealed class SyncException(SyncErrorKind kind, string message, Exception? inner = null) : Exception(message, inner)
{
    public SyncErrorKind Kind { get; } = kind;
}

public static class SyncErrorText
{
    public static string LangKey(SyncErrorKind kind) => kind switch
    {
        SyncErrorKind.SessionExpired => "error.sessionExpired",
        SyncErrorKind.Network => "error.network",
        SyncErrorKind.Server => "error.server",
        SyncErrorKind.Throttled => "error.throttled",
        SyncErrorKind.Timeout => "error.timeout",
        SyncErrorKind.FileBusy => "error.fileBusy",
        SyncErrorKind.Data => "error.data",
        _ => "error.generic",
    };

    public static SyncErrorKind? Parse(string? name) =>
        Enum.TryParse<SyncErrorKind>(name, ignoreCase: false, out var k) && Enum.IsDefined(k) ? k : null;
}
