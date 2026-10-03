using System.Net;
using System.Text.Json;
using SoHocTap.Data;
using SoHocTap.Sources;
using SoHocTap.Sources.Lms;

namespace SoHocTap.Tests;

public class SyncErrorsTests
{
    public static TheoryData<Exception, SyncErrorKind> Cases => new()
    {
        { new SessionExpiredException("Phiên MyBK hết hạn"), SyncErrorKind.SessionExpired },
        { new LmsException("x", "invalidtoken"), SyncErrorKind.SessionExpired },
        { new LmsException("core_course_get_contents: lỗi"), SyncErrorKind.Server },
        { new TimeoutException("LMS không phản hồi"), SyncErrorKind.Timeout },
        { new TaskCanceledException("HttpClient.Timeout", new TimeoutException()), SyncErrorKind.Timeout },
        { new HttpRequestException("giới hạn", null, HttpStatusCode.TooManyRequests), SyncErrorKind.Throttled },
        { new HttpRequestException("bảo trì", null, HttpStatusCode.ServiceUnavailable), SyncErrorKind.Throttled },
        { new HttpRequestException("lỗi", null, HttpStatusCode.InternalServerError), SyncErrorKind.Server },
        { new HttpRequestException("trang lỗi", null, HttpStatusCode.OK), SyncErrorKind.Server },
        { new HttpRequestException("Không kết nối được LMS", new IOException()), SyncErrorKind.Network },
        { new IOException("The process cannot access the file because it is being used by another process."), SyncErrorKind.FileBusy },
        { new UnauthorizedAccessException("Access to the path is denied."), SyncErrorKind.FileBusy },
        { new JsonException("bad"), SyncErrorKind.Data },
        { new SyncException(SyncErrorKind.Data, "thiếu userid"), SyncErrorKind.Data },
        { new AggregateException(new TimeoutException()), SyncErrorKind.Timeout },
        { new InvalidOperationException("lạ"), SyncErrorKind.Bug },
        { new NullReferenceException(), SyncErrorKind.Bug },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Classify(Exception e, SyncErrorKind kind) => Assert.Equal(kind, SyncErrors.Classify(e));

    [Fact]
    public void LangKey_EveryKindHasKey()
    {
        foreach (var k in Enum.GetValues<SyncErrorKind>()) Assert.StartsWith("error.", SyncErrorText.LangKey(k), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SessionExpired", SyncErrorKind.SessionExpired)]
    [InlineData("Network", SyncErrorKind.Network)]
    public void Parse_KnownNames(string name, SyncErrorKind kind) => Assert.Equal(kind, SyncErrorText.Parse(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("network")]
    [InlineData("Cosmic")]
    public void Parse_UnknownIsNull(string? name) => Assert.Null(SyncErrorText.Parse(name));
}
