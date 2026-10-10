using System.Text.Json.Nodes;
using SoHocTap.Sources;
using SoHocTap.Sources.Mybk;

namespace SoHocTap.Tests;

/// <summary>Đọc một phản hồi API MyBK: dữ liệu, lý do lỗi, có nên gọi lại một lần không (lỗi chương trình đào tạo của người dùng, 09/10/2026).</summary>
public class MybkReplyTests
{
    private static FetchResult R(int status, string body, bool token = true) => new(status, body, token);

    [Theory]
    [InlineData("""{"code":"200","data":{"x":1}}""")]
    [InlineData("""{"code":204,"data":null}""")]
    public void Ok_codes_return_data(string body)
    {
        var r = MybkReply.Read(R(200, body));
        Assert.Null(r.Why);
        Assert.False(r.Retry);
    }

    [Theory]
    [InlineData(500, "", true)]                                   // server lỗi: thoáng qua
    [InlineData(0, "", true)]                                     // mất kết nối, fetch lỗi
    [InlineData(200, "<html>bảo trì</html>", true)]               // không phải JSON
    [InlineData(200, """{"code":"500","msg":"Lỗi hệ thống"}""", true)]
    [InlineData(503, "", false)]                                  // server bảo nghỉ: không dồn thêm request
    [InlineData(429, "", false)]
    [InlineData(404, "", false)]                                  // sai đường dẫn: gọi lại cũng vậy
    public void Failures_say_why_and_whether_to_retry(int status, string body, bool retry)
    {
        var r = MybkReply.Read(R(status, body));
        Assert.NotNull(r.Why);
        Assert.Equal(retry, r.Retry);
    }

    /// <summary>Trình duyệt giả: mỗi tên API trả lần lượt các phản hồi cho trước (hết danh sách thì lặp phản hồi cuối); đếm số lần gọi.</summary>
    private sealed class FakeRunner(Dictionary<string, FetchResult[]> replies) : IBrowserRunner
    {
        public Dictionary<string, int> Calls { get; } = [];

        public Task<IReadOnlyDictionary<string, FetchResult>> FetchAsync(IReadOnlyList<FetchRequest> requests, CancellationToken ct, Action<string>? onEach = null)
        {
            var res = new Dictionary<string, FetchResult>();
            foreach (var q in requests)
            {
                var i = Calls[q.Name] = Calls.GetValueOrDefault(q.Name) + 1;
                res[q.Name] = replies[q.Name][Math.Min(i, replies[q.Name].Length) - 1];
            }
            return Task.FromResult<IReadOnlyDictionary<string, FetchResult>>(res);
        }

        public Task<string> PageAsync(string url, CancellationToken ct, string? mustContain = null) => Task.FromResult("");
    }

    private static readonly FetchResult Good = new(200, """{"code":"200","data":[1]}""", true);
    private static readonly FetchResult Broken = new(200, """{"code":"500","msg":"Lỗi hệ thống"}""", true);

    private static async Task<(Dictionary<string, FetchResult> Got, IReadOnlyList<string> Retried, FakeRunner Runner)> Run(FetchResult[] info, FetchResult[] list)
    {
        var runner = new FakeRunner(new() { ["curriculumInfo"] = info, ["curriculum"] = list });
        var (got, retried) = await MybkReply.FetchAsync(runner, [new("curriculumInfo", "a"), new("curriculum", "b")], TimeSpan.Zero, CancellationToken.None);
        return (got, retried, runner);
    }

    [Fact]
    public async Task Transient_error_is_retried_once()
    {
        var (got, retried, runner) = await Run([Good], [Broken, Good]);
        Assert.Null(MybkReply.Read(got["curriculum"]).Why);
        Assert.Equal(["curriculum"], retried);
        Assert.Equal(2, runner.Calls["curriculum"]);
        Assert.Equal(1, runner.Calls["curriculumInfo"]);   // API đã đọc được thì không gọi lại
    }

    [Fact]
    public async Task Lasting_error_stays_after_one_retry()
    {
        var (got, _, runner) = await Run([Good], [Broken]);
        Assert.NotNull(MybkReply.Read(got["curriculum"]).Why);
        Assert.Equal(2, runner.Calls["curriculum"]);   // một lần, không lặp
    }

    [Fact]
    public async Task Throttle_is_not_retried()
    {
        var (_, retried, runner) = await Run([Good], [new(503, "", true)]);
        Assert.Empty(retried);
        Assert.Equal(1, runner.Calls["curriculum"]);
    }

    [Fact]
    public void Only_header_api_is_silent()
    {
        // curriculumInfo chỉ là phần đầu: lỗi thì Build giữ phần đầu cũ, người dùng không bị báo "chưa đọc được chương trình đào tạo".
        Assert.True(MybkReply.Silent("curriculumInfo"));
        Assert.False(MybkReply.Silent("curriculum"));
        Assert.False(MybkReply.Silent("schedule"));
    }

    [Theory]
    [InlineData(401, true)]
    [InlineData(403, true)]
    [InlineData(500, false)]                                      // lỗi mà trang không có token: phiên đã hết
    public void Session_end_throws(int status, bool token) =>
        Assert.Throws<SessionExpiredException>(() => MybkReply.Read(R(status, "", token)));
}
