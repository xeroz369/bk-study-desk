using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

// Port phần thuần của src/ui/src/lib/study/shuffle.test.ts. Giá trị "từ TS" chạy bằng Node trên shuffle.ts lúc viết test (07/10/2026).
public class ShuffleTests
{
    private static Question Q(string[] options, bool? keepOrder = null) =>
        new() { Id = "q1", Prompt = "p", Options = [.. options], Answer = 0, KeepOrder = keepOrder };

    [Fact]
    public void Rng_matches_typescript()
    {
        var r = Shuffle.Rng(42);
        double[] fromTs = [0.2523451747838408, 0.08812504541128874, 0.5772811982315034, 0.22255426598712802, 0.37566019711084664];
        Assert.Equal(fromTs, Enumerable.Range(0, 5).Select(_ => r()));
    }

    [Fact]
    public void Same_seed_same_sequence_in_unit_interval()
    {
        Func<double> a = Shuffle.Rng(42), b = Shuffle.Rng(42);
        var xs = Enumerable.Range(0, 20).Select(_ => a()).ToList();
        Assert.Equal(xs, Enumerable.Range(0, 20).Select(_ => b()));
        Assert.All(xs, x => Assert.InRange(x, 0, 0.9999999999));
    }

    [Fact]
    public void SeedFor_matches_typescript_and_depends_on_id()
    {
        Assert.Equal(3613888298u, Shuffle.SeedFor(7, "a.q1"));
        Assert.Equal(3994427522u, Shuffle.SeedFor(123456, "bài.q2"));
        Assert.NotEqual(Shuffle.SeedFor(7, "a.q1"), Shuffle.SeedFor(7, "a.q2"));
    }

    [Fact]
    public void Shuffled_is_permutation_and_keeps_source()
    {
        int[] src = [1, 2, 3, 4, 5, 6];
        var output = Shuffle.Shuffled(src, Shuffle.Rng(3));
        Assert.Equal([6, 1, 5, 4, 3, 2], output);
        Assert.Equal([1, 2, 3, 4, 5, 6], src);
    }

    [Fact]
    public void Positional_options_stay_rest_shuffled()
    {
        string[] opts = ["x", "y", "z", "Cả A và B đều đúng", "Tất cả đều sai"];
        for (uint s = 1; s < 20; s++)
        {
            var o = Shuffle.OptionOrder(Q(opts), Shuffle.Rng(s));
            Assert.Equal([3, 4], o[3..]);
            Assert.Equal([0, 1, 2, 3, 4], o.Order());
        }
    }

    [Fact]
    public void OptionOrder_matches_typescript()
    {
        string[] opts = ["x", "y", "z", "Cả A và B đều đúng", "w", "Tất cả đều sai", "v"];
        Assert.Equal([0, 2, 4, 3, 6, 5, 1], Shuffle.OptionOrder(Q(opts), Shuffle.Rng(1)));
        Assert.Equal([4, 6, 0, 3, 2, 5, 1], Shuffle.OptionOrder(Q(opts), Shuffle.Rng(5)));
        Assert.Equal([2, 0, 4, 3, 6, 5, 1], Shuffle.OptionOrder(Q(opts), Shuffle.Rng(99)));
    }

    [Fact]
    public void KeepOrder_and_two_options_stay()
    {
        Assert.Equal([0, 1, 2, 3], Shuffle.OptionOrder(Q(["a", "b", "c", "d"], keepOrder: true), Shuffle.Rng(1)));
        Assert.Equal([0, 1], Shuffle.OptionOrder(Q(["Đúng", "Sai"]), Shuffle.Rng(1)));
    }

    [Theory]
    [InlineData("<b>Tất cả</b> đều đúng")]
    [InlineData("Không có đáp án nào đúng")]
    [InlineData("cả x lẫn y")]
    [InlineData("None of the above")]
    [InlineData("Both A and B")]
    public void Positional_patterns(string option) => Assert.True(Shuffle.IsPositional(option));
}
