using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(BKStudyDesk.Math.Tests.TestApp))]

namespace BKStudyDesk.Math.Tests;

// App rỗng cho test: Fluent và Skia thật (không dùng headless drawing) để glyph được vẽ và đọc lại pixel.
public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        // Bảng (TablesTests, issue #43): theme DataGrid và một style đặt SublevelIndent cho dải nhóm, nạp lúc khởi động như App.axaml.
        Styles.Add(new StyleInclude(new Uri("avares://BKStudyDesk.Math.Tests/")) { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") });
        var header = new Style(x => x.OfType<DataGridRowGroupHeader>());
        header.Setters.Add(new Setter(DataGridRowGroupHeader.SublevelIndentProperty, 0d));
        Styles.Add(header);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
