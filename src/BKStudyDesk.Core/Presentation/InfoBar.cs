using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public enum InfoSeverity { Info, Success, Warning, Error }

public enum InfoAction { None, Login, Retry, OpenSource, AutoLoginOn, AutoLoginNo }

public sealed record InfoBarState(string Key, InfoSeverity Severity, string Text, string? Detail,
    InfoAction Primary, string? PrimaryText, InfoAction Secondary = InfoAction.None, string? SecondaryText = null,
    string? Source = null, bool AutoHide = false);

public sealed record InfoInput(string LoginStage, string LoginMessage, AccountNeed Need, bool MybkNeeded, bool FirstRun,
    bool OfferAutoLogin, string? Closed);

public static class InfoBar
{
    public static InfoInput Input(AppState s, string stage, string message, bool mybkNeeded, bool autoLoginSupported, string? closed)
    {
        return new(stage, message, s.Account, mybkNeeded, s.Lms is null && s.Mybk is null,
            autoLoginSupported && !Settings.Sso.AutoLoginAsked && !Settings.Sso.AutoLogin && Settings.Sso.RememberDays > 0 && s.Mybk is not null, closed);
    }

    public static InfoBarState? Decide(InfoInput i)
    {
        if (Login(i.LoginStage, i.LoginMessage) is { } login) return login;
        var need = i.Need == AccountNeed.Mybk && !i.MybkNeeded ? AccountNeed.None : i.Need;
        var bar = need != AccountNeed.None ? NeedLogin(need, i.FirstRun)
            : i.OfferAutoLogin ? new InfoBarState("autologin", InfoSeverity.Info, L.T("info.autoLogin"), null,
                InfoAction.AutoLoginOn, L.T("info.autoLogin.on"), InfoAction.AutoLoginNo, L.T("info.autoLogin.no"))
            : null;
        return bar is not null && bar.Key == i.Closed ? null : bar;
    }

    private static InfoBarState NeedLogin(AccountNeed need, bool firstRun)
    {
        if (firstRun) return new("first", InfoSeverity.Info, L.T("info.firstRun"), null, InfoAction.Login, L.T("common.loginHcmut"));
        var what = need switch { AccountNeed.Both => L.T("info.both"), AccountNeed.Lms => "LMS", _ => "MyBK" };
        return new("need:" + need, InfoSeverity.Error, L.F("info.needLogin", what), null, InfoAction.Login, L.T("common.loginHcmut"));
    }

    private static InfoBarState? Login(string stage, string message) => stage switch
    {
        "check" or "password" or "mybk" => new("login", InfoSeverity.Info, L.T("info." + stage), null, InfoAction.None, null),
        "lms" => new("login", InfoSeverity.Info, message.Length > 0 ? L.F("info.lmsFor", message) : L.T("info.lms"), null, InfoAction.None, null),
        "done" or "logout" => new("login:" + stage, InfoSeverity.Success, L.T("info." + stage), null, InfoAction.None, null, AutoHide: true),
        "cancel" => new("login:cancel", InfoSeverity.Warning, L.T("info.cancel"), null, InfoAction.Login, L.T("common.loginHcmut")),
        "error" => new("login:error:" + message, InfoSeverity.Error, L.F("info.error", message), null, InfoAction.Login, L.T("web.retry")),
        _ => null,
    };
}
