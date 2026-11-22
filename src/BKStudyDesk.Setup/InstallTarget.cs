using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BKStudyDesk.Setup;

public static class InstallTarget
{
    public const string AppFolder = "BKStudyDesk";

    public static (string? Dir, string? Error) Resolve(string picked, Func<string, bool> dirExists, Func<string, IEnumerable<string>> entries,
        Func<string, bool> fileExists, string packId, Func<string, string?> installedPackId)
    {
        picked = (picked ?? "").Trim();
        if (!IsFull(picked)) return (null, "Hãy chọn một thư mục đầy đủ, ví dụ D:\\Ung dung.");
        var full = Path.GetFullPath(picked).TrimEnd('\\', '/');
        var dir = Path.GetFileName(full).Equals(AppFolder, StringComparison.OrdinalIgnoreCase) ? full : Path.Combine(full, AppFolder);
        if (!dirExists(dir)) return (dir, null);
        if (fileExists(Path.Combine(dir, "Update.exe")))
            return string.Equals(installedPackId(dir), packId, StringComparison.OrdinalIgnoreCase)
                ? (dir, null) : (null, $"Thư mục {dir} đang có một bản cài khác. Hãy chọn thư mục khác.");
        if (entries(dir).Any())
            return (null, $"Thư mục {dir} đã có file khác. Hãy chọn thư mục trống để không bị ghi đè.");
        return (dir, null);
    }

    private static bool IsFull(string p) =>
        (p.Length >= 3 && char.IsLetter(p[0]) && p[1] == ':' && (p[2] == '\\' || p[2] == '/')) || p.StartsWith(@"\\", StringComparison.Ordinal)
        || (Path.DirectorySeparatorChar == '/' && p.StartsWith("/", StringComparison.Ordinal));
}
