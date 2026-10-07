using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

// Port src/ui/src/lib/study/exam.test.ts và interleave.test.ts.
internal static class Fake
{
    /// <summary>Câu giả: id "u.l.q", fp duy nhất trừ khi truyền fp.</summary>
    public static Question Q(string id, string? group = null, string? fp = null) => new()
    {
        Id = id,
        LessonId = string.Join(".", id.Split('.').Take(2)),
        Fp = fp ?? "fp-" + id,
        Prompt = id,
        Options = ["a", "b"],
        Answer = 0,
        Group = group,
    };

    public static List<Question> Lesson(int u, int l, int n, Func<int, string?>? group = null) =>
        Enumerable.Range(0, n).Select(i => Q($"{u}.{l}.{i + 1}", group?.Invoke(i))).ToList();

    public static string[] Ids(IEnumerable<Question> qs) => qs.Select(x => x.Id).ToArray();

    public static List<List<List<Question>>> Units(params List<List<Question>>[] units) => [.. units];
}

public class ExamPickTests
{
    private static readonly Func<Question, double> Flat = _ => 0;

    [Fact]
    public void Spread_by_size_never_over()
    {
        Assert.Equal([5, 5], ExamPick.Spread(10, [10, 10]));
        Assert.Equal([1, 9], ExamPick.Spread(10, [3, 30]));
        Assert.Equal([3, 4], ExamPick.Spread(50, [3, 4]));
        Assert.Equal([0, 0], ExamPick.Spread(5, [0, 0]));
    }

    [Fact]
    public void Keeps_original_order()
    {
        var units = Fake.Units([Fake.Lesson(0, 0, 6), Fake.Lesson(0, 1, 6)], [Fake.Lesson(1, 0, 8)]);
        var order = units.SelectMany(u => u).SelectMany(l => l).Select(x => x.Id).ToList();
        for (uint seed = 1; seed < 30; seed++)
        {
            var pick = ExamPick.PickRandomExam(units, null, 10, Flat, Shuffle.Rng(seed));
            Assert.Equal(10, pick.Questions.Count);
            var at = pick.Questions.Select(x => order.IndexOf(x.Id)).ToList();
            Assert.Equal(at.Order(), at);
        }
    }

    [Fact]
    public void Group_taken_whole_or_not_and_adjacent()
    {
        var l = Fake.Lesson(0, 0, 8, i => i is >= 1 and <= 3 ? "f" : null);
        string[] grp = ["0.0.2", "0.0.3", "0.0.4"];
        for (uint seed = 1; seed < 50; seed++)
        {
            var got = Fake.Ids(ExamPick.PickRandomExam(Fake.Units([l]), null, 5, Flat, Shuffle.Rng(seed)).Questions);
            var g = got.Count(grp.Contains);
            Assert.Contains(g, new[] { 0, 3 });
            if (g > 0)
            {
                var i = Array.IndexOf(got, "0.0.2");
                Assert.Equal(grp, got[i..(i + 3)]);
            }
            Assert.True(got.Length <= 5);
        }
    }

    [Fact]
    public void Same_group_name_in_two_lessons_is_two_groups()
    {
        var a = Fake.Lesson(0, 0, 2, _ => "g");
        var b = Fake.Lesson(0, 1, 2, _ => "g");
        var qs = ExamPick.PickRandomExam(Fake.Units([a, b]), null, 2, Flat, Shuffle.Rng(3)).Questions;
        Assert.Equal(2, qs.Count);
        Assert.Single(qs.Select(x => x.LessonId).Distinct());
    }

    [Fact]
    public void One_per_fingerprint()
    {
        var a = Fake.Lesson(0, 0, 3);
        List<Question> b = [Fake.Q("0.1.1", fp: a[0].Fp), Fake.Q("0.1.2")];
        Assert.Equal(["0.0.1", "0.0.2", "0.0.3", "0.1.2"], Fake.Ids(ExamPick.PickRandomExam(Fake.Units([a, b]), null, 10, Flat, Shuffle.Rng(1)).Questions));
    }

    [Fact]
    public void Low_rank_first()
    {
        string[] want = ["0.0.7", "0.0.8", "0.0.9"];
        var qs = ExamPick.PickRandomExam(Fake.Units([Fake.Lesson(0, 0, 10)]), null, 3, x => want.Contains(x.Id) ? 0 : 1, Shuffle.Rng(5)).Questions;
        Assert.Equal(want, Fake.Ids(qs));
    }

    [Fact]
    public void Blueprint_counts_per_chapter_zero_not_filled()
    {
        var units = Fake.Units([Fake.Lesson(0, 0, 10)], [Fake.Lesson(1, 0, 10)], [Fake.Lesson(2, 0, 10)]);
        var pick = ExamPick.PickRandomExam(units, new Blueprint(30, [3, 2, 0]), 20, Flat, Shuffle.Rng(2));
        Assert.Equal(5, pick.Total);
        Assert.Equal(3, pick.Questions.Count(x => x.Id.StartsWith("0.")));
        Assert.Equal(2, pick.Questions.Count(x => x.Id.StartsWith("1.")));
        Assert.Equal(0, pick.Questions.Count(x => x.Id.StartsWith("2.")));
    }

    [Fact]
    public void Short_chapter_gives_rest_to_others()
    {
        var units = Fake.Units([Fake.Lesson(0, 0, 1)], [Fake.Lesson(1, 0, 10)]);
        var qs = ExamPick.PickRandomExam(units, new Blueprint(30, [4, 4]), 20, Flat, Shuffle.Rng(2)).Questions;
        Assert.Equal(8, qs.Count);
        Assert.Equal("0.0.1", qs[0].Id);
    }

