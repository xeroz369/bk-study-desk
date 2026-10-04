using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Shell;

/// <summary>
/// Đo phiên SSO thật trên server: lưu lúc đăng nhập đầy đủ (người dùng nhập mật khẩu), lần cuối biết chắc phiên còn và lần
/// đã báo hết, trong data/sso-session.json (chỉ mốc thời gian, không cookie, không vé). Restart app vẫn đo tiếp được.
/// Hết phiên thì ghi một dòng Info cho mỗi phiên, xem <see cref="SsoLifetime.ExpiredMessage"/>.
/// </summary>
internal static class SsoSession
{
    private static readonly object Gate = new();
    private static string StateFile => Paths.DataFile("sso-session.json");

    /// <summary>Người dùng vừa nhập mật khẩu SSO và vào được trang trường: một phiên mới bắt đầu.</summary>
    public static void MarkLogin() => Update(o =>
    {
        var now = Now();
        o["loginAt"] = now;
        o["aliveAt"] = now;
        o.Remove("expiredAt");
    });

    /// <summary>Vừa đi qua SSO mà không phải nhập mật khẩu (đăng nhập ngầm, giữ phiên, đồng bộ MyBK): phiên còn.</summary>
    public static void MarkAlive() => Update(o =>
    {
        o["aliveAt"] = Now();
        o.Remove("expiredAt");
    });

    /// <summary>
    /// Thấy phiên SSO đã hết trên server (giữ phiên gặp trang đăng nhập, hay đồng bộ MyBK phải đi qua SSO mà SSO đòi mật khẩu).
    /// Ghi log Info một lần cho mỗi phiên: lần sau gặp lại (thử lại, restart app) thì thôi, tới khi đăng nhập lại.
    /// </summary>
    public static void Expired()
    {
        string? line = null;
        Update(o =>
        {
            if (o["expiredAt"] is not null) return;
            o["expiredAt"] = Now();
            line = SsoLifetime.ExpiredMessage(Local(o["loginAt"]), Local(o["aliveAt"]), DateTime.Now);
        });
        if (line is not null) Log.Info(line);
    }

    /// <summary>Người dùng tự đăng xuất: phiên do app kết thúc, không phải server, nên không đo và không báo hết hạn.</summary>
    public static void Forget() => Update(o =>
    {
        o.Clear();
        o["expiredAt"] = Now();
    });

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static DateTime? Local(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<long>(out var t) ? DateTimeOffset.FromUnixTimeSeconds(t).LocalDateTime : null;

    // Giữ phiên chạy trên thread nền, đồng bộ MyBK và đăng nhập chạy trên UI thread: khóa cả đọc lẫn ghi để không đè mất mốc.
    private static void Update(Action<JsonObject> change)
    {
        lock (Gate)
        {
            var o = JsonStore.ReadObject(StateFile);
            change(o);
            try { JsonStore.Write(StateFile, o); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Error("Không ghi được sso-session.json", e); }
        }
    }
}
