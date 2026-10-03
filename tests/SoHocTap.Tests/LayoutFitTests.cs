using SoHocTap.Ui;

namespace SoHocTap.Tests;

/// <summary>Thanh trên cùng co giãn (NavFit) và đặt cửa sổ vào vùng làm việc của màn hình (ScreenFit).</summary>
public class LayoutFitTests
{
    private static readonly double[] Full = [110, 90, 110, 110, 150, 100];   // 670
    private static readonly double[] Compact = [42, 42, 42, 42, 42, 42];      // 252
    // Cụm bên phải: mức 0 = Đồng bộ có chữ, mức 1 = Đồng bộ chỉ icon. Giới thiệu đã luôn chỉ icon nên không còn mức riêng.
    private static readonly double[] Right = [370, 320];
    private const double More = 90;

    [Fact]
    public void WideWindowShowsEverything() =>
        Assert.Equal(new NavLayout(false, 0, 6), NavFit.Choose(1200, Full, Compact, Right, More));

    [Fact]
    public void RightButtonsHaveTwoLevels() => Assert.Equal(2, NavFit.RightLevels);

    [Fact]
    public void JustEnoughRoomKeepsLabels() =>
        Assert.Equal(new NavLayout(false, 0, 6), NavFit.Choose(1040, Full, Compact, Right, More));

    [Fact]
    public void NavGoesIconOnlyFirst() =>
        Assert.Equal(new NavLayout(true, 0, 6), NavFit.Choose(1039, Full, Compact, Right, More));

    [Fact]
    public void NavIconOnlyKeepsSyncLabelWhileItFits() =>
        Assert.Equal(new NavLayout(true, 0, 6), NavFit.Choose(622, Full, Compact, Right, More));

    [Fact]
    public void SyncGoesIconOnlyBeforeOverflow() =>
        Assert.Equal(new NavLayout(true, 1, 6), NavFit.Choose(580, Full, Compact, Right, More));

    [Fact]
    public void OverflowKeepsLeadingItems()
    {
        var layout = NavFit.Choose(500, Full, Compact, Right, More);
        Assert.True(layout.NavCompact);
        Assert.Equal(1, layout.Right);
        Assert.Equal(2, layout.Visible);   // 500 - 320 - 90 = 90: hai mục 42 px
    }

    [Fact]
    public void OverflowAlwaysKeepsOneItem() =>
        Assert.Equal(1, NavFit.Choose(100, Full, Compact, Right, More).Visible);

    [Fact]
    public void WindowInsideWorkAreaIsUnchanged() =>
        Assert.Equal(new Box(100, 80, 1000, 700), ScreenFit.Clamp(new Box(100, 80, 1000, 700), new Box(0, 0, 1536, 824), 760, 480));

    [Fact]
    public void WindowOnSecondMonitorStaysThere() =>
        // Màn hình phụ bên phải (x từ 1536): không bị kéo về màn hình chính.
        Assert.Equal(new Box(1700, 100, 900, 600), ScreenFit.Clamp(new Box(1700, 100, 900, 600), new Box(1536, 0, 1920, 1040), 760, 480));

    [Fact]
    public void WindowLargerThanWorkAreaShrinksAndMovesIn() =>
        Assert.Equal(new Box(0, 0, 1229, 720), ScreenFit.Clamp(new Box(300, 200, 1600, 900), new Box(0, 0, 1229, 720), 760, 480));

    [Fact]
    public void WindowFromRemovedMonitorIsPulledOnScreen()
    {
        // Màn hình phụ đã rút: MonitorFromRect trả màn hình gần nhất, cửa sổ phải nằm trọn trong đó.
        var fit = ScreenFit.Clamp(new Box(2500, 300, 1000, 700), new Box(0, 0, 1536, 824), 760, 480);
        Assert.Equal(new Box(536, 124, 1000, 700), fit);
    }

    [Fact]
    public void MinimumSizeWinsOverTinyWorkArea()
    {
        var fit = ScreenFit.Clamp(new Box(0, 0, 900, 600), new Box(0, 0, 700, 400), 760, 480);
        Assert.Equal((760, 480), (fit.Width, fit.Height));
        Assert.Equal((0, 0), (fit.X, fit.Y));
    }
}
