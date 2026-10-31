using SoHocTap.Web;

namespace BKStudyDesk.Core.Tests;

/// <summary>SchoolUrls.PagePath: trang đăng ký môn có ";jsessionid=..." vẫn là cùng trang (lỗi đợt đăng ký môn 28/10/2026).</summary>
public class PagePathTests
{
    [Theory]
    [InlineData("https://mybk.hcmut.edu.vn/dkmh/dangKyMonHocForm.action;jsessionid=3rjjvwxy6fo81hd9ilnzpbuku", "/dkmh/dangKyMonHocForm.action")]
    [InlineData("https://mybk.hcmut.edu.vn/dkmh/dangKyMonHocForm.action", "/dkmh/dangKyMonHocForm.action")]
    [InlineData("https://mybk.hcmut.edu.vn/dkmh/home.action;jsessionid=abc?x=1", "/dkmh/home.action")]
    [InlineData("https://mybk.hcmut.edu.vn/app/", "/app/")]
    [InlineData("https://mybk.hcmut.edu.vn/dkmh;x=1/home.action;jsessionid=a", "/dkmh/home.action")]
    public void Drops_session_parameter(string url, string path) => Assert.Equal(path, SchoolUrls.PagePath(new Uri(url)));
}
