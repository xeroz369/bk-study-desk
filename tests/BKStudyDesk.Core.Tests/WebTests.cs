using System.Net;
using SoHocTap.Web;

namespace BKStudyDesk.Core.Tests;

public class WebTests
{
    private static readonly LoginHosts Hosts = new("sso.hcmut.edu.vn", "lms.hcmut.edu.vn", "mybk.hcmut.edu.vn");

    [Fact]
    public void LoginSteps_SsoLogin_NeedsPassword() =>
        Assert.Equal(LoginAction.NeedPassword, LoginSteps.Next(LoginStage.Sso, new Uri("https://sso.hcmut.edu.vn/cas/login?service=x"), Hosts));

    [Fact]
    public void LoginSteps_MybkMy_GoesAppLogin()
    {
        Assert.Equal(LoginAction.GoAppLogin, LoginSteps.Next(LoginStage.Sso, new Uri("https://mybk.hcmut.edu.vn/my/homeSSO.action"), Hosts));
        Assert.Equal(LoginAction.GoAppLogin, LoginSteps.Next(LoginStage.Sso, new Uri("https://mybk.hcmut.edu.vn/app/login/"), Hosts));
    }

    [Fact]
    public void LoginSteps_MybkApp_Ready()
    {
        Assert.Equal(LoginAction.MybkReady, LoginSteps.Next(LoginStage.Sso, new Uri("https://mybk.hcmut.edu.vn/app/"), Hosts));
        Assert.Equal(LoginAction.Wait, LoginSteps.Next(LoginStage.Sso, new Uri("https://mybk.hcmut.edu.vn/app/401"), Hosts));
    }

    [Fact]
    public void LoginSteps_LmsAfterCas_GoLaunch()
    {
        Assert.Equal(LoginAction.GoLaunch, LoginSteps.Next(LoginStage.LmsCas, new Uri("https://lms.hcmut.edu.vn/my/"), Hosts));
        Assert.Equal(LoginAction.Wait, LoginSteps.Next(LoginStage.LmsCas, new Uri("https://lms.hcmut.edu.vn/login/index.php"), Hosts));
        Assert.Equal(LoginAction.Wait, LoginSteps.Next(LoginStage.Done, new Uri("https://mybk.hcmut.edu.vn/app/"), Hosts));
    }

    /// <summary>Handler giả: ghi lại request, trả response cố định. Không gọi mạng.</summary>
    private sealed class FakeHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Seen;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Seen = request;
            return Task.FromResult(response);
        }
    }

    private const string Launch = "https://lms.hcmut.edu.vn/admin/tool/mobile/launch.php?service=s&passport=p&urlscheme=moodlemobile";

    [Fact]
    public async Task TokenGrab_Redirect_ReturnsCallback()
    {
        var r = new HttpResponseMessage(HttpStatusCode.SeeOther);
        r.Headers.TryAddWithoutValidation("Location", "moodlemobile://token=YWJj");   // không phải URI hợp lệ với .NET: phải đọc header thô
        var got = await LmsTokenGrab.LaunchCallbackAsync(new FakeHandler(r), Launch, [], "moodlemobile", CancellationToken.None);
        Assert.Equal("moodlemobile://token=YWJj", got);
    }

    [Fact]
    public async Task TokenGrab_NoRedirect_ReturnsNull()
    {
        var got = await LmsTokenGrab.LaunchCallbackAsync(new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)), Launch, [], "moodlemobile", CancellationToken.None);
        Assert.Null(got);
        var other = new HttpResponseMessage(HttpStatusCode.Redirect);
        other.Headers.Location = new Uri("https://lms.hcmut.edu.vn/login/index.php");
        Assert.Null(await LmsTokenGrab.LaunchCallbackAsync(new FakeHandler(other), Launch, [], "moodlemobile", CancellationToken.None));
    }

    [Fact]
    public async Task TokenGrab_SendsCookies()
    {
        var h = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK));
        Cookie[] cookies = [new("MoodleSession", "abc", "/", "lms.hcmut.edu.vn"), new("other", "x", "/", "example.com")];
        await LmsTokenGrab.LaunchCallbackAsync(h, Launch, cookies, "moodlemobile", CancellationToken.None);
        Assert.Equal("MoodleSession=abc", string.Join("; ", h.Seen!.Headers.GetValues("Cookie")));
    }

    [Fact]
    public void SsoCookies_ExtendsOnlySchoolHosts()
    {
        var now = new DateTime(2026, 10, 7, 9, 0, 0);
        Cookie[] cookies =
        [
            new("TGC", "t", "/cas/", "sso.hcmut.edu.vn"),                                      // phiên: gia hạn
            new("JSESSIONID", "j", "/", ".mybk.hcmut.edu.vn") { Expires = now.AddDays(90) },    // server cho xa hơn: giữ
            new("ads", "a", "/", "example.com"),                                                // ngoài trường: bỏ
        ];
        var changed = SsoCookies.Extend(cookies, ["hcmut.edu.vn"], 30, now);
        var tgc = Assert.Single(changed);
        Assert.Equal("TGC", tgc.Name);
        Assert.Equal(now.AddDays(30), tgc.Expires);
    }

    [Fact]
    public void SsoCookies_ZeroDays_None() =>
        Assert.Empty(SsoCookies.Extend([new Cookie("TGC", "t", "/", "sso.hcmut.edu.vn")], ["hcmut.edu.vn"], 0, DateTime.Now));
}
