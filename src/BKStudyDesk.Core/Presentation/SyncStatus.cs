using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>Chữ trạng thái đồng bộ trên thanh trên cùng: đang chạy, lỗi, lần cuối (hôm nay ghi giờ, ngày khác ghi ngày), chưa từng.</summary>
public static class SyncStatus
{
    public static string Text(bool syncing, bool failed, long? syncedAt, long now)
    {
        if (syncing) return L.T("sync.syncing");
        if (failed) return L.T("sync.failed");
        if (syncedAt is not { } t) return L.T("sync.never");
        var at = VnTime.ToWall(t);
        return L.F("sync.at", at.Date == VnTime.ToWall(now).Date ? at.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) : at.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture));
    }
}
