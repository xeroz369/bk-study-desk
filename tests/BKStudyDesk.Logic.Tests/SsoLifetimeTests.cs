using SoHocTap.Core;

namespace SoHocTap.Tests;

/// <summary>Hạn cookie SSO sau mỗi lượt qua SSO và dòng log đo phiên. Bản 1.1.9 giữ cookie tối đa 8 giờ, không gia hạn: MyBK tự đăng xuất.</summary>
public class SsoLifetimeTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0);

    [Fact]
    public void RememberOff_LeavesCookiesAlone()
    {
        Assert.Null(SsoLifetime.CookieExpiry(isSession: true, default, Now, 0));
        Assert.Null(SsoLifetime.CookieExpiry(isSession: false, Now.AddHours(1), Now, 0));
        Assert.Null(SsoLifetime.CookieExpiry(isSession: true, default, Now, -5));
    }

    [Fact]
    public void SessionCookie_Gets30Days()
    {
        Assert.Equal(Now.AddDays(30), SsoLifetime.CookieExpiry(isSession: true, default, Now, 30));
    }

    [Fact]
    public void PersistentCookie_IsExtendedOnEveryPass()
    {
        // Cookie đổi lúc đăng nhập 8 giờ trước (hạn cũ sắp tới): lượt qua SSO này phải đẩy hạn tiếp, không để app tự xóa.
        Assert.Equal(Now.AddDays(30), SsoLifetime.CookieExpiry(isSession: false, Now.AddMinutes(5), Now, 30));
        Assert.Equal(Now.AddDays(30), SsoLifetime.CookieExpiry(isSession: false, Now.AddDays(29), Now, 30));
    }

    [Fact]
    public void ServerCookieLongerThanRemember_IsNotShortened()
    {
        Assert.Null(SsoLifetime.CookieExpiry(isSession: false, Now.AddDays(365), Now, 30));
        Assert.Null(SsoLifetime.CookieExpiry(isSession: false, Now.AddDays(30), Now, 30));
    }

    [Fact]
    public void ExpiredMessage_ReportsLifetimeAndLastAlive()
    {
        var login = new DateTime(2026, 10, 4, 3, 11, 0);
        var msg = SsoLifetime.ExpiredMessage(login, Now.AddMinutes(-47), Now);
        Assert.Equal("Phiên SSO hết hạn trên server: đăng nhập lúc 03:11 04/10, sống 8 giờ 49 phút, lần cuối còn phiên 47 phút trước", msg);
    }

    [Fact]
    public void ExpiredMessage_LongerThanADay_CountsTotalHours()
    {
        var msg = SsoLifetime.ExpiredMessage(Now.AddHours(-30).AddMinutes(-5), Now, Now);
        Assert.Contains("sống 30 giờ 5 phút", msg);
        Assert.Contains("lần cuối còn phiên 0 phút trước", msg);
    }

    [Fact]
    public void ExpiredMessage_UnknownTimes()
    {
        Assert.Equal("Phiên SSO hết hạn trên server: chưa rõ lúc đăng nhập, chưa rõ lần cuối còn phiên", SsoLifetime.ExpiredMessage(null, null, Now));
    }
}
