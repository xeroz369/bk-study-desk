using SoHocTap.Core;
using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

public class SyncStatusTests
{
    private static long At(int day, int hour, int min = 0) => VnTime.FromWall(new DateTime(2026, 10, day, hour, min, 0));

    private static SoHocTap.Sources.SyncProgress P(int? permille) => new("k", Permille: permille);

    [Fact]
    public void Bar_hidden_when_nothing_syncs() => Assert.False(SyncStatus.Progress([(false, P(500)), (false, null)]).Visible);

    [Fact]
    public void Bar_is_real_percent_when_every_running_source_knows_its_total()
    {
        var bar = SyncStatus.Progress([(true, P(200)), (true, P(600)), (false, null)]);
        Assert.True(bar.Visible);
        Assert.Equal(0.4, bar.Fraction!.Value, 3);   // trung bình các nguồn đang chạy
    }

    [Fact]
    public void Bar_is_indeterminate_while_a_source_has_no_total_yet()
    {
        // Đang mở trang MyBK, qua SSO: chưa biết tổng nên chạy vô định, không đứng yên ở 0%.
        Assert.Null(SyncStatus.Progress([(true, P(500)), (true, P(null))]).Fraction);
        Assert.Null(SyncStatus.Progress([(true, null)]).Fraction);
    }

    [Fact]
    public void SyncStatus_Texts()
    {
        var now = At(7, 15);
        Assert.Equal("Đang đồng bộ", SyncStatus.Text(syncing: true, failed: false, At(7, 10), now));
        Assert.Equal("Lỗi đồng bộ", SyncStatus.Text(false, true, At(7, 10), now));
        Assert.Equal("Đồng bộ 10:21", SyncStatus.Text(false, false, At(7, 10, 21), now));
        Assert.Equal("Đồng bộ 05/10", SyncStatus.Text(false, false, At(5, 22), now));
        Assert.Equal("Chưa đồng bộ", SyncStatus.Text(false, false, null, now));
    }
}
