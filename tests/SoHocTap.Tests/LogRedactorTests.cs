using SoHocTap.Core;

namespace SoHocTap.Tests;

public class LogRedactorTests
{
    [Theory]
    [InlineData("GET https://lms.example/x.pdf?token=abc123&forcedownload=1", "GET https://lms.example/x.pdf?token=***&forcedownload=1")]
    [InlineData("wstoken=0123456789abcdef", "wstoken=***")]
    [InlineData("redirect ?ticket=ST-12345-AbCdEfGhIj-cas", "redirect ?ticket=***")]
    [InlineData("vé ST-98765-xyzXYZ0123-sso hết hạn", "vé ST-*** hết hạn")]
    [InlineData("Cookie: MoodleSession=abc; CASTGC=TGT-1", "Cookie: ***")]
    [InlineData("Authorization: Bearer eyJhbGciOi.payload.sig", "Authorization: ***")]
    [InlineData("header bearer eyJhbGciOi.payload.sig", "header Bearer ***")]
    [InlineData("liên hệ sv.name@hcmut.edu.vn nhé", "liên hệ ***@*** nhé")]
    public void Redact_MasksSecrets(string input, string expected) => Assert.Equal(expected, LogRedactor.Redact(input));

    [Fact]
    public void Redact_LeavesNormalText() =>
        Assert.Equal("Sync LMS: 17 request, check 5 lớp", LogRedactor.Redact("Sync LMS: 17 request, check 5 lớp"));
}
