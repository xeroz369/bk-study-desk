namespace SoHocTap.Data;

/// <summary>Đợt đăng ký môn MyBK cần hiện ở trang chủ. Hàm thuần, test được.</summary>
public static class Registrations
{
    /// <summary>
    /// Đợt chưa đóng: đang mở lên trước (đóng sớm hơn trước), rồi tới đợt sắp mở (mở sớm hơn trước). Đợt đã đóng thì bỏ.
    /// Không cắt số đợt (DESIGN 6b-3): một kỳ thường chỉ vài đợt.
    /// </summary>
    public static List<MybkRegistration> Upcoming(IEnumerable<MybkRegistration> all, long now) =>
        [.. all.Where(r => r.End > now).OrderBy(r => r.Start <= now ? 0 : 1).ThenBy(r => r.Start <= now ? r.End : r.Start)];

    public static bool IsOpen(MybkRegistration r, long now) => r.Start <= now && now < r.End;
}
