using System.Text.Json.Nodes;
using SoHocTap.Sources.Mybk;

namespace BKStudyDesk.Logic.Tests;

/// <summary>Which MyBK sections a page needs and when a saved section is stale (on-demand sync, 06/11/2026).</summary>
public class MybkSectionsTests
{
    private const long Now = 1_793_900_000;

    private static JsonObject Saved(params (string Section, long Ago)[] at) => new()
    {
        ["student"] = new JsonObject { ["id"] = 1 },
        ["term"] = new JsonObject { ["code"] = "20261" },
        ["basisAt"] = Now - 3600,
        ["sectionsAt"] = new JsonObject(at.Select(a => KeyValuePair.Create(a.Section, (JsonNode?)(Now - a.Ago)))),
    };

    [Fact]
    public void Pages_ask_only_for_what_they_show()
    {
        Assert.Equal([MybkSections.Schedule, MybkSections.Exams], MybkSections.ForPage("lich", 0));
        Assert.Equal([MybkSections.Fees], MybkSections.ForPage("mybk", 4));
        Assert.Empty(MybkSections.ForPage("mybk", 7));   // services: links only
        Assert.Empty(MybkSections.ForPage("luyen-tap", 0));
        Assert.All(new[] { "hom-nay", "lich", "mon" }.SelectMany(r => MybkSections.ForPage(r, 0)), s => Assert.True(MybkSections.All.ContainsKey(s)));
        Assert.All(Enumerable.Range(0, 9).SelectMany(t => MybkSections.ForPage("mybk", t)), s => Assert.True(MybkSections.All.ContainsKey(s)));
    }

    [Fact]
    public void Stale_by_section_age()
    {
        var saved = Saved((MybkSections.Schedule, 11 * 3600), (MybkSections.Exams, 7 * 3600));
        Assert.False(MybkSections.Stale(saved, MybkSections.Schedule, Now));   // 12 h tier
        Assert.True(MybkSections.Stale(saved, MybkSections.Exams, Now));       // 6 h tier
        Assert.True(MybkSections.Stale(saved, MybkSections.Fees, Now));        // never fetched
    }

    [Fact]
    public void Sync_button_marks_older_fetches_stale()
    {
        var saved = Saved((MybkSections.Schedule, 60));
        Assert.False(MybkSections.Stale(saved, MybkSections.Schedule, Now));
        Assert.True(MybkSections.Stale(saved, MybkSections.Schedule, Now, staleBefore: Now - 30));
        Assert.False(MybkSections.Stale(saved, MybkSections.Schedule, Now, staleBefore: Now - 90));
    }

    [Fact]
    public void Pick_force_and_recent_failures()
    {
        var saved = Saved((MybkSections.Schedule, 60));
        string[] wanted = [MybkSections.Schedule, MybkSections.Exams, "nope"];
        Assert.Equal([MybkSections.Exams], MybkSections.Pick(wanted, saved, Now, force: false));
        Assert.Equal([MybkSections.Schedule, MybkSections.Exams], MybkSections.Pick(wanted, saved, Now, force: true));
        var failed = new Dictionary<string, long> { [MybkSections.Exams] = Now - 120 };
        Assert.Empty(MybkSections.Pick(wanted, saved, Now, force: false, failed));
        Assert.Equal([MybkSections.Exams], MybkSections.Pick(wanted, saved, Now + 600, force: false, failed));
    }

    [Fact]
    public void Basis_needs_id_term_and_recent_time()
    {
        Assert.True(MybkSections.BasisFreshIn(Saved(), Now));
        Assert.False(MybkSections.BasisFreshIn(Saved(), Now + 12 * 3600));
        var noId = Saved();
        noId["student"] = new JsonObject { ["mssv"] = "2310000" };
        Assert.False(MybkSections.BasisFreshIn(noId, Now));
        Assert.False(MybkSections.BasisFreshIn(null, Now));
    }
}
