namespace SoHocTap.Core;

public static class DataImport
{
    private static readonly string[] SkipFolders = ["webview", "tmp"];
    private static readonly string[] SkipFiles = ["update-state.json"];

    public static List<string> Plan(IEnumerable<string> relativeFiles) => relativeFiles.Where(f =>
    {
        var parts = f.Split('\\', '/');
        return !SkipFolders.Contains(parts[0], StringComparer.OrdinalIgnoreCase)
            && !f.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
            && !SkipFiles.Contains(f, StringComparer.OrdinalIgnoreCase);
    }).ToList();
}
