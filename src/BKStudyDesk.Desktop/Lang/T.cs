using Avalonia.Markup.Xaml;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Lang;

public sealed class T(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(key);
}
