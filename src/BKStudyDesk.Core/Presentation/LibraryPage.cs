using System.Globalization;
using SoHocTap.Files;
using SoHocTap.Library;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>
/// Một môn trong danh sách của trang Thư viện. Mine: môn có trong trang Môn học của người dùng (khớp mã); Folder: thư mục môn để lưu tài
/// liệu tải về (môn của người dùng thì đúng thư mục môn đó, môn khác thì theo tên môn trên thư viện). ReplacementOf: môn cũ của người dùng
/// đã ngừng, đây là môn thay thế.
/// </summary>
public sealed record LibraryCourseRow(CourseRef Course, string Title, string Sub, bool Mine, string Folder, CourseRef? ReplacementOf)
{
    public override string ToString() => Title;
}

/// <summary>
/// Trang Thư viện: mọi môn của thư viện (không chỉ môn đang học), tìm theo mã, tên, tên khác, tên cũ (bỏ dấu), lọc theo khoa.
/// Môn của người dùng lên đầu, môn có tài liệu trước môn chưa có, môn đang mở trước môn đã ngừng, rồi theo mã (môn chọn sẵn có nội dung để xem). Bảng tài liệu của một môn dùng chung LibraryPresenter với bản cũ.
/// </summary>
public static class LibraryPagePresenter
{
    /// <summary>Danh sách môn. <paramref name="mine"/>: từng môn ở trang Môn học và các môn thư viện khớp với nó.</summary>
    public static IReadOnlyList<LibraryCourseRow> Courses(LibraryIndex? index, IEnumerable<(string Subject, IReadOnlyList<CourseMatch> Matches)> mine)
    {
        var my = new Dictionary<string, (string Subject, CourseRef? Replaces)>(StringComparer.Ordinal);
        foreach (var (subject, matches) in mine)
            foreach (var m in matches) my.TryAdd(m.Course.Id, (subject, m.ReplacementOf));
        return
        [
            .. (index?.Courses ?? []).Select(c =>
            {
                var isMine = my.TryGetValue(c.Id, out var s);
                return new LibraryCourseRow(c, c.Name, Sub(c, isMine), isMine, isMine ? s.Subject : FolderName(c.Name), isMine ? s.Replaces : null);
            })
            .OrderByDescending(r => r.Mine).ThenByDescending(r => r.Course.Items > 0).ThenBy(r => r.Course.Retired).ThenBy(r => r.Course.Code, StringComparer.Ordinal).ThenBy(r => r.Course.Id, StringComparer.Ordinal),
        ];
    }

    /// <summary>"MT1005, 12 tài liệu, Môn của bạn"; môn đã ngừng ghi rõ.</summary>
    private static string Sub(CourseRef c, bool mine)
    {
        var bits = new List<string> { c.Code, c.Items > 0 ? L.F("library.items", c.Items) : L.T("library.noItems") };
        if (c.Retired) bits.Add(L.T("library.retired"));
        if (mine) bits.Add(L.T("library.mine"));
        return string.Join(", ", bits);
    }

    /// <summary>Tên môn thành tên thư mục: bỏ ký tự hệ điều hành không cho dùng trong tên thư mục.</summary>
    public static string FolderName(string name)
    {
        var bad = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var clean = new string([.. name.Select(ch => bad.Contains(ch) ? ' ' : ch)]).Trim().TrimEnd('.');
        return clean.Length == 0 ? "_" : string.Join(' ', clean.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Lọc: chữ gõ khớp mã, tên, tên tiếng Anh, tên khác, tên cũ (bỏ dấu, không phân biệt hoa thường); khoa null là mọi khoa.</summary>
    public static IReadOnlyList<LibraryCourseRow> Filter(IReadOnlyList<LibraryCourseRow> rows, string query, string? faculty)
    {
        var q = query.Trim();
        return
        [
            .. rows.Where(r => (faculty is null || string.Equals(r.Course.Faculty, faculty, StringComparison.OrdinalIgnoreCase))
                && (q.Length == 0 || Names(r.Course).Any(n => NameMatch.ContainsFolded(n, q)))),
        ];
    }

    private static IEnumerable<string> Names(CourseRef c) =>
        new[] { c.Code, c.Name, c.NameEn ?? "" }.Concat(c.Aliases ?? []).Concat(c.OldNames ?? []).Where(n => n.Length > 0);

    /// <summary>Bộ lọc khoa: "mọi khoa" rồi các khoa có môn, tên theo ngôn ngữ của app (không có thì mã khoa).</summary>
    public static IReadOnlyList<FilterOption> Faculties(LibraryIndex? index)
    {
        var used = (index?.Courses ?? []).Select(c => c.Faculty).Where(f => f is { Length: > 0 }).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vi = StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), true);
        return
        [
            new FilterOption(null, L.T("library.filter.allFaculties")),
            .. (index?.Faculties ?? []).Where(f => used.Contains(f.Key))
                .Select(f => new FilterOption(f.Key, (L.Code == "en" ? f.Name?.En ?? f.Name?.Vi : f.Name?.Vi ?? f.Name?.En) ?? f.Key))
                .OrderBy(o => o.Label, vi),
        ];
    }
}
