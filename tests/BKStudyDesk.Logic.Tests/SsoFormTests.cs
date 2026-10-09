using SoHocTap.Core;

namespace SoHocTap.Tests;

public class SsoFormTests
{
    [Fact]
    public void FillScript_EscapesQuotesBackslashAndTags()
    {
        var js = SsoForm.FillScript("2310000", "a'b\"c\\d</script>");
        Assert.Contains("u.value = \"2310000\";", js);
        Assert.DoesNotContain("a'b", js);           // nháy đơn bị mã hóa ('), không đóng chuỗi trong script
        Assert.DoesNotContain("</script>", js);     // < và > bị mã hóa
        Assert.Contains("\\\\d", js);               // gạch chéo ngược được nhân đôi
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
