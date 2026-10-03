namespace SoHocTap.Files;

/// <summary>
/// File và thư mục rác hay ẩn trong thư mục môn: không đếm, không hiện ở "Mới cập nhật", không hash, không đi vào khi quét.
/// Một chỗ dùng chung cho Organizer.HashesIn, Documents.ListSubjects, SubjectFiles, ListDir.
/// - Thư mục: bắt đầu bằng '.' (KiCad .history, .git), db (Quartus), __pycache__, node_modules.
/// - File: ~$* (Office đang mở), .$*, *.tmp, *.bkp, *.part (đang tải dở), desktop.ini, Thumbs.db.
/// </summary>
public static class JunkFilter
{
    private static readonly HashSet<string> Dirs = new(StringComparer.OrdinalIgnoreCase) { "db", "__pycache__", "node_modules" };
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase) { "desktop.ini", "Thumbs.db" };
    private static readonly string[] Extensions = [".tmp", ".bkp", ".part"];

    public static bool IsJunkDir(string name) => name.StartsWith('.') || Dirs.Contains(name);

    public static bool IsJunkFile(string name) =>
        name.StartsWith("~$", StringComparison.Ordinal) || name.StartsWith(".$", StringComparison.Ordinal) || Names.Contains(name)
        || Extensions.Any(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Mọi file không phải rác dưới <paramref name="root"/>: không đi vào thư mục rác (cây .git có hàng nghìn file), bỏ symlink/junction
    /// (có thể trỏ ra ngoài thư mục môn hoặc tạo vòng lặp), bỏ thư mục không có quyền đọc.
    /// </summary>
    public static IEnumerable<FileInfo> Files(string root, CancellationToken ct = default)
    {
        if (!Directory.Exists(root)) yield break;
        var stack = new Stack<DirectoryInfo>();
        stack.Push(new DirectoryInfo(root));
        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();
            FileSystemInfo[] entries;
            try { entries = dir.GetFileSystemInfos(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            foreach (var e in entries)
            {
                if ((e.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                if (e is DirectoryInfo d) { if (!IsJunkDir(d.Name)) stack.Push(d); }
                else if (e is FileInfo f && !IsJunkFile(f.Name)) yield return f;
            }
        }
    }

    /// <summary>Mọi thư mục không phải rác dưới <paramref name="root"/> (không tính root), không đi vào thư mục rác.</summary>
    public static IEnumerable<DirectoryInfo> Directories(string root, CancellationToken ct = default)
    {
        if (!Directory.Exists(root)) yield break;
        var stack = new Stack<DirectoryInfo>();
        stack.Push(new DirectoryInfo(root));
        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            DirectoryInfo[] subs;
            try { subs = stack.Pop().GetDirectories(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            foreach (var d in subs)
            {
                if ((d.Attributes & FileAttributes.ReparsePoint) != 0 || IsJunkDir(d.Name)) continue;
                yield return d;
                stack.Push(d);
            }
        }
    }
}
