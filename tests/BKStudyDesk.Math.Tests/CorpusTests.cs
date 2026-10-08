using Avalonia.Headless.XUnit;

namespace BKStudyDesk.Math.Tests;

public class CorpusTests
{
    [AvaloniaFact]
    public void Corpus_extracts_delimiters()
    {
        const string md = @"Cho \(x^2\) và \[\frac{1}{2}\] rồi $$a+b$$, lặp \(x^2\) và \(\begin{pmatrix}1\\2\end{pmatrix}\).";
        Assert.Equal([@"x^2", @"\frac{1}{2}", "a+b", @"\begin{pmatrix}1\\2\end{pmatrix}"], Corpus.FromText(md, json: false));
        // JSON nhân đôi dấu \: \\( là \(, \\\\ là \\ (xuống dòng của ma trận).
        const string json = @"{""q"": ""Cho \\(x\\) và \\(\\begin{cases}a\\\\b\\end{cases}\\)""}";
        Assert.Equal(["x", @"\begin{cases}a\\b\end{cases}"], Corpus.FromText(json, json: true));
    }

    [AvaloniaFact]
    public void Examples_render_without_errors()
    {
        // Bản riêng có cả gói hcmut-* (khoảng 1.100 công thức); repo công khai chỉ có vi-du.md nên chỉ đòi không rỗng.
        var formulas = Corpus.FromFiles(Corpus.Files(Path.Combine(Corpus.RepoRoot(), "studypack", "examples"), ".md", ".json"));
        Assert.NotEmpty(formulas);
        AssertRenders(formulas);
    }

    // Nội dung riêng (content/ của người dùng, không vào repo công khai): chạy qua src/SoHocTap/tools-dev/math-corpus.ps1.
    // xUnit không có skip động, nên không đặt biến BK_MATH_CORPUS thì test kết thúc ngay.
    [AvaloniaFact]
    public void Private_corpus_when_configured()
    {
        var dir = Environment.GetEnvironmentVariable("BK_MATH_CORPUS");
        if (string.IsNullOrEmpty(dir)) return;
        var formulas = Corpus.FromFiles(Corpus.Files(dir, ".js", ".json", ".md"));
        Assert.True(formulas.Count > 0, $"không thấy công thức nào trong {dir}");
        AssertRenders(formulas);
    }

    private static void AssertRenders(IReadOnlyList<string> formulas)
    {
        var fails = Corpus.Failures(formulas);
        // Dòng đầu là tổng: math-corpus.ps1 in ra để biết test đã chạy trên bao nhiêu công thức.
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "corpus-failures.txt"),
            fails.Select(f => $"{f.Error}\t{f.Tex}").Prepend($"{formulas.Count} công thức, {fails.Count} lỗi"));
        Assert.True(fails.Count == 0, $"{fails.Count}/{formulas.Count} lỗi:\n" + string.Join("\n", fails.Take(30).Select(f => $"{f.Error}  |  {f.Tex}")));
    }
}
