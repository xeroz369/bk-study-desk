using System.Text.RegularExpressions;

namespace SoHocTap.Sources.Lms;

/// <summary>Lọc mốc của lớp dùng chung cho nhiều nhóm (lớp thí nghiệm) theo mã nhóm trong tên mốc. Hàm thuần, test được.</summary>
public static class GroupFilter
{
    /// <summary>
    /// Các mã nhóm đứng riêng trong tên (khớp <paramref name="pattern"/>, có ranh giới từ nên "CL01" không tính L01), đúng thứ tự
    /// xuất hiện, không trùng. Tên hay pattern rỗng thì trả danh sách rỗng.
    /// </summary>
    public static IReadOnlyList<string> Codes(string name, string pattern)
    {
        return Matcher(pattern)(name);
    }

    /// <summary>Như <see cref="Codes"/> nhưng biên dịch regex một lần, để gọi cho nhiều tên (dựng timeline).</summary>
    public static Func<string, IReadOnlyList<string>> Matcher(string pattern)
    {
        if (pattern.Length == 0) return _ => [];
        var re = new Regex($"(?<![A-Za-z0-9])(?:{pattern})(?![A-Za-z0-9])");
        return name => name.Length == 0 ? [] : [.. re.Matches(name).Select(m => m.Value).Distinct()];
    }

    /// <summary>
    /// Giữ mốc không? Tên không có mã nhóm thì giữ. Không biết nhóm của mình trong lớp (đọc nhóm lỗi, hoặc LMS trả danh sách rỗng,
    /// <paramref name="myGroups"/> null hay rỗng) thì cũng giữ: thà hiện thừa mốc của nhóm khác còn hơn mất hạn nộp của mình.
    /// Chỉ bỏ khi biết chắc nhóm của mình và mã trong tên không phải nhóm đó.
    /// </summary>
    public static bool Keep(IReadOnlySet<string> codesInName, IReadOnlySet<string>? myGroups) =>
        codesInName.Count == 0 || myGroups is not { Count: > 0 } || codesInName.Overlaps(myGroups);
}
