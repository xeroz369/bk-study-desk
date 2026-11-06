using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Nút chỉ có icon (Styles/Icons.axaml): luôn kèm tooltip và tên cho trình đọc màn hình, để icon không thay mất chữ với người cần chữ.
/// Key là tên tài nguyên không có tiền tố Icon: "Back", "Download", "Check", "Folder", "OpenNew", "Close", "Bell", "Settings", "Search".
/// </summary>
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
