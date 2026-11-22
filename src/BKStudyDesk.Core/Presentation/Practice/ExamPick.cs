using System.Globalization;

namespace SoHocTap.Presentation.Practice;

public sealed record RandomPick(IReadOnlyList<Question> Questions, int Total);

public static class ExamPick
{
    private static string? GroupKey(Question q, string lesson) => q.Group is { } g ? $"{lesson}|{q.PackId ?? ""}|{g}" : null;

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
            var room = sizes.Select((n, i) => bp?.Units is not { } u3 || (i < u3.Length ? u3[i] : 0) > 0 ? n - want[i] : 0).ToArray();
            var extra = Spread(shortBy, room);
            want = want.Select((n, i) => n + extra[i]).ToArray();
        }
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

    public static bool EnoughForRandomExam(int questions, bool hasBlueprint) => questions >= 10 || (hasBlueprint && questions > 0);
}
