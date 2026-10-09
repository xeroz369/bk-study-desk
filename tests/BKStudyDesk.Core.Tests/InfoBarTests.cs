using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests;

/// <summary>Dải báo dưới thanh trên cùng: cùng quy tắc với InfoBar bản 1.x.</summary>
public class InfoBarTests
{
    private static readonly (string, string?, string, string) Fail = ("Không đồng bộ được LMS. Mất mạng", "chi tiết", "lms", "LMS");

    private static InfoInput In(string stage = "", AccountNeed need = AccountNeed.None, bool mybkNeeded = false, bool firstRun = false,
        (string, string?, string, string)? failure = null, bool offer = false, string? closed = null, string message = "") =>
        new(stage, message, need, mybkNeeded, firstRun, failure, offer, closed);

    [Fact]
    public void Nothing_to_say_hides_bar() => Assert.Null(InfoBar.Decide(In()));

    [Fact]
    public void Login_in_progress_wins_over_everything()
    {
        var bar = InfoBar.Decide(In("check", AccountNeed.Both, failure: Fail))!;
        Assert.Equal(InfoSeverity.Info, bar.Severity);
        Assert.Equal(InfoAction.None, bar.Primary);
    }

    [Theory]
    [InlineData("done", InfoSeverity.Success, true)]
    [InlineData("logout", InfoSeverity.Success, true)]
    [InlineData("cancel", InfoSeverity.Warning, false)]
    [InlineData("error", InfoSeverity.Error, false)]
    public void Login_outcomes(string stage, InfoSeverity severity, bool autoHide)
    {
        var bar = InfoBar.Decide(In(stage, message: "x"))!;
        Assert.Equal(severity, bar.Severity);
        Assert.Equal(autoHide, bar.AutoHide);
        Assert.Equal(autoHide ? InfoAction.None : InfoAction.Login, bar.Primary);
    }

    [Fact]
    public void First_run_invites_instead_of_session_expired()
    {
        var bar = InfoBar.Decide(In(need: AccountNeed.Both, firstRun: true))!;
        Assert.Equal(InfoSeverity.Info, bar.Severity);
        Assert.Equal(L.T("info.firstRun"), bar.Text);
        Assert.Equal(InfoSeverity.Error, InfoBar.Decide(In(need: AccountNeed.Lms))!.Severity);
    }

    // MyBK hết phiên mà lúc này không cần MyBK: không bật dải (thanh trạng thái vẫn báo); cần MyBK thì mời đăng nhập lại.
    [Fact]
    public void Quiet_mybk_only_when_not_needed()
    {
        Assert.Null(InfoBar.Decide(In(need: AccountNeed.Mybk)));
        Assert.Equal(InfoAction.Login, InfoBar.Decide(In(need: AccountNeed.Mybk, mybkNeeded: true))!.Primary);
    }

    [Fact]
    public void Need_login_wins_over_sync_failure_then_failure_over_auto_login_offer()
    {
        Assert.Equal(InfoAction.Login, InfoBar.Decide(In(need: AccountNeed.Lms, failure: Fail, offer: true))!.Primary);
        var fail = InfoBar.Decide(In(failure: Fail, offer: true))!;
        Assert.Equal((InfoAction.Retry, InfoAction.OpenSource, "lms"), (fail.Primary, fail.Secondary, fail.Source));
        Assert.Equal(InfoAction.AutoLoginOn, InfoBar.Decide(In(offer: true))!.Primary);
    }

    // Đóng dải thì dải cùng Key không hiện lại; lỗi khác (Key khác) vẫn hiện.
    [Fact]
    public void Closed_key_stays_hidden_other_key_shows()
    {
        var key = InfoBar.Decide(In(failure: Fail))!.Key;
        Assert.Null(InfoBar.Decide(In(failure: Fail, closed: key)));
        Assert.NotNull(InfoBar.Decide(In(failure: ("Không đồng bộ được LMS. Khác", null, "lms", "LMS"), closed: key)));
    }
}
