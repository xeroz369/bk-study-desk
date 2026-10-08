using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SoHocTap.Core;
using SoHocTap.Files;

namespace BKStudyDesk.Desktop.Files;

/// <summary>
/// Mở tài liệu, thư mục trong thư mục học (đường dẫn tương đối) bằng app mặc định của hệ điều hành qua Launcher của Avalonia
/// (Windows, macOS, Linux). File chạy được (exe, script...) không mở, chỉ mở thư mục chứa. PDF dùng viewer.pdfApp nếu người dùng đặt.
/// </summary>
internal static class Opener
{
    public static async Task<bool> OpenAsync(Control from, string rel)
    {
        if (Paths.StudyPath(rel) is not { } full || TopLevel.GetTopLevel(from) is not { } top) return false;
        if (Directory.Exists(full)) return await top.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(full));
        if (!File.Exists(full)) return false;
        if (Documents.IsRunnable(full)) return await RevealAsync(from, rel);
        var pdfApp = Environment.ExpandEnvironmentVariables(Settings.OpenDocs.PdfApp);
        if (full.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && pdfApp.Length > 0 && File.Exists(pdfApp))
        {
            Process.Start(new ProcessStartInfo(pdfApp) { ArgumentList = { full }, UseShellExecute = false });
            return true;
        }
        return await top.Launcher.LaunchFileInfoAsync(new FileInfo(full));
    }

    /// <summary>Mở thư mục chứa file (Launcher không có "chọn file trong thư mục" chung cho ba hệ điều hành).</summary>
    public static async Task<bool> RevealAsync(Control from, string rel) =>
        Paths.StudyPath(rel) is { } full && Path.GetDirectoryName(full) is { } dir && Directory.Exists(dir) && TopLevel.GetTopLevel(from) is { } top
        && await top.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(dir));
}
