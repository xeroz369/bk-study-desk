using SoHocTap.Core;

namespace SoHocTap.Tests;

public class MybkLoginPromptTests
{
    private const long Now = 1_800_000_000, D = 86400;

    [Fact]
    public void Quiet_WhenFreshDataAndNothingNeedsMybk() =>
        Assert.False(MybkLoginPrompt.Needed(false, Now, Now - D, [(Now + 10 * D, Now + 12 * D), (Now - 9 * D, Now - 8 * D)]));

    [Theory]
    [InlineData(true, 1L, false)]      // đang ở trang MyBK
    [InlineData(false, 4L, false)]     // dữ liệu MyBK cũ hơn 3 ngày
    [InlineData(false, 1L, true)]      // sắp tới đợt đăng ký
    public void Prompts_WhenMybkIsNeeded(bool onPage, long daysOld, bool registrationSoon) =>
        Assert.True(MybkLoginPrompt.Needed(onPage, Now, Now - daysOld * D, registrationSoon ? [(Now + 2 * D, Now + 4 * D)] : []));

    [Fact]
    public void Prompts_WhenNoMybkDataYet() => Assert.True(MybkLoginPrompt.Needed(false, Now, null, []));

    [Fact]
    public void Prompts_WhileRegistrationIsOpen() => Assert.True(MybkLoginPrompt.Needed(false, Now, Now - D, [(Now - D, Now + D)]));
}
