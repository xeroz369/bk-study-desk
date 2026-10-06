using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>
/// Hai câu hỏi lần đầu mở app (như 1.x, FolderSetupWindow và UpdateWindow): nơi lưu tài liệu (bản cài chưa chọn lần nào) và chế độ cập
/// nhật (chưa trả lời thì app không gọi mạng để kiểm tra bản mới). Mỗi lần mở chỉ hỏi một câu, thư mục trước.
/// </summary>
public static class FirstRun
{
    public static bool NeedsFolder => Paths.Kind != InstallKind.Portable && Config.Str("folders.root").Trim().Length == 0;

    /// <summary>Chọn gốc ổ đĩa (D:\) thì dùng thư mục con của app bên trong, khỏi bày file ra gốc ổ.</summary>
    public static string Picked(string folder) =>
        Path.GetPathRoot(folder) is { Length: > 0 } root && string.Equals(root.TrimEnd('\\', '/'), folder.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(folder, AppPackage.Product) : folder;

    /// <summary>Dòng dưới ô thư mục: dung lượng còn trống của ổ (rỗng khi không đọc được).</summary>
    public static string FreeSpace(string folder)
    {
        try
        {
            var root = Path.GetPathRoot(folder.Trim());
            var drive = string.IsNullOrEmpty(root) ? null : new DriveInfo(root);
            if (drive is not { IsReady: true }) return "";
            var bytes = drive.AvailableFreeSpace;
            var free = bytes >= 1L << 30 ? $"{(bytes / (double)(1L << 30)).ToString("0", L.Culture)} GB" : Format.Size(bytes);
            return L.F("setup.free", free, drive.Name.TrimEnd('\\', '/'));
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException) { return ""; }
    }

    /// <summary>Tạo thư mục, thử ghi một file tạm, rồi lưu folders.root; null là xong, còn lại là câu báo lỗi.</summary>
    public static string? SaveFolder(string path)
    {
        try
        {
            if (!Path.IsPathFullyQualified(path)) return L.T("setup.notFull");
            var full = Path.GetFullPath(path);
            Directory.CreateDirectory(full);
            var probe = Path.Combine(full, ".write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            var c = (JsonObject)Config.Current.DeepClone();
            (c["folders"] as JsonObject ?? (JsonObject)(c["folders"] = new JsonObject()))["root"] = full;
            Config.Save(c);
            Log.Info($"Thư mục tài liệu: {full}");
            return null;
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return L.F("setup.invalid", x.Message);
        }
    }
}
