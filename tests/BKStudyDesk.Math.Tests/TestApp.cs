using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(BKStudyDesk.Math.Tests.TestApp))]

namespace BKStudyDesk.Math.Tests;

// App rỗng cho test: Fluent và Skia thật (không dùng headless drawing) để glyph được vẽ và đọc lại pixel.
public sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
