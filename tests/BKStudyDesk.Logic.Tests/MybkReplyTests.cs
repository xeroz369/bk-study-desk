using SoHocTap.Sources;
using SoHocTap.Sources.Mybk;

namespace SoHocTap.Tests;

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

    [Fact]
    public void Not_found_code_means_no_data_not_failure()
    {
        var r = MybkReply.Read(R(200, """{"code":"404","msg":"Không tìm thấy thông tin CTĐT","data":null}"""));
        Assert.Null(r.Why);
        Assert.False(r.Retry);
        Assert.Equal("Không tìm thấy thông tin CTĐT", r.Missing);
        Assert.Null(r.Data);
    }

    [Theory]
    [InlineData(500, "", true)]
    [InlineData(0, "", true)]
    [InlineData(200, "<html>bảo trì</html>", true)]
    [InlineData(200, """{"code":"500","msg":"Lỗi hệ thống"}""", true)]
    [InlineData(503, "", false)]
    [InlineData(429, "", false)]
    [InlineData(404, "", false)]
    public void Failures_say_why_and_whether_to_retry(int status, string body, bool retry)
    {
        var r = MybkReply.Read(R(status, body));
        Assert.NotNull(r.Why);
        Assert.Equal(retry, r.Retry);
    }

    private sealed class FakeRunner(Dictionary<string, FetchResult[]> replies) : IBrowserRunner
    {
        public Dictionary<string, int> Calls { get; } = [];
        public int Reentered;
        public void Reenter() => Reentered++;

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
        Assert.Equal(1, runner.Calls["curriculumInfo"]);
    }

    [Fact]
    public async Task Lasting_error_stays_after_one_retry()
    {
        var (got, _, runner) = await Run([Good], [Broken]);
        Assert.NotNull(MybkReply.Read(got["curriculum"]).Why);
        Assert.Equal(2, runner.Calls["curriculum"]);
    }

    [Fact]
    public async Task Expired_reply_reenters_and_retries_once()
    {
        var (got, retried, runner) = await Run([Good], [new(401, "", true), Good]);
        Assert.Null(MybkReply.Read(got["curriculum"]).Why);
        Assert.Equal(1, runner.Reentered);
        Assert.Equal(["curriculum"], retried);
        Assert.Equal(1, runner.Calls["curriculumInfo"]);
    }

    [Fact]
    public async Task Still_expired_after_reentry_is_session_end()
    {
        var (got, _, runner) = await Run([Good], [new(401, "", true)]);
        Assert.Equal(2, runner.Calls["curriculum"]);
        Assert.Throws<SessionExpiredException>(() => MybkReply.Read(got["curriculum"]));
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
        Assert.True(MybkReply.Silent("curriculumInfo"));
        Assert.False(MybkReply.Silent("curriculum"));
        Assert.False(MybkReply.Silent("schedule"));
    }

    [Theory]
    [InlineData(401, true)]
    [InlineData(403, true)]
    [InlineData(500, false)]
    public void Session_end_throws(int status, bool token) =>
        Assert.Throws<SessionExpiredException>(() => MybkReply.Read(R(status, "", token)));

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(-1, true)]
    public void No_reply_is_not_session_end(int status, bool token)
    {
        var r = MybkReply.Read(R(status, "không có phản hồi (quá giờ)", token));
        Assert.NotNull(r.Why);
        Assert.True(r.Retry);
    }

    [Fact]
    public async Task Timed_out_api_is_retried_once()
    {
        var (got, retried, runner) = await Run([Good], [new(-1, "không có phản hồi (quá giờ)", false), Good]);
        Assert.Null(MybkReply.Read(got["curriculum"]).Why);
        Assert.Equal(["curriculum"], retried);
        Assert.Equal(2, runner.Calls["curriculum"]);
    }
}
