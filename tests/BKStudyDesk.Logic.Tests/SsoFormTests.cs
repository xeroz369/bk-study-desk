using SoHocTap.Core;

namespace SoHocTap.Tests;

public class SsoFormTests
{
    [Fact]
    public void FillScript_EscapesQuotesBackslashAndTags()
    {
        var js = SsoForm.FillScript("2310000", "a'b\"c\\d</script>");
        Assert.Contains("u.value = \"2310000\";", js);
        Assert.DoesNotContain("a'b", js);
        Assert.DoesNotContain("</script>", js);
        Assert.Contains("\\\\d", js);
    }

    [Fact]
    public void FillScript_DoesNotCallShadowedFormSubmit()
    {
        var js = SsoForm.FillScript("u", "p");
        Assert.DoesNotContain("f.submit()", js);
        Assert.Contains(".click()", js);
        Assert.Contains("HTMLFormElement.prototype.submit.call(f)", js);
    }

    [Fact]
    public void FillScript_StopsOnCaptchaOrMissingForm()
    {
        var js = SsoForm.FillScript("u", "p");
        Assert.Contains("captcha", js);
        Assert.Contains("return 'false'", js);
        Assert.EndsWith("})()", js);
    }
}
