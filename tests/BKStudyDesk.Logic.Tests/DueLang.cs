namespace SoHocTap.Ui;

internal static class L
{
    public static string T(string key) => key;
    public static string F(string key, params object?[] args) =>
        string.Join("|", args.Select(a => Convert.ToString(a, System.Globalization.CultureInfo.InvariantCulture)).Prepend(key));
}
