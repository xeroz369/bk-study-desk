namespace BKStudyDesk.Core.Tests;

public class BoundaryTests
{
    private static readonly string[] Banned = ["Avalonia", "PresentationFramework", "PresentationCore", "WindowsBase", "Microsoft.Web.WebView2", "System.Windows.Forms"];

    [Fact]
    public void Core_ReferencesNoUiOrWindowsUi()
    {
        var refs = typeof(SoHocTap.Core.Config).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();
        Assert.DoesNotContain(refs, r => Banned.Any(b => r.StartsWith(b, StringComparison.Ordinal)));
    }
}
