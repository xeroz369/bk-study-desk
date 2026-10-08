using Avalonia.Markup.Xaml;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Lang;

/// <summary>
/// Chữ giao diện trong XAML lấy từ lang/*.json (cùng file với 1.x): Text="{l:T calendar.tabWeek}". Đọc một lần lúc dựng trang;
/// đổi ngôn ngữ thì mở lại app (như 1.x).
/// </summary>
public sealed class T(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(key);
}
