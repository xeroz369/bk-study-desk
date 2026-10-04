namespace SoHocTap.Ui;

/// <summary>
/// Phông và cỡ chữ người dùng chọn (app.font.family, app.font.size). Family "" và Size 0 nghĩa là theo mặc định của theme,
/// tức phông, cỡ chữ thông báo của Windows (SystemFonts.MessageFont*), vì theme Fluent của WPF không đặt phông hay cỡ chữ cho control.
/// Hàm thuần, không đụng WPF, để test được.
/// </summary>
internal readonly record struct FontChoice(string Family, double Size)
{
    /// <summary>Các cỡ chữ cho chọn (px theo đơn vị WPF). 14 là cỡ thân chữ của Fluent (ControlContentThemeFontSize).</summary>
    public static readonly IReadOnlyList<double> Sizes = [13, 14, 16, 18];

    public static FontChoice Default => new("", 0);

    public bool IsDefault => Family.Length == 0 && Size <= 0;

    /// <summary>
    /// Đọc giá trị trong config. Tên phông không có trên máy (gỡ phông, chép config từ máy khác) thì về mặc định, không để WPF
    /// lặng lẽ thay bằng phông khác. Cỡ chữ lạ thì lấy cỡ gần nhất trong <see cref="Sizes"/>; 0, số âm, NaN là mặc định.
    /// </summary>
    public static FontChoice Parse(string? family, double? size, Func<string, bool> installed) =>
        new(NormalizeFamily(family, installed), NormalizeSize(size));

    public static string NormalizeFamily(string? family, Func<string, bool> installed)
    {
        var f = (family ?? "").Trim();
        // Dấu phẩy là danh sách phông dự phòng của WPF, nháy và ký tự điều khiển thì không phải tên phông: không nhận, khỏi lọt sang CSS.
        if (f.Length == 0 || f.Length > 100 || f.IndexOfAny([',', '"', '\'', '\\', ';', '{', '}', '<', '>']) >= 0 || f.Any(char.IsControl)) return "";
        return installed(f) ? f : "";
    }

    public static double NormalizeSize(double? size)
    {
        if (size is not { } s || double.IsNaN(s) || double.IsInfinity(s) || s <= 0) return 0;
        return Sizes.MinBy(x => Math.Abs(x - s));
    }

    /// <summary>Cỡ thân chữ thật: cỡ đã chọn, hoặc cỡ mặc định của Windows.</summary>
    public double BodySize(double systemSize) => Size > 0 ? Size : systemSize;

    /// <summary>Tỉ lệ so với cỡ mặc định: tiêu đề, chữ phụ giãn theo cùng tỉ lệ (không ép mọi chữ về một cỡ).</summary>
    public double Scale(double systemSize) => systemSize > 0 ? BodySize(systemSize) / systemSize : 1;

    /// <summary>Cỡ của một vai chữ (thiết kế ở cỡ mặc định) sau khi giãn, làm tròn nửa px cho chữ nét.</summary>
    public static double Scaled(double designSize, double scale) => Math.Round(designSize * scale * 2, MidpointRounding.AwayFromZero) / 2;

    /// <summary>
    /// Tỉ lệ cho khung Luyện tập (gốc 16px), gửi kèm tên phông qua URL (main.ts dựng font-family, tên đã qua <see cref="NormalizeFamily"/>
    /// nên không có nháy hay dấu chấm phẩy). Không nhỏ hơn 1: khung HTML đã ở mức chữ nhỏ nhất 12px (MASTER), thu nhỏ nữa là dưới mức đó.
    /// </summary>
    public double WebScale(double systemSize) => Math.Max(1, Scale(systemSize));
}
