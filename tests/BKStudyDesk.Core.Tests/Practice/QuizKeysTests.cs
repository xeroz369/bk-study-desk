using SoHocTap.Presentation.Practice;

namespace BKStudyDesk.Core.Tests.Practice;

public class QuizKeysTests
{
    [Theory]
    [InlineData("D1", 0)]
    [InlineData("NumPad3", 2)]
    [InlineData("D4", 3)]
    public void Digits_choose_an_option(string key, int expected)
    {
        Assert.Equal(QuizKey.Choose, QuizKeys.Of(key, typing: false, options: 4, out var o));
        Assert.Equal(expected, o);
    }

    [Theory]
    [InlineData("D5", 4)]      // câu chỉ có 4 đáp án
    [InlineData("D0", 4)]
    [InlineData("A", 4)]
    [InlineData("Delete", 4)]
    public void Other_keys_do_nothing(string key, int options) => Assert.Equal(QuizKey.None, QuizKeys.Of(key, false, options, out _));

    [Theory]
    [InlineData("Enter", QuizKey.Check)]
    [InlineData("Left", QuizKey.Prev)]
    [InlineData("Right", QuizKey.Next)]
    public void Enter_and_arrows(string key, QuizKey expected) => Assert.Equal(expected, QuizKeys.Of(key, false, 4, out _));

    [Theory]
    [InlineData("D1")]
    [InlineData("Enter")]
    [InlineData("Left")]
    public void Typing_in_a_box_keeps_keys_for_the_box(string key) => Assert.Equal(QuizKey.None, QuizKeys.Of(key, typing: true, 4, out _));
}
