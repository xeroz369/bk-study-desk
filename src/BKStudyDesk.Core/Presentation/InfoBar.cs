using SoHocTap.Core;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>Mức độ theo hướng dẫn InfoBar của Microsoft: Error sự cố đã xảy ra, Warning cần làm gì đó, Success việc nền xong, Info đang làm.</summary>
public enum InfoSeverity { Info, Success, Warning, Error }

/// <summary>Việc của một nút trên dải báo; View đổi thành hành động thật (đăng nhập, đồng bộ...).</summary>
public enum InfoAction { None, Login, Retry, OpenSource, AutoLoginOn, AutoLoginNo }

/// <summary>
/// Một dải báo dưới thanh trên cùng. Key: người dùng bấm Đóng thì dải cùng Key không hiện lại (lỗi khác, lượt khác thì hiện).
/// AutoHide: báo xong việc, tự ẩn sau khi đủ lâu để đọc. Source: nguồn của nút OpenSource.
/// </summary>
public sealed record InfoBarState(string Key, InfoSeverity Severity, string Text, string? Detail,
    InfoAction Primary, string? PrimaryText, InfoAction Secondary = InfoAction.None, string? SecondaryText = null,
    string? Source = null, bool AutoHide = false);

/// <summary>
/// Đầu vào của InfoBar.Decide (InfoBar.Input đọc từ AppState). Failure: lỗi đồng bộ gần nhất, đã bỏ MyBK khi MyBK hết phiên mà không cần.
/// Closed: Key của dải người dùng vừa đóng.
/// </summary>
public sealed record InfoInput(string LoginStage, string LoginMessage, AccountNeed Need, bool MybkNeeded, bool FirstRun,
    (string Text, string? Detail, string Source, string Label)? Failure, bool OfferAutoLogin, string? Closed);

/// <summary>
/// Dải báo của app (cùng quy tắc với InfoBar bản 1.x, Ui/MainWindow.UpdateInfoBar). Thứ tự ưu tiên: lượt đăng nhập đang chạy, lỗi đồng bộ,
/// mời bật tự đăng nhập lại, cần đăng nhập. MyBK hết phiên mà lúc này không cần MyBK (MybkLoginPrompt) thì không bật dải, LMS vẫn chạy.
/// Hàm thuần, có test.
/// </summary>
public static class InfoBar
{
    /// <summary>
    /// Đọc đầu vào từ AppState. mybkNeeded: MybkLoginPrompt.Needed (View biết trang đang mở). Mời tự đăng nhập lại một lần, sau khi đã có
    /// dữ liệu MyBK và đang Ghi nhớ đăng nhập (như 1.x OfferAutoLogin); autoLoginSupported: máy có kho mật khẩu.
    /// </summary>
    public static InfoInput Input(AppState s, string stage, string message, bool mybkNeeded, bool autoLoginSupported, string? closed)
    {
        var quietMybk = s.Account == AccountNeed.Mybk && !mybkNeeded;
        return new(stage, message, s.Account, mybkNeeded, s.Lms is null && s.Mybk is null, FirstFailure(s, quietMybk ? SourceIds.Mybk : null),
            autoLoginSupported && !Settings.Sso.AutoLoginAsked && !Settings.Sso.AutoLogin && Settings.Sso.RememberDays > 0 && s.Mybk is not null, closed);
    }

    public static InfoBarState? Decide(InfoInput i)
    {
        if (Login(i.LoginStage, i.LoginMessage) is { } login) return login;
        var need = i.Need == AccountNeed.Mybk && !i.MybkNeeded ? AccountNeed.None : i.Need;
        var bar = need != AccountNeed.None ? NeedLogin(need, i.FirstRun)
            : i.Failure is { } f ? new InfoBarState("fail:" + f.Text, InfoSeverity.Error, f.Text, f.Detail, InfoAction.Retry, L.T("web.retry"),
                InfoAction.OpenSource, L.F("info.openSource", f.Label), f.Source)
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

    /// <summary>Lượt đăng nhập: đang làm (Info), xong, đăng xuất (Success, tự ẩn), hủy (Warning), lỗi (Error, nút đăng nhập lại).</summary>
    private static InfoBarState? Login(string stage, string message) => stage switch
    {
        "check" or "password" or "mybk" => new("login", InfoSeverity.Info, L.T("info." + stage), null, InfoAction.None, null),
        "lms" => new("login", InfoSeverity.Info, message.Length > 0 ? L.F("info.lmsFor", message) : L.T("info.lms"), null, InfoAction.None, null),
        "done" or "logout" => new("login:" + stage, InfoSeverity.Success, L.T("info." + stage), null, InfoAction.None, null, AutoHide: true),
        "cancel" => new("login:cancel", InfoSeverity.Warning, L.T("info.cancel"), null, InfoAction.Login, L.T("common.loginHcmut")),
        "error" => new("login:error:" + message, InfoSeverity.Error, L.F("info.error", message), null, InfoAction.Login, L.T("web.retry")),   // chữ lỗi bảo "chọn Thử lại"
        _ => null,
    };

    /// <summary>Lỗi đồng bộ gần nhất của LMS, MyBK (đang đồng bộ thì chưa tính; skip: nguồn không xét): câu dễ hiểu và chi tiết kỹ thuật.</summary>
    private static (string Text, string? Detail, string Source, string Label)? FirstFailure(AppState s, string? skip)
    {
        foreach (var (name, label) in new[] { (SourceIds.Lms, "LMS"), (SourceIds.Mybk, "MyBK") })
        {
            if (s.Syncing(name) || name == skip) continue;
            if (s.ExplainError(name, label) is var (text, detail)) return (L.F("info.syncFailed", label, text), detail, name, label);
            if (s.Warnings(name) is { Count: > 0 } w)
                return (L.F("info.syncWarnings", label, string.Join(", ", w.Select(x => x.What).Distinct())), string.Join("\n", w.Select(x => $"{x.What}: {x.Detail}")), name, label);
        }
        return null;
    }
}
