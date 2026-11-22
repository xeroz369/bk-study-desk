using System.Globalization;

namespace SoHocTap.Presentation.Practice;

public static class Interleave
{
    public const int MixCount = 20;

    public static bool CanMix(IReadOnlyList<IReadOnlyList<IReadOnlyList<Question>>> units) =>
        units.Count(u => u.Any(qs => qs.Count > 0)) >= 2;

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
