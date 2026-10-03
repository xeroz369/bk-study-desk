namespace SoHocTap.Ui;

/// <summary>Thanh trên cùng hiện thế nào: mục điều hướng có chữ hay chỉ icon, nút bên phải gọn tới đâu, bao nhiêu mục còn trên thanh.</summary>
/// <param name="NavCompact">Mục điều hướng chỉ còn icon (tên ở tooltip và tên cho UI Automation).</param>
/// <param name="Right">0 = nút Đồng bộ có chữ; 1 = Đồng bộ chỉ còn icon. Cài đặt và Giới thiệu lúc nào cũng chỉ có icon.</param>
/// <param name="Visible">Số mục điều hướng còn trên thanh; các mục sau nằm trong menu Thêm.</param>
internal readonly record struct NavLayout(bool NavCompact, int Right, int Visible);

/// <summary>
/// Chọn cách hiện thanh trên cùng theo bề rộng (không cuộn ngang). Thứ tự bớt dần: mục điều hướng chỉ icon, rồi Đồng bộ chỉ icon,
/// cuối cùng mới dồn các mục cuối vào menu Thêm. Hàm thuần để test được không cần WPF.
/// </summary>
internal static class NavFit
{
    /// <summary>Số mức của cụm nút bên phải (xem <see cref="NavLayout.Right"/>).</summary>
    public const int RightLevels = 2;

    /// <param name="available">Bề rộng cho cả thanh (điều hướng + nút bên phải).</param>
    /// <param name="full">Bề rộng từng mục khi có chữ.</param>
    /// <param name="compact">Bề rộng từng mục khi chỉ icon.</param>
    /// <param name="right">Bề rộng cụm nút bên phải ở mức 0, 1.</param>
    /// <param name="more">Bề rộng nút Thêm.</param>
    public static NavLayout Choose(double available, IReadOnlyList<double> full, IReadOnlyList<double> compact, IReadOnlyList<double> right, double more)
    {
        var n = full.Count;
        if (full.Sum() + right[0] <= available) return new(false, 0, n);
        if (compact.Sum() + right[0] <= available) return new(true, 0, n);
        if (compact.Sum() + right[1] <= available) return new(true, 1, n);
        // Vẫn thiếu: giữ các mục đầu (dùng nhiều nhất), còn lại vào menu Thêm. Luôn giữ ít nhất một mục.
        var room = available - right[1] - more;
        var visible = 0;
        for (double used = 0; visible < n && used + compact[visible] <= room; visible++) used += compact[visible];
        return new(true, 1, Math.Max(1, visible));
    }
}
