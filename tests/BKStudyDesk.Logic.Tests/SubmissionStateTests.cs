using System.Text.Json.Nodes;
using SoHocTap.Sources.Lms;
using Xunit;

namespace SoHocTap.Tests;

public class SubmissionStateTests
{
    private static JsonObject Res(string status) => (JsonObject)JsonNode.Parse(
        "{\"lastattempt\":{\"submission\":{\"status\":\"" + status + "\"},\"gradingstatus\":\"notgraded\"},\"assignmentdata\":{}}")!;

    [Theory]
    [InlineData("submitted", SubmissionState.Submitted)]
    [InlineData("draft", SubmissionState.Draft)]
    [InlineData("new", SubmissionState.NotSubmitted)]
    [InlineData("reopened", SubmissionState.NotSubmitted)]
    public void FromMoodle_TheoStatus(string status, SubmissionState expected) => Assert.Equal(expected, SubmissionStates.FromMoodle(Res(status)));

    [Fact]
    public void FromMoodle_ThieuStatus_LaChuaNop() =>
        Assert.Equal(SubmissionState.NotSubmitted, SubmissionStates.FromMoodle((JsonObject)JsonNode.Parse("""{"lastattempt":{"submission":{}}}""")!));

    [Fact]
    public void FromMoodle_CoDiem_LaDaCham() =>
        Assert.Equal(SubmissionState.Graded, SubmissionStates.FromMoodle((JsonObject)JsonNode.Parse(
            """{"lastattempt":{"submission":{"status":"submitted"}},"feedback":{"grade":{"grade":"8.50000"}}}""")!));

    [Fact]
    public void FromMoodle_JsonLoi_LaUnknown()
    {
        Assert.Equal(SubmissionState.Unknown, SubmissionStates.FromMoodle(null));
        Assert.Equal(SubmissionState.Unknown, SubmissionStates.FromMoodle(new JsonObject()));
        Assert.Equal(SubmissionState.Unknown, SubmissionStates.FromMoodle((JsonObject)JsonNode.Parse("""{"exception":"x","errorcode":"y"}""")!));
    }

    private const long Now = 1_800_000_000;

    private static Task<Dictionary<long, SubmissionState>> Run(SubmissionAsk[] asks, JsonObject cache, Func<long, CancellationToken, Task<JsonNode?>> fetch, List<Exception>? errors = null) =>
        SubmissionStates.ResolveAsync(asks, cache, Now, fetch, _ => true, e => errors?.Add(e), CancellationToken.None);

    [Fact]
    public async Task LoiGoiApi_DongBoVanXong_Unknown_MotCanhBao()
    {
        var errors = new List<Exception>();
        var calls = 0;
        var map = await Run([new(1, 11, Now + 100), new(2, 12, Now + 100), new(3, 13, Now + 100)], new JsonObject(),
            (_, _) => { calls++; throw new HttpRequestException("down"); }, errors);
        Assert.Empty(map);
        Assert.Single(errors);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task LoiGoiApi_DungCacheCu()
    {
        var cache = new JsonObject { ["11"] = new JsonObject { ["state"] = "draft", ["at"] = Now - 10 } };
        var map = await Run([new(1, 11, Now + 100)], cache, (_, _) => throw new HttpRequestException("down"));
        Assert.Equal(SubmissionState.Draft, map[1]);
        Assert.Equal("draft", cache["11"]!["state"]!.ToString());
    }

    [Fact]
    public async Task ChiHoiBaiTuHanBayNgayTruoc_VaChuaCham()
    {
        var asked = new List<long>();
        var cache = new JsonObject
        {
            ["12"] = new JsonObject { ["state"] = "graded", ["at"] = Now - 5 * 86400 },
            ["13"] = new JsonObject { ["state"] = "submitted", ["at"] = Now - 3600 },
            ["14"] = new JsonObject { ["state"] = "submitted", ["at"] = Now - 2 * 86400 },
        };
        var map = await Run([new(1, 11, Now - 8 * 86400), new(2, 12, Now + 1), new(3, 13, Now + 1), new(4, 14, Now + 1), new(5, 15, Now + 1)], cache,
            (id, _) => { asked.Add(id); return Task.FromResult<JsonNode?>(Res("new")); });
        Assert.Equal([4L, 5L], asked);
        Assert.False(map.ContainsKey(1));
        Assert.Equal(SubmissionState.Graded, map[2]);
        Assert.Equal(SubmissionState.Submitted, map[3]);
        Assert.Equal(SubmissionState.NotSubmitted, map[4]);
        Assert.Equal(SubmissionState.NotSubmitted, map[5]);
        Assert.Equal("notsubmitted", cache["15"]!["state"]!.ToString());
    }

    [Fact]
    public async Task LoiKhongBoQuaDuoc_NemTiep()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmissionStates.ResolveAsync([new SubmissionAsk(1, 11, Now + 1)], new JsonObject(), Now,
            (_, _) => throw new InvalidOperationException(), _ => false, _ => { }, CancellationToken.None));
    }

    [Fact]
    public void FromMoodle_GradingStatusGraded_LaDaCham() =>
        Assert.Equal(SubmissionState.Graded, SubmissionStates.FromMoodle((JsonObject)JsonNode.Parse(
            "{\"lastattempt\":{\"submission\":{\"status\":\"submitted\"},\"gradingstatus\":\"graded\"}}")!));

    [Fact]
    public async Task LoiMoodleTungBai_KhongChanBaiKhac_MotCanhBao()
    {
        var errors = new List<Exception>();
        var cache = new JsonObject { ["11"] = new JsonObject { ["state"] = "draft", ["at"] = Now - 10 } };
        var map = await Run([new(1, 11, Now + 1), new(2, 12, Now + 1), new(3, 13, Now + 1)], cache,
            (id, _) => id is 1 or 3 ? throw new LmsException("no", "nopermissions") : Task.FromResult<JsonNode?>(Res("submitted")), errors);
        Assert.Single(errors);
        Assert.Equal(SubmissionState.Draft, map[1]);
        Assert.Equal(SubmissionState.Submitted, map[2]);
        Assert.False(map.ContainsKey(3));
    }

    [Fact]
    public async Task CacheHong_CoiNhuThieu()
    {
        var cache = new JsonObject { ["11"] = new JsonObject { ["state"] = "submitted", ["at"] = "abc" } };
        var asked = new List<long>();
        var map = await Run([new(1, 11, Now + 1)], cache, (id, _) => { asked.Add(id); return Task.FromResult<JsonNode?>(Res("draft")); });
        Assert.Equal([1L], asked);
        Assert.Equal(SubmissionState.Draft, map[1]);
    }

    [Fact]
    public async Task KhongCoCmid_HoiNhungKhongCache()
    {
        var cache = new JsonObject();
        var map = await Run([new(7, null, Now + 1)], cache, (_, _) => Task.FromResult<JsonNode?>(Res("submitted")));
        Assert.Equal(SubmissionState.Submitted, map[7]);
        Assert.Empty(cache);
    }
}
