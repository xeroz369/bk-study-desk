using System.Globalization;

namespace SoHocTap.Presentation.Practice;

/// <summary>Câu đã rút cho đề ngẫu nhiên (thứ tự gốc: chương, bài, câu) và số câu dự tính theo đề thật.</summary>
public sealed record RandomPick(IReadOnlyList<Question> Questions, int Total);

/// <summary>
/// Đề ngẫu nhiên và xáo câu có nhóm (port <c>exam.ts</c>). Câu cùng nhóm trong một bài dùng chung đề, số liệu
/// nên luôn được rút cùng nhau, đứng liền nhau và giữ thứ tự gốc.
/// </summary>
public static class ExamPick
{
    private static string? GroupKey(Question q, string lesson) => q.Group is { } g ? $"{lesson}|{q.PackId ?? ""}|{g}" : null;

    /// <summary>Gom câu thành khối: câu cùng nhóm vào một khối (ở vị trí câu đầu tiên), câu lẻ mỗi câu một khối.</summary>
    public static List<List<Question>> Blocks(IEnumerable<Question> qs, Func<Question, string>? lessonOf = null)
    {
        lessonOf ??= q => q.LessonId ?? q.ExamId ?? "";
        var output = new List<List<Question>>();
        var byKey = new Dictionary<string, List<Question>>();
        foreach (var q in qs)
        {
            var k = GroupKey(q, lessonOf(q));
            if (k != null && byKey.TryGetValue(k, out var have)) have.Add(q);
            else
            {
                var b = new List<Question> { q };
                if (k != null) byKey[k] = b;
                output.Add(b);
            }
        }
        return output;
    }

    /// <summary>Xáo thứ tự các khối, giữ nguyên thứ tự câu trong mỗi khối.</summary>
    public static List<Question> ShuffleGroups(IEnumerable<Question> qs, Func<double> rand)
    {
        var b = Blocks(qs);
        for (var i = b.Count - 1; i > 0; i--)
        {
            var j = (int)Math.Floor(rand() * (i + 1));
            (b[i], b[j]) = (b[j], b[i]);
        }
        return b.SelectMany(x => x).ToList();
    }

    /// <summary>
    /// Rút câu cho đề ngẫu nhiên, mỗi fingerprint một lần. <paramref name="units"/>[chương][bài] là danh sách câu.
    /// <paramref name="rank"/> nhỏ hơn thì được rút trước (chưa làm, tới hạn ôn); bằng nhau thì ngẫu nhiên.
    /// Chương thiếu câu nhường phần còn lại cho chương khác (chỉ chương đề thật có ra, nếu có blueprint.units).
    /// </summary>
    public static RandomPick PickRandomExam(IReadOnlyList<IReadOnlyList<IReadOnlyList<Question>>> units, Blueprint? bp, int count,
        Func<Question, double>? rank = null, Func<double>? random = null)
    {
        rank ??= _ => 0;
        random ??= Random.Shared.NextDouble;
        var seen = new HashSet<string>();
        var pos = new Dictionary<Question, (int U, int L, int Q)>(ReferenceEqualityComparer.Instance);
        var pools = units.Select((lessons, ui) =>
        {
            var items = lessons.SelectMany((qs, li) =>
            {
                // Câu trùng (cùng fingerprint) chỉ giữ lần đầu; câu trùng trong một nhóm thì nhóm bớt câu đó.
                var fresh = qs.Where((q, qi) =>
                {
                    if (q.Fp is null || !seen.Add(q.Fp)) return false;
                    pos[q] = (ui, li, qi);
                    return true;
                }).ToList();
                return Blocks(fresh, _ => li.ToString(CultureInfo.InvariantCulture)).Select(b => (Qs: b, K: b.Min(rank) + random())).ToList();
            }).ToList();
            return items.OrderBy(it => it.K).ToList();
        }).ToList();

        var sizes = pools.Select(p => p.Sum(it => it.Qs.Count)).ToArray();
        var total = bp?.Units is { } bu ? bu.Sum() : bp?.Count ?? count;
        var want = bp?.Units is { } u2 ? sizes.Select((n, i) => Math.Min(i < u2.Length ? u2[i] : 0, n)).ToArray() : Spread(total, sizes);
        var shortBy = total - want.Sum();
        if (shortBy > 0)
        {
            // Bù chỉ từ chương mà đề thật có ra (blueprint > 0), không lấy từ gói hay quiz LMS đã lưu.
            var room = sizes.Select((n, i) => bp?.Units is not { } u3 || (i < u3.Length ? u3[i] : 0) > 0 ? n - want[i] : 0).ToArray();
            var extra = Spread(shortBy, room);
            want = want.Select((n, i) => n + extra[i]).ToArray();
        }
        // Khối lớn hơn chỗ còn trống thì bỏ qua, lấy khối sau: đề có thể ít hơn dự tính vài câu nhưng không cắt đôi nhóm.
        var picked = pools.SelectMany((items, i) =>
        {
            var take = new List<Question>();
            foreach (var it in items)
                if (take.Count + it.Qs.Count <= want[i]) take.AddRange(it.Qs);
            return take;
        });
        var ordered = picked.OrderBy(q => pos[q].U).ThenBy(q => pos[q].L).ThenBy(q => pos[q].Q).ToList();
        return new RandomPick(ordered, total);
    }

    /// <summary>Chia <paramref name="total"/> cho các nhóm theo cỡ (largest remainder), không nhóm nào nhận quá số nó có.</summary>
    public static int[] Spread(int total, IReadOnlyList<int> sizes)
    {
        var sum = sizes.Sum();
        if (sum == 0) return new int[sizes.Count];
        var n = Math.Min(total, sum);
        var raw = sizes.Select(s => (double)n * s / sum).ToArray();
        var output = raw.Select(r => (int)Math.Floor(r)).ToArray();
        var rest = n - output.Sum();
        foreach (var i in raw.Select((r, i) => (Frac: r - Math.Floor(r), I: i)).OrderByDescending(x => x.Frac).Select(x => x.I))
            if (rest > 0 && output[i] < sizes[i])
            {
                output[i]++;
                rest--;
            }
        return output;
    }

    /// <summary>Đủ câu để ra đề ngẫu nhiên: 10 câu trở lên, hoặc môn có kiểu đề thật và có ít nhất một câu.</summary>
    public static bool EnoughForRandomExam(int questions, bool hasBlueprint) => questions >= 10 || (hasBlueprint && questions > 0);
}
