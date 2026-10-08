using SoHocTap.Core;
using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

public class SyncStatusTests
{
    private static long At(int day, int hour, int min = 0) => VnTime.FromWall(new DateTime(2026, 10, day, hour, min, 0));

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
