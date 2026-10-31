using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BKStudyDesk.Desktop;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Ui;

[assembly: AvaloniaTestApplication(typeof(BKStudyDesk.Ui.Tests.UiApp))]

namespace BKStudyDesk.Ui.Tests;

// Chạy app thật (App.axaml: Fluent, DataGrid, Tokens) trong headless để các style "card", "title"... có thật như khi chạy.
public static class UiApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

// Thư mục data của test: Paths dò ngược từ thư mục exe tới thư mục đầu tiên có data\, và ở kho này sẽ rơi vào data\ thật của người dùng.
// Tạo data\ ngay cạnh exe test trước khi ai đụng tới Paths, để mọi đọc, ghi của test nằm trong thư mục build.
internal static class Isolation
{
    [ModuleInitializer]
    internal static void Init()
    {
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        L.Load();
    }
}

internal static class Sample
{
    private static long Ago(int hours) => Format.Now - hours * 3600L;

    public static LmsMessages Messages(int count = 130)
    {
        var msgs = Enumerable.Range(1, count)
            .Select(i => new LmsMessage(i, i % 2 == 0 ? 1 : 2, i % 2 == 0 ? "Giang vien mau" : "Toi", i % 2 != 0, $"<p>Tin mau so {i}</p>", Ago(count - i + 1)))
            .ToList();
        var small = new List<LmsMessage> { new(900, 3, "Ban hoc mau", false, "<p>Chao ban</p>", Ago(2)) };
        return new LmsMessages(Format.Now, [
            new LmsConversation(10, "Nhom hoc mau", true, ["A", "B"], 2, Ago(1), "https://example.test/message/10", msgs),
            new LmsConversation(11, "Ban hoc mau", false, ["Ban hoc mau"], 0, Ago(2), null, small),
        ]);
    }

    public static LmsData Lms() => new(Format.Now, "Sinh vien Mau", "HK mau",
        [new LmsCourse(1, "Giai tich 1", "Giai tich 1", "MT1003", null, "HK mau", "Thay A", "https://example.test/course/1", "Giai tich 1")],
        [], [], null,
        [
            new LmsAnnouncement("a1", "news", "Giai tich 1", "Thong bao", "Thong bao nghi hoc", "Thay A", Ago(3), "https://example.test/a1",
                "<p>Lop nghi hoc tuan sau.</p><p>Xem them o dinh kem.</p>", [new LmsLink("de.pdf", "https://example.test/de.pdf")]),
            new LmsAnnouncement("a2", "system", "", "He thong", "Bao tri he thong", null, Ago(30), null, "<p>LMS bao tri toi nay.</p>"),
        ], null);

    public static void WriteData()
    {
        LmsMessagesStore.Write(JsonSerializer.SerializeToNode(Messages(), DataJson.Options)!);
        LmsStore.Write(JsonSerializer.SerializeToNode(Lms(), DataJson.Options)!);
    }
}

internal static class Ui
{
    /// <summary>Đặt control vào cửa sổ, hiện, chạy hết việc đang chờ (dựng, đo, vẽ).</summary>
    public static Window Show(Control content, double width = 1100, double height = 700)
    {
        var w = new Window { Width = width, Height = height, Content = content };
        w.Show();
        Pump(w);
        return w;
    }

    public static void Pump(Window w)
    {
        Dispatcher.UIThread.RunJobs();
        w.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    public static IEnumerable<string> Texts(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").Where(t => t.Length > 0);

    public static bool HasText(Control root, string part) => Texts(root).Any(t => t.Contains(part, StringComparison.Ordinal));
}
