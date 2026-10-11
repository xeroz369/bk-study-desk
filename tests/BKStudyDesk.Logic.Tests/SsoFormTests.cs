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
    public void FillScript_DoesNotCallShadowedFormSubmit()
    {
        // Trang SSO có <input name="submit">: ô này che form.submit, gọi f.submit() là TypeError, tự đăng nhập lại không bao giờ chạy
        // (thử trên HTML thật của trang 11/10/2026). Bấm nút gửi của trang, không có nút thì gọi hàm submit gốc của form.
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
