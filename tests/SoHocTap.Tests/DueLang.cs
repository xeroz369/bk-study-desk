namespace SoHocTap.Ui;

/// <summary>Lang.cs đọc Config nên không link được vào test: bản giả này trả "key|đối số" để kiểm key và số mà Due chọn.</summary>
internal static class L
{
    public static string T(string key) => key;
    public static string F(string key, params object?[] args) =>
        string.Join("|", args.Select(a => Convert.ToString(a, System.Globalization.CultureInfo.InvariantCulture)).Prepend(key));
}
