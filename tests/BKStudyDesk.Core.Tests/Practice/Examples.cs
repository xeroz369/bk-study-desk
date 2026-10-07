namespace BKStudyDesk.Core.Tests.Practice;

// Gói ví dụ trong studypack/examples. Gói lấy từ đề thật (hcmut-*) chỉ có ở bản private, repo public chỉ có vi-du.md:
// thiếu file thì test liên quan kết thúc sớm (xUnit 2 không skip động được), như import.meta.glob của bản TS.
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

    /// <summary>Nội dung file ví dụ theo tên; không có (bản public) thì null.</summary>
    public static string? Read(string name) =>
        Dir() is { } dir && File.Exists(Path.Combine(dir, name)) ? File.ReadAllText(Path.Combine(dir, name)) : null;
}
