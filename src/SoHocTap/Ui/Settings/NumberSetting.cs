using System.Globalization;
using System.Windows.Controls;

namespace SoHocTap.Ui.SettingsCards;

/// <summary>Ô số nguyên của thẻ Cài đặt (<see cref="TextSetting"/>): không phải số nguyên, hay nhỏ hơn <c>min</c>, thì báo lỗi và giữ giá trị cũ.</summary>
internal sealed class NumberSetting(TextBox box, TextBlock error, int min, Func<int> get, Action<int> set, Action? saved = null)
{
    private static string Text(int v) => v.ToString(CultureInfo.InvariantCulture);

    private readonly TextSetting _text = new(box, error, () => Text(get()),
        s => int.TryParse(s.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var v) && v >= min ? Text(v) : null,
        s => set(int.Parse(s, CultureInfo.InvariantCulture)), L.F("settings.numberError", min), saved);

    public void Load() => _text.Load();

    public void Flush() => _text.Flush();
}
