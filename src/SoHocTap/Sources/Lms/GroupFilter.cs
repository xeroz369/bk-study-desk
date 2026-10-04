namespace SoHocTap.Sources.Lms;

/// <summary>Lọc mốc của lớp dùng chung cho nhiều nhóm (lớp thí nghiệm) theo mã nhóm trong tên mốc. Hàm thuần, test được.</summary>
public static class GroupFilter
{
    /// <summary>
    /// Giữ mốc không? Tên không có mã nhóm thì giữ. Không biết nhóm của mình trong lớp (đọc nhóm lỗi, hoặc LMS trả danh sách rỗng,
    /// <paramref name="myGroups"/> null hay rỗng) thì cũng giữ: thà hiện thừa mốc của nhóm khác còn hơn mất hạn nộp của mình.
    /// Chỉ bỏ khi biết chắc nhóm của mình và mã trong tên không phải nhóm đó.
    /// </summary>
    public static bool Keep(IReadOnlySet<string> codesInName, IReadOnlySet<string>? myGroups) =>
        codesInName.Count == 0 || myGroups is not { Count: > 0 } || codesInName.Overlaps(myGroups);
}
