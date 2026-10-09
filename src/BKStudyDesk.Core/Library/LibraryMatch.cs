using SoHocTap.Files;

namespace SoHocTap.Library;

/// <summary>
/// Ghép môn của người dùng với môn trong thư viện, chỉ theo mã: mã trong tên lớp LMS (LmsCourse.Code, phần trước "_") và mã môn
/// trong thời khóa biểu MyBK, so với id, code và aliases (mã cũ) của thư viện. So theo NFC, không phân biệt hoa thường.
/// Chưa so theo tên (kể cả oldNames): tên môn trùng nhau giữa các khoa, khớp sai thì hiện tài liệu của môn khác.
/// Hàm thuần, không đọc file, không gọi mạng.
/// </summary>
public static class LibraryMatch
{
    /// <summary>Mã đã chuẩn hóa: NFC, bỏ khoảng trắng hai đầu, chữ hoa. Không cắt "_": id thư viện được phép có "_".</summary>
    public static string Key(string? code) => NameMatch.Nfc(code).Trim().ToUpperInvariant();

    /// <summary>Mã môn từ mã lớp LMS ("MT1005_HK251_CC01" thành "MT1005"), giống cách các trang khác của app đọc LmsCourse.Code.</summary>
    public static string LmsCode(string? lmsCode) => Key((lmsCode ?? "").Split('_')[0]);

    /// <summary>
    /// Mọi môn thư viện khớp với các mã của một môn của người dùng, tốt nhất trước: trùng id, trùng code, trùng alias; cùng mức thì
    /// môn đang dùng trước, rồi theo id. Một mã có thể khớp nhiều môn (mã bị dùng lại: GE4169 và GE4169-2024 cùng code), app hiện
    /// cả hai kèm nhãn khóa thay vì tự chọn một. Môn đã ngừng (retired) có replacedBy thì thêm cả môn thay thế, ghi rõ thay cho môn nào.
    /// Không khớp thì danh sách rỗng (tab Thư viện không hiện).
    /// </summary>
    public static IReadOnlyList<CourseMatch> FindAll(IEnumerable<string?> userCodes, IReadOnlyList<CourseRef> library)
    {
        var codes = userCodes.Select(Key).Where(c => c.Length > 0).ToHashSet(StringComparer.Ordinal);
        if (codes.Count == 0) return [];
        var direct = library.Select(c => (Course: c, Rank: Rank(c, codes))).Where(x => x.Rank < int.MaxValue)
            .OrderBy(x => x.Rank).ThenBy(x => x.Course.Id, StringComparer.Ordinal).Select(x => x.Course).ToList();
        var byId = library.GroupBy(c => c.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var result = new List<CourseMatch>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in direct)
        {
            if (seen.Add(c.Id)) result.Add(new CourseMatch(c, null));
            // Đi theo replacedBy (tối đa vài bước, chặn vòng lặp nếu dữ liệu sai).
            var from = c;
            for (var hop = 0; hop < 5 && from.Retired && from.ReplacedBy is { } next && byId.TryGetValue(next, out var repl); hop++)
            {
                // Môn thay thế đã khớp thẳng (người dùng có cả mã mới) thì giữ như cũ, không ghi "thay cho".
                if (seen.Add(repl.Id)) result.Add(new CourseMatch(repl, c));
                from = repl;
            }
        }
        return result;
    }

    // 0..5: nhỏ hơn là khớp tốt hơn; MaxValue là không khớp.
    private static int Rank(CourseRef c, HashSet<string> codes)
    {
        int level;
        if (codes.Contains(Key(c.Id))) level = 0;
        else if (codes.Contains(Key(c.Code))) level = 1;
        else if ((c.Aliases ?? []).Any(a => codes.Contains(Key(a)))) level = 2;
        else return int.MaxValue;
        return level * 2 + (c.Retired ? 1 : 0);
    }

    /// <summary>Ghép một lượt cho nhiều môn: tên môn của người dùng thành các môn thư viện; môn không khớp thì không có trong kết quả.</summary>
    public static Dictionary<string, IReadOnlyList<CourseMatch>> MatchAll(IEnumerable<(string Subject, IEnumerable<string?> Codes)> subjects, IReadOnlyList<CourseRef> library)
    {
        var map = new Dictionary<string, IReadOnlyList<CourseMatch>>(NameMatch.NfcIgnoreCase);
        foreach (var (subject, codes) in subjects)
            if (FindAll(codes, library) is { Count: > 0 } found) map.TryAdd(subject, found);
        return map;
    }
}

/// <param name="ReplacementOf">môn đã ngừng mà người dùng đang học (khớp mã), môn này là môn thay thế (replacedBy); null nếu khớp thẳng.</param>
public sealed record CourseMatch(CourseRef Course, CourseRef? ReplacementOf);
