namespace BKStudyDesk.Core.Tests.Practice;

internal static class Examples
{
    public static string? Dir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "studypack", "examples");
            if (Directory.Exists(p)) return p;
        }
        return null;
    }

    public static string? Read(string name) =>
        Dir() is { } dir && File.Exists(Path.Combine(dir, name)) ? File.ReadAllText(Path.Combine(dir, name)) : null;
}
