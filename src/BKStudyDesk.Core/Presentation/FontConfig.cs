using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public static class FontConfig
{
    public const double DesignBodySize = 14;

    internal static FontChoice Read(Func<string, bool> installed)
    {
        var size = Config.Node("app.font.size") is JsonValue v && v.TryGetValue<double>(out var d) ? d : (double?)null;
        var family = Config.Node("app.font.family") is JsonValue f && f.TryGetValue<string>(out var s) ? s : null;
        var choice = FontChoice.Parse(family, size, installed);
        if (family is { Length: > 0 } && choice.Family.Length == 0) Log.Warn($"Phông trong config không có trên máy, dùng mặc định: {family}");
        return choice;
    }

    internal static void Save(FontChoice choice)
    {
        var c = (JsonObject)Config.Current.DeepClone();
        var app = c["app"] as JsonObject ?? (JsonObject)(c["app"] = new JsonObject());
        app["font"] = new JsonObject { ["family"] = choice.Family, ["size"] = choice.Size };
        Config.Save(c);
    }

    public static IReadOnlyList<(string Value, string Text)> SizeOptions() =>
        [("0", L.F("set.fontSizeDefault", DesignBodySize)), .. FontChoice.Sizes.Select(s => (Key(s), L.F("settings.font.sizeN", s)))];

    public static string Key(double size) => size.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
