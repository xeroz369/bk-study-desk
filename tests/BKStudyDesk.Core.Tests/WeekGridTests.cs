using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

public class WeekGridTests
{
    private static WeekBlock B(int start, int end, string key = "MT1005") => new(2, start, end, key, "", "", key);

    [Fact]
    public void Lanes_OverlapSplits_SeparateClustersKeepFullWidth()
    {
        var a = B(420, 520);
        var b = B(480, 600);
        var c = B(600, 700);
        var lanes = WeekGridPresenter.Lanes([c, b, a]);
        Assert.Equal((0, 2), lanes[a]);
        Assert.Equal((1, 2), lanes[b]);
        Assert.Equal((0, 1), lanes[c]);
    }

    [Fact]
    public void Lanes_ReuseFreedLane()
    {
        var a = B(420, 480);
        var b = B(420, 600);
        var c = B(480, 540);
        var lanes = WeekGridPresenter.Lanes([a, b, c]);
        Assert.Equal(2, lanes[c].Count);
        Assert.Equal(lanes[a].Lane, lanes[c].Lane);
    }

    [Fact]
    public void ColorIndex_StableAndInRange()
    {
        Assert.Equal(WeekGridPresenter.ColorIndex("MT1005", 8), WeekGridPresenter.ColorIndex("MT1005", 8));
        Assert.InRange(WeekGridPresenter.ColorIndex("CO1007", 8), 0, 7);
        Assert.Equal(0, WeekGridPresenter.ColorIndex("x", 0));
    }

    [Fact]
    public void Span_WholeHours_DefaultWhenEmpty()
    {
        Assert.Equal((7 * 60, 17 * 60), WeekGridPresenter.Span([]));
        Assert.Equal((7 * 60, 10 * 60), WeekGridPresenter.Span([B(450, 530), B(540, 590)]));
    }
}
