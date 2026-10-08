using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>
/// Phông, cỡ chữ người dùng chọn (app.font.family, app.font.size, cùng khóa với 1.x). Cỡ thiết kế của thân chữ là 14 (cỡ chữ của control
/// Fluent, ControlContentThemeFontSize); tiêu đề, chữ phụ giãn theo cùng tỉ lệ (FontChoice.Scale), không ép mọi chữ về một cỡ.
/// </summary>
public static class FontConfig
{
    public const double DesignBodySize = 14;

    /// <summary>Đọc config; phông không có trên máy (gỡ phông, config chép từ máy khác) thì về mặc định.</summary>
    internal static FontChoice Read(Func<string, bool> installed)
    {
        var size = Config.Node("app.font.size") is JsonValue v && v.TryGetValue<double>(out var d) ? d : (double?)null;
        var family = Config.Node("app.font.family") is JsonValue f && f.TryGetValue<string>(out var s) ? s : null;
        var choice = FontChoice.Parse(family, size, installed);
        if (family is { Length: > 0 } && choice.Family.Length == 0) Log.Warn($"Phông trong config không có trên máy, dùng mặc định: {family}");
        return choice;
    }

    /// <summary>Lưu lựa chọn (trống, 0 là mặc định).</summary>
    internal static void Save(FontChoice choice)
    {
        var c = (JsonObject)Config.Current.DeepClone();
        var app = c["app"] as JsonObject ?? (JsonObject)(c["app"] = new JsonObject());
        app["font"] = new JsonObject { ["family"] = choice.Family, ["size"] = choice.Size };
        Config.Save(c);
    }

    /// <summary>Lựa chọn cỡ chữ cho ô chọn: "0" là mặc định, còn lại là FontChoice.Sizes.</summary>
    public static IReadOnlyList<(string Value, string Text)> SizeOptions() =>
        [("0", L.F("set.fontSizeDefault", DesignBodySize)), .. FontChoice.Sizes.Select(s => (Key(s), L.F("settings.font.sizeN", s)))];

    public static string Key(double size) => size.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
