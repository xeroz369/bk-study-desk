namespace BKStudyDesk.Core.Tests;

/// <summary>Lõi chạy được trên Windows, macOS, Linux và test được không cần giao diện: không tham chiếu thư viện giao diện hay WebView2.</summary>
public class BoundaryTests
{
    private static readonly string[] Banned = ["Avalonia", "PresentationFramework", "PresentationCore", "WindowsBase", "Microsoft.Web.WebView2", "System.Windows.Forms"];

    [Fact]
    public void Core_ReferencesNoUiOrWindowsUi()
    {
        var refs = typeof(SoHocTap.Core.Config).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();
        Assert.Empty(refs.Where(r => Banned.Any(b => r.StartsWith(b, StringComparison.Ordinal))));
    }
}
