namespace SoHocTap.Files;

/// <summary>Thao tác file dùng chung, không phụ thuộc config (test được với thư mục tạm).</summary>
public static class FileOps
{
    /// <summary>Đường dẫn chưa có ai dùng: trùng tên thì thêm " (bản dd-MM-yyyy)", rồi " (bản dd-MM-yyyy 2)"...</summary>
    public static string UniquePath(string dest)
    {
        if (!File.Exists(dest) && !Directory.Exists(dest)) return dest;
        var dir = Path.GetDirectoryName(dest)!;
        var stem = Path.GetFileNameWithoutExtension(dest);
        var ext = Path.GetExtension(dest);
        var stamp = DateTime.Now.ToString("dd-MM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var cand = Path.Combine(dir, $"{stem} (bản {stamp}){ext}");
        for (var i = 2; File.Exists(cand); i++) cand = Path.Combine(dir, $"{stem} (bản {stamp} {i}){ext}");
        return cand;
    }

    public static void MoveFile(string src, string dest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Move(src, dest);
    }
}
