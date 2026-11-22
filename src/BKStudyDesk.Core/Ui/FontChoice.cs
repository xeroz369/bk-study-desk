namespace SoHocTap.Ui;

internal readonly record struct FontChoice(string Family, double Size)
{
    public static readonly IReadOnlyList<double> Sizes = [13, 14, 16, 18];

    public static FontChoice Default => new("", 0);

    public bool IsDefault => Family.Length == 0 && Size <= 0;

    public static FontChoice Parse(string? family, double? size, Func<string, bool> installed) =>
        new(NormalizeFamily(family, installed), NormalizeSize(size));

    public static string NormalizeFamily(string? family, Func<string, bool> installed)
    {
        var f = (family ?? "").Trim();
        if (f.Length == 0 || f.Length > 100 || f.IndexOfAny([',', '"', '\'', '\\', ';', '{', '}', '<', '>']) >= 0 || f.Any(char.IsControl)) return "";
        return installed(f) ? f : "";
    }

    public static double NormalizeSize(double? size)
    {
        if (size is not { } s || double.IsNaN(s) || double.IsInfinity(s) || s <= 0) return 0;
        return Sizes.MinBy(x => Math.Abs(x - s));
    }

    public double BodySize(double systemSize) => Size > 0 ? Size : systemSize;

    public double Scale(double systemSize) => systemSize > 0 ? BodySize(systemSize) / systemSize : 1;

    public static double Scaled(double designSize, double scale) => Math.Round(designSize * scale * 2, MidpointRounding.AwayFromZero) / 2;

    public double WebScale(double systemSize) => Math.Max(1, Scale(systemSize));
}