    [Fact]
    public void ShuffleGroups_keeps_groups_together()
    {
        var qs = Fake.Lesson(0, 0, 10, i => i is >= 4 and <= 6 ? "x" : null);
        var moved = false;
        for (uint seed = 1; seed < 40; seed++)
        {
            var output = Fake.Ids(ExamPick.ShuffleGroups(qs, Shuffle.Rng(seed)));
            Assert.Equal(Fake.Ids(qs).Order(StringComparer.Ordinal), output.Order(StringComparer.Ordinal));
            var i = Array.IndexOf(output, "0.0.5");
            Assert.Equal(["0.0.5", "0.0.6", "0.0.7"], output[i..(i + 3)]);
            if (!output.SequenceEqual(Fake.Ids(qs))) moved = true;
        }
        Assert.True(moved);
    }

    [Fact]
    public void Blocks_singles_alone_group_at_first_position()
    {
        List<Question> qs = [Fake.Q("0.0.1", "a"), Fake.Q("0.0.2"), Fake.Q("0.0.3", "a")];
        Assert.Equal([["0.0.1", "0.0.3"], ["0.0.2"]], ExamPick.Blocks(qs).Select(Fake.Ids));
    }

    // Cùng seed thì cùng đề như bản TS (chạy exam.ts, interleave.ts bằng Node lúc viết test, 07/10/2026).
    [Fact]
    public void Same_seed_same_result_as_typescript()
    {
        var units = Fake.Units([Fake.Lesson(0, 0, 6, i => i < 2 ? "g" : null), Fake.Lesson(0, 1, 6)], [Fake.Lesson(1, 0, 8)], [Fake.Lesson(2, 0, 5)]);
        Assert.Equal(["0.0.1", "0.0.2", "0.0.6", "0.1.3", "0.1.4", "1.0.2", "1.0.7", "1.0.8", "2.0.2", "2.0.4"],
            Fake.Ids(ExamPick.PickRandomExam(units, null, 10, Flat, Shuffle.Rng(7)).Questions));
        Assert.Equal(["0.0.6", "1.0.6", "2.0.4", "0.1.2", "1.0.7", "2.0.1", "0.0.1", "0.0.2", "1.0.5"],
            Fake.Ids(Interleave.Run(units, 9, Flat, Shuffle.Rng(4))));
    }

    [Fact]
    public void EnoughForRandomExam_rules()
    {
        Assert.True(ExamPick.EnoughForRandomExam(10, false));
        Assert.False(ExamPick.EnoughForRandomExam(9, false));
        Assert.True(ExamPick.EnoughForRandomExam(1, true));
        Assert.False(ExamPick.EnoughForRandomExam(0, true));
    }
}

public class InterleaveTests
{
    private static readonly Func<Question, double> Flat = _ => 0;
    private static string UnitOf(Question x) => x.Id.Split('.')[0];

    [Fact]
    public void Neighbours_from_different_chapters_while_possible()
    {
        var units = Fake.Units([Fake.Lesson(0, 0, 6)], [Fake.Lesson(1, 0, 6)], [Fake.Lesson(2, 0, 2)]);
        for (uint seed = 1; seed < 20; seed++)
        {
            var u = Interleave.Run(units, 12, Flat, Shuffle.Rng(seed)).Select(UnitOf).ToList();
            Assert.Equal(12, u.Count);
            for (var i = 1; i < u.Count; i++) Assert.NotEqual(u[i - 1], u[i]);
        }
    }

    [Fact]
    public void Never_over_count_one_per_fingerprint()
    {
        var b = Fake.Lesson(1, 0, 3);
        b[0].Fp = "fp-0.0.1";
        var output = Interleave.Run(Fake.Units([Fake.Lesson(0, 0, 3)], [b]), 50, Flat, Shuffle.Rng(3));
        Assert.Equal(output.Count, output.Select(x => x.Fp).Distinct().Count());
        Assert.Equal(5, output.Count);
    }

    [Fact]
    public void Group_stays_together()
    {
        var units = Fake.Units([Fake.Lesson(0, 0, 3, i => i < 2 ? "g" : null)], [Fake.Lesson(1, 0, 3)]);
        for (uint seed = 1; seed < 20; seed++)
        {
            var ids = Fake.Ids(Interleave.Run(units, 6, Flat, Shuffle.Rng(seed))).ToList();
            Assert.Equal(1, Math.Abs(ids.IndexOf("0.0.1") - ids.IndexOf("0.0.2")));
        }
    }

    [Fact]
    public void Low_rank_first_in_each_chapter()
    {
        var units = Fake.Units([Fake.Lesson(0, 0, 4)], [Fake.Lesson(1, 0, 4)]);
        var output = Fake.Ids(Interleave.Run(units, 2, x => x.Id.EndsWith(".4") ? 0 : 1, Shuffle.Rng(7)));
        Assert.Equal(["0.0.4", "1.0.4"], output.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void CanMix_needs_two_chapters_with_questions()
    {
        Assert.False(Interleave.CanMix(Fake.Units([Fake.Lesson(0, 0, 5)], [[]])));
        Assert.True(Interleave.CanMix(Fake.Units([Fake.Lesson(0, 0, 1)], [Fake.Lesson(1, 0, 1)])));
    }
}
