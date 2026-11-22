using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace BKStudyDesk.Desktop.Views;

internal static class Icons
{
    public static PathIcon Of(string key) => new()
    {
        Data = Application.Current?.FindResource("Icon" + key) as Geometry,
    };

    public static Button Button(string key, string tip, string? classes = "icon")
    {
        var b = new Button { Content = Of(key) };
        if (classes is { Length: > 0 }) foreach (var c in classes.Split(' ')) b.Classes.Add(c);
        ToolTip.SetTip(b, tip);
        AutomationProperties.SetName(b, tip);
        return b;
    }
}
