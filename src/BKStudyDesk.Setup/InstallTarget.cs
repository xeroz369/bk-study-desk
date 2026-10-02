using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BKStudyDesk.Setup;

/// <summary>
/// Từ thư mục người dùng chọn ra thư mục cài thật. Hàm thuần (kiểm tra đĩa qua delegate) để test được.
/// Setup.exe của Velopack xóa sạch thư mục đích trước khi cài, nên chỉ cài vào thư mục trống hoặc bản cài cũ của app,
/// và luôn cài vào thư mục con "BKStudyDesk" (chọn D:\ thì cài vào D:\BKStudyDesk, không đụng file khác ở D:\).
/// </summary>
public static class InstallTarget
{
    public const string AppFolder = "BKStudyDesk";

    /// <param name="packId">pack id của gói nhúng trong bộ cài (BKStudyDesk)</param>
    /// <param name="installedPackId">pack id của bản đã cài trong thư mục (đọc current\sq.version), null nếu không đọc được</param>
    public static (string? Dir, string? Error) Resolve(string picked, Func<string, bool> dirExists, Func<string, IEnumerable<string>> entries,
        Func<string, bool> fileExists, string packId, Func<string, string?> installedPackId)
    {
        picked = (picked ?? "").Trim();
        if (!IsFull(picked)) return (null, "Hãy chọn một thư mục đầy đủ, ví dụ D:\\Ung dung.");
        var full = Path.GetFullPath(picked).TrimEnd('\\', '/');
        var dir = Path.GetFileName(full).Equals(AppFolder, StringComparison.OrdinalIgnoreCase) ? full : Path.Combine(full, AppFolder);
        if (!dirExists(dir)) return (dir, null);
        if (fileExists(Path.Combine(dir, "Update.exe")))
            // Bản cài của gói khác (bản test, bản khác tên) thì không cài đè: Setup.exe sẽ xóa nó và hai app ghi lẫn vào nhau.
            return string.Equals(installedPackId(dir), packId, StringComparison.OrdinalIgnoreCase)
                ? (dir, null) : (null, $"Thư mục {dir} đang có một bản cài khác. Hãy chọn thư mục khác.");
        if (entries(dir).Any())
            return (null, $"Thư mục {dir} đã có file khác. Hãy chọn thư mục trống để không bị ghi đè.");
        return (dir, null);
    }

    /// <summary>Đường dẫn tuyệt đối có ổ đĩa (C:\…) hoặc mạng (\\máy\…), không nhận "Apps" hay "\Apps".</summary>
    private static bool IsFull(string p) =>
        (p.Length >= 3 && char.IsLetter(p[0]) && p[1] == ':' && (p[2] == '\\' || p[2] == '/')) || p.StartsWith(@"\\", StringComparison.Ordinal)
        || (Path.DirectorySeparatorChar == '/' && p.StartsWith("/", StringComparison.Ordinal));
}
