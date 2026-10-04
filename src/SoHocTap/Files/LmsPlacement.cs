namespace SoHocTap.Files;

/// <summary>File LMS vừa tải về thì làm gì: trùng nội dung một file đã có, thay bản cũ do app tải, hay đặt file mới.</summary>
public enum PlacementKind { Duplicate, ReplaceOwn, New }

/// <param name="Target">Duplicate: file đã có cùng nội dung; ReplaceOwn: bản cũ do app tải (bị cất vào Bản cũ); New: chỗ định đặt.</param>
public sealed record Placement(PlacementKind Kind, string Target);

/// <summary>
/// Đặt file LMS vào thư mục môn mà không bao giờ đụng file của người dùng.
/// Chỉ cây "Tài liệu LMS" (folders.lmsSubfolder) trong thư mục môn là của app: chỉ file trong đó mới được coi là bản cũ để cất
/// vào _Lưu trữ\Bản cũ và thay bằng bản mới. File cùng nội dung nằm chỗ khác (BKeL\, OnGK\ của người dùng) chỉ được ghi là
/// bản trùng (<c>dedupOf</c> trong lms-files.json), không bao giờ ghi vào <c>path</c>: trước 1.1.8 path đó bị coi là bản cũ, lần LMS
/// sửa file sau thì file của người dùng bị chuyển vào Bản cũ và bản mới của LMS nằm đè chỗ đó.
/// Không phụ thuộc config: đường dẫn gốc truyền vào, test được với thư mục tạm.
/// </summary>
public static class LmsPlacement
{
    /// <summary><paramref name="path"/> nằm trong một thư mục <paramref name="lmsSubfolder"/> bên dưới <paramref name="subjectsRoot"/>.</summary>
    public static bool IsAppOwned(string? path, string subjectsRoot, string lmsSubfolder)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(lmsSubfolder)) return false;
        string rel;
        try { rel = Path.GetRelativePath(Path.GetFullPath(subjectsRoot), Path.GetFullPath(path)); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        if (rel == "." || rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel)) return false;
        var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // Phần cuối là tên file; thư mục môn (phần đầu) không thể là "Tài liệu LMS".
        return parts.Length >= 3 && parts[1..^1].Any(p => NameMatch.Same(p, lmsSubfolder));
    }

    /// <param name="knownPath">path đang ghi cho url này trong lms-files.json (có thể là đường dẫn cũ trỏ vào file người dùng).</param>
    /// <param name="sameContent">file trong môn có cùng sha256 với file vừa tải, nếu có.</param>
    /// <param name="dest">chỗ đặt mới trong Tài liệu LMS\&lt;mục&gt;.</param>
    public static Placement Decide(string? knownPath, string? sameContent, string dest, string subjectsRoot, string lmsSubfolder, Func<string, bool> fileExists)
    {
        if (sameContent is not null && fileExists(sameContent)) return new(PlacementKind.Duplicate, sameContent);
        if (knownPath is not null && fileExists(knownPath) && IsAppOwned(knownPath, subjectsRoot, lmsSubfolder)) return new(PlacementKind.ReplaceOwn, knownPath);
        return new(PlacementKind.New, dest);
    }

    /// <summary>
    /// Làm theo quyết định. Trả về file cuối cùng của url (null = bản trùng, file tạm đã xóa). <paramref name="archiveFor"/> cho chỗ cất
    /// bản cũ (trong _Lưu trữ\Bản cũ). File của người dùng không bao giờ bị chuyển: chỉ ReplaceOwn chuyển file, và chỉ file của app.
    /// </summary>
    public static string? Apply(string tmp, Placement p, Func<string, string> archiveFor)
    {
        switch (p.Kind)
        {
            case PlacementKind.Duplicate:
                File.Delete(tmp);
                return null;
            case PlacementKind.ReplaceOwn:
                FileOps.MoveFile(p.Target, FileOps.UniquePath(archiveFor(p.Target)));
                FileOps.MoveFile(tmp, p.Target);   // LMS có bản mới thì đặt đúng chỗ bản cũ
                return p.Target;
            default:
                var dest = FileOps.UniquePath(p.Target);
                FileOps.MoveFile(tmp, dest);
                return dest;
        }
    }

    /// <summary>
    /// Ghi gì vào lms-files.json: <c>path</c> chỉ khi là file của chính url này trong cây của app; trùng với file khác thì
    /// <c>dedupOf</c> (để biết là đã có trên máy, không tải lại), không bao giờ là path.
    /// </summary>
    public static (string? Path, string? DedupOf) IndexFields(Placement p, string? knownPath, string? final, string subjectsRoot, string lmsSubfolder)
    {
        if (p.Kind != PlacementKind.Duplicate) return (final, null);
        var own = knownPath is not null && string.Equals(Path.GetFullPath(knownPath), Path.GetFullPath(p.Target), StringComparison.OrdinalIgnoreCase)
                  && IsAppOwned(p.Target, subjectsRoot, lmsSubfolder);
        return own ? (p.Target, null) : (null, p.Target);
    }
}
