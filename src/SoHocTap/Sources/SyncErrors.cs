using System.Net;
using System.Text.Json;
using SoHocTap.Data;
using SoHocTap.Sources.Lms;

namespace SoHocTap.Sources;

/// <summary>Xếp exception của một lượt đồng bộ vào <see cref="SyncErrorKind"/>. Hàm thuần, test được.</summary>
public static class SyncErrors
{
    public static SyncErrorKind Classify(Exception e) => e switch
    {
        SyncException s => s.Kind,
        SessionExpiredException => SyncErrorKind.SessionExpired,
        LmsException { Code: "invalidtoken" or "accessexception" } => SyncErrorKind.SessionExpired,
        // Moodle trả về lỗi web service: server có trả lời, chỉ là báo lỗi.
        LmsException => SyncErrorKind.Server,
        TimeoutException => SyncErrorKind.Timeout,
        // HttpClient hết Timeout thì ném TaskCanceledException có inner TimeoutException (không phải do người dùng hủy).
        TaskCanceledException { InnerException: TimeoutException } => SyncErrorKind.Timeout,
        HttpRequestException h => Http(h),
        // Sharing violation (PDF đang mở), thiếu quyền ghi, ổ đầy: đều là chuyện trên máy, không phải server.
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
        // Không có mã HTTP: không tới được server (socket, DNS, TLS), hoặc WebView không mở được trang.
        null => SyncErrorKind.Network,
    };
}
