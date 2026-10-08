using SoHocTap.Presentation.Practice;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests.Practice;

public class AuthorFormTests
{
    private static AuthorForm Form(string type) => new()
    {
        Type = type, Prompt = @"Tính \(x^2\) tại 2", Solution = "Bằng **4**.",
        Options = [new("3", false), new("4", true), new("", false), new("5", true)],
    };

    [Fact]
    public void Single_keeps_filled_options_and_first_right()
    {
        var (q, err) = Form("single").Build();
        Assert.Null(err);
        Assert.Equal(["3", "4", "5"], q!.Options);
        Assert.Equal(1, q.AnswerNumber);
        Assert.Null(q.Type);
        Assert.Contains("<b>4</b>", q.Solution);
        Assert.StartsWith("<p>", q.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Multi_lists_every_right_option()
    {
        var (q, _) = Form("multi").Build();
        Assert.Equal("multi", q!.Type);
        Assert.Equal([1, 2], q.Answers!);
    }

    [Fact]
    public void Truefalse_numeric_short()
    {
        var f = Form("truefalse");
        f.Truth = false;
        Assert.False(f.Build().Q!.AnswerBool);
        f = Form("numeric");
        f.Value = "0,1875";
        f.Tolerance = "0,001";
        f.Unit = "m";
        var n = f.Build().Q!;
        Assert.Equal((0.1875, 0.001, "m"), (n.AnswerNumber!.Value, n.Tolerance!.Value, n.Unit));
        f = Form("short");
        f.Accept = " a | b |";
        Assert.Equal(["a", "b"], f.Build().Q!.Accept!);
    }

    [Theory]
    [InlineData("single", "", "x", "practice.author.err.prompt")]
    [InlineData("single", "x", "", "practice.author.err.solution")]
    [InlineData("numeric", "x", "y", "practice.author.err.value")]
    [InlineData("short", "x", "y", "practice.author.err.accept")]
    public void Errors_name_what_is_missing(string type, string prompt, string solution, string key)
    {
        var f = Form(type);
        f.Prompt = prompt;
        f.Solution = solution;
        Assert.Equal(L.T(key), f.Build().Error);
    }

    [Fact]
    public void Needs_two_options_and_a_right_one()
    {
        var f = Form("single");
        f.Options = [new("a", true)];
        Assert.Equal(L.T("practice.author.err.options"), f.Build().Error);
        f.Options = [new("a", false), new("b", false)];
        Assert.Equal(L.T("practice.author.err.right"), f.Build().Error);
    }
}
