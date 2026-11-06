using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Sources;
using SoHocTap.Sources.Mybk;

namespace BKStudyDesk.Ui.Tests;

/// <summary>
/// MyBK on demand (docs/plans/2026-11-06-on-demand-sync.md, section 4): a sync calls only the APIs of the stale sections in view,
/// keeps everything else from the saved copy, and records a fetch time per section. Uses the test data folder, never real data.
/// </summary>
[Collection("offline")]   // writes mybk.json in the shared test data folder: not in parallel with other tests
public class MybkSectionsSyncTests
{
    private sealed class FakeRunner(Func<string, FetchResult> reply) : IBrowserRunner
    {
        public List<string> Calls { get; } = [];

        public Task<IReadOnlyDictionary<string, FetchResult>> FetchAsync(IReadOnlyList<FetchRequest> requests, CancellationToken ct, Action<string>? onEach = null)
        {
            var res = new Dictionary<string, FetchResult>();
            foreach (var q in requests)
            {
                Calls.Add(q.Name);
                res[q.Name] = reply(q.Name);
                onEach?.Invoke(q.Name);
            }
            return Task.FromResult<IReadOnlyDictionary<string, FetchResult>>(res);
        }

        public Task<string> PageAsync(string url, CancellationToken ct, string? mustContain = null) => throw new NotSupportedException("no page in this test");
    }

    private static FetchResult Ok(string data) => new(200, $"{{\"code\":\"200\",\"data\":{data}}}", true);

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static void Seed(bool withId, long scheduleAt, long examsAt)
    {
        var student = new JsonObject { ["name"] = "SV", ["mssv"] = "2310000", ["class"] = "L01" };
        if (withId) student["id"] = 42;
        MybkStore.Write(new JsonObject
        {
            ["syncedAt"] = Now - 3600,
            ["student"] = student,
            ["term"] = new JsonObject { ["code"] = "20261", ["name"] = "Học kỳ 1 Năm học 2026 - 2027" },
            ["basisAt"] = Now - 60,
            ["schedule"] = new JsonArray(new JsonObject { ["code"] = "MT1005", ["name"] = "Giải tích 2", ["day"] = 2, ["weeks"] = new JsonArray(42) }),
            ["exams"] = new JsonArray(new JsonObject { ["code"] = "MT1005", ["name"] = "Giải tích 2", ["date"] = "20/12/2026" }),
            ["grades"] = new JsonArray(),
            ["sectionsAt"] = new JsonObject { ["schedule"] = scheduleAt, ["exams"] = examsAt },
        });
    }

    private static Task Sync(FakeRunner r) => new MybkSource(() => r).SyncAsync(_ => { }, force: false, CancellationToken.None);

    [Fact]
    public async Task Only_the_stale_section_in_view_is_called_and_the_rest_is_kept()
    {
        Seed(withId: true, scheduleAt: Now - 13 * 3600, examsAt: Now);
        MybkSource.Show(MybkSections.ForPage("lich", 0));   // schedule (stale) and exams (fresh)
        var r = new FakeRunner(_ => Ok("[]"));
        await Sync(r);
        Assert.Equal(["schedule"], r.Calls);   // no info, no semesters, no exams, no grades
        var saved = MybkStore.Latest();
        Assert.Single(saved["schedule"]!.AsArray());   // empty reply mid-term keeps the saved classes
        Assert.Single(saved["exams"]!.AsArray());
        Assert.True(MybkSections.FetchedAt(saved, "schedule") >= Now - 5);
        Assert.Equal(42, saved["student"]!["id"]!.GetValue<int>());
        Assert.False(MybkSource.NeedsRun());
        MybkSource.Show([]);
    }

    [Fact]
    public async Task Fresh_sections_make_no_request()
    {
        Seed(withId: true, scheduleAt: Now, examsAt: Now);
        Assert.False(MybkSource.Show(MybkSections.ForPage("lich", 1)));
        var r = new FakeRunner(_ => Ok("[]"));
        await Sync(r);
        Assert.Empty(r.Calls);
        MybkSource.Show([]);
    }

    [Fact]
    public async Task Missing_student_id_reads_info_and_term_first()
    {
        Seed(withId: false, scheduleAt: Now - 13 * 3600, examsAt: Now - 60);
        MybkSource.Show([MybkSections.Schedule]);
        var r = new FakeRunner(n => n switch
        {
            "info" => Ok("{\"id\":7,\"code\":\"2310000\",\"firstName\":\"A\",\"lastName\":\"B\"}"),
            "semesters" => Ok("[{\"code\":\"20261\",\"isCurrent\":true,\"nameVi\":\"Học kỳ 1 Năm học 2026 - 2027\"}]"),
            _ => Ok("[]"),
        });
        await Sync(r);
        Assert.Equal(["info", "semesters", "schedule"], r.Calls);
        Assert.Equal(7, MybkStore.Latest()["student"]!["id"]!.GetValue<int>());
        MybkSource.Show([]);
    }

    [Fact]
    public async Task Mybk_button_reloads_the_page_in_view_only()
    {
        Seed(withId: true, scheduleAt: Now - 60, examsAt: Now - 60);   // fetched before the button press
        MybkSource.Show([MybkSections.Exams]);
        MybkSource.RefreshButton();
        var r = new FakeRunner(_ => Ok("[]"));
        await Sync(r);
        Assert.Equal(["exams"], r.Calls);   // schedule is not on this page: not loaded now
        Assert.True(MybkSource.Show([MybkSections.Schedule]));   // but it counts as stale when its page opens
        MybkSource.Show([]);
    }

    [Fact]
    public async Task Button_reloads_fresh_sections_and_a_failure_waits_before_retrying()
    {
        Seed(withId: true, scheduleAt: Now, examsAt: Now);
        MybkSource.Show([MybkSections.Fees]);
        MybkSource.WantVisible();
        var r = new FakeRunner(_ => new FetchResult(401, "", false));   // session expired
        await Assert.ThrowsAsync<SessionExpiredException>(() => Sync(r));
        Assert.All(r.Calls, c => Assert.Equal("fees", c));   // only fees (MybkReply may retry it once)
        Assert.False(MybkSource.NeedsRun());   // failed just now: not restarted in a loop
        MybkSource.Show([]);
    }
}
