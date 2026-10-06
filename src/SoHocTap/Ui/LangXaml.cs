using System.Windows.Markup;

namespace SoHocTap.Ui;

/// <summary>Dùng trong XAML: Text="{ui:T nav.today}". Chữ chỉ lấy một lần lúc dựng UI, nên đổi ngôn ngữ phải restart app.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(Key);
}
