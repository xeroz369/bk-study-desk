using System.Globalization;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>Một lựa chọn màu nhấn (app.accentChoices trong config, tên hiện lấy từ lang accent.&lt;key&gt;); Value là "#RRGGBB" hay "system".</summary>
public sealed record AccentChoice(string Name, string Value)
{
    public override string ToString() => Name;
}

/// <summary>
/// Màu nhấn người dùng chọn và màu thanh trên cùng suy ra từ nó (thuần, có test). Thanh có chữ trắng nên làm tối màu nhấn tới khi
/// tương phản với trắng đạt MinBarContrast (WCAG 2.x, công thức độ chói tương đối); chế độ tối cần cao hơn để thanh không chói.
/// </summary>
public static class AccentColors
{
    /// <summary>Tương phản tối thiểu giữa chữ trắng và nền thanh (chế độ sáng, chế độ tối).</summary>
    public const double MinBarContrastLight = 5.0, MinBarContrastDark = 7.0;

    public static IReadOnlyList<AccentChoice> Choices() =>
        [.. (Config.Node("app.accentChoices") as JsonArray ?? []).OfType<JsonObject>()
            .Select(o => (Key: o["key"]?.GetValue<string>() ?? "", Value: o["color"]?.GetValue<string>() ?? ""))
            .Where(c => c.Key.Length > 0 && (c.Value == "system" || Parse(c.Value) is not null))
            .Select(c => new AccentChoice(L.T("accent." + c.Key), c.Value))];

    /// <summary>"#RRGGBB" thành (r, g, b); sai dạng thì null.</summary>
    public static (byte R, byte G, byte B)? Parse(string hex) =>
        hex.Length == 7 && hex[0] == '#' && uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)
            ? ((byte)(v >> 16), (byte)(v >> 8), (byte)v) : null;

    public static string Hex((byte R, byte G, byte B) c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Phần màu nhấn pha lên nền thẻ (sáng, tối): ô ngày ở trang Hôm nay, dòng đang chọn trong danh sách và bảng.</summary>
    public const double DateBoxTintLight = 0.10, DateBoxTintDark = 0.25, SelectedTintLight = 0.07, SelectedTintDark = 0.18;

    /// <summary>Pha <paramref name="amount"/> (0..1) màu nhấn lên nền.</summary>
    public static (byte R, byte G, byte B) Mix((byte R, byte G, byte B) accent, (byte R, byte G, byte B) background, double amount)
    {
        static byte Ch(byte a, byte b, double t) => (byte)Math.Round(b + (a - b) * t);
        return (Ch(accent.R, background.R, amount), Ch(accent.G, background.G, amount), Ch(accent.B, background.B, amount));
    }

    /// <summary>Màu thanh: màu nhấn làm tối dần (mỗi bước 4%) tới khi chữ trắng đạt tương phản tối thiểu.</summary>
    public static (byte R, byte G, byte B) Bar((byte R, byte G, byte B) accent, bool dark)
    {
        var min = dark ? MinBarContrastDark : MinBarContrastLight;
        var c = accent;
        for (var i = 0; i < 40 && ContrastWithWhite(c) < min; i++)
            c = ((byte)(c.R * 0.96), (byte)(c.G * 0.96), (byte)(c.B * 0.96));
        return c;
    }

    public static double ContrastWithWhite((byte R, byte G, byte B) c) => 1.05 / (Luminance(c) + 0.05);

    private static double Luminance((byte R, byte G, byte B) c)
    {
        static double Ch(byte v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
    }
}
