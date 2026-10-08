using System.Globalization;

namespace SoHocTap.Presentation.Practice;

/// <summary>
/// Luyện trộn (port <c>interleave.ts</c>): rút câu nhiều chương rồi xếp xen kẽ, hai câu liền nhau thuộc hai chương khác nhau
/// khi còn chương khác. Trộn dạng bài buộc người học tự nhận ra bài nào dùng cách nào (Rohrer và Taylor 2007).
/// Khác đề ngẫu nhiên: đề giữ thứ tự chương như đề thật, ở đây cố ý xen kẽ.
/// </summary>
public static class Interleave
{
    /// <summary>Số câu mỗi lượt luyện trộn.</summary>
    public const int MixCount = 20;

    /// <summary>Môn có ít nhất hai chương có câu thì mới trộn được.</summary>
    public static bool CanMix(IReadOnlyList<IReadOnlyList<IReadOnlyList<Question>>> units) =>
        units.Count(u => u.Any(qs => qs.Count > 0)) >= 2;

    /// <summary>
    /// Mỗi fingerprint lấy một lần; câu cùng nhóm đi liền nhau như một khối. Trong từng chương, khối có rank nhỏ lên trước
    /// (chưa làm, tới hạn ôn), bằng nhau thì ngẫu nhiên. Lấy lần lượt mỗi chương một khối theo vòng; khối lớn hơn chỗ còn trống
    /// thì chương đó nhường lượt.
    /// </summary>
    public static List<Question> Run(IReadOnlyList<IReadOnlyList<IReadOnlyList<Question>>> units, int count,
        Func<Question, double>? rank = null, Func<double>? random = null)
    {
        rank ??= _ => 0;
        random ??= Random.Shared.NextDouble;
        var seen = new HashSet<string>();
        var pools = units.Select(lessons =>
            lessons.SelectMany((qs, li) => ExamPick.Blocks(qs.Where(q => q.Fp is { } fp && seen.Add(fp)).ToList(), _ => li.ToString(CultureInfo.InvariantCulture)))
                .ToList()
                .Select(b => (B: b, K: b.Min(rank) + random()))
                .ToList()
                .OrderBy(it => it.K)
                .Select(it => it.B)
                .ToList()).ToList();
        var output = new List<Question>();
        for (var added = true; added && output.Count < count;)
        {
            added = false;
            foreach (var pool in pools)
            {
                var i = pool.FindIndex(b => output.Count + b.Count <= count);
                if (i < 0) continue;
                output.AddRange(pool[i]);
                pool.RemoveAt(i);
                added = true;
            }
        }
        return output;
    }
}
