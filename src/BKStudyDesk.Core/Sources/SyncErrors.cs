using System.Net;
using System.Text.Json;
using SoHocTap.Sources.Lms;

namespace SoHocTap.Sources;

public static class SyncErrors
{
    public static SyncErrorKind Classify(Exception e) => e switch
    {
        SyncException s => s.Kind,
        SessionExpiredException => SyncErrorKind.SessionExpired,
        LmsException { Code: "invalidtoken" or "accessexception" } => SyncErrorKind.SessionExpired,
        LmsException => SyncErrorKind.Server,
        TimeoutException => SyncErrorKind.Timeout,
        TaskCanceledException { InnerException: TimeoutException } => SyncErrorKind.Timeout,
        HttpRequestException h => Http(h),
        UnauthorizedAccessException or IOException => SyncErrorKind.FileBusy,
        JsonException or FormatException or InvalidCastException => SyncErrorKind.Data,
        AggregateException { InnerExceptions.Count: 1 } a => Classify(a.InnerExceptions[0]),
        _ => SyncErrorKind.Bug,
    };

    private static SyncErrorKind Http(HttpRequestException h) => h.StatusCode switch
    {
        HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable => SyncErrorKind.Throttled,
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => SyncErrorKind.SessionExpired,
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => SyncErrorKind.Timeout,
        not null => SyncErrorKind.Server,
        null => SyncErrorKind.Network,
    };
}
