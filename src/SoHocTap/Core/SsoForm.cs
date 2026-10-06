using System.Text.Json;

namespace SoHocTap.Core;

/// <summary>
/// Script điền trang đăng nhập SSO của trường (CAS: form #fm1, ô #username, #password; đo trên trang thật 06/10/2026) để tự đăng nhập lại.
/// Giá trị đi qua JSON nên dấu nháy, gạch chéo, thẻ HTML trong mật khẩu không phá script. Trang có captcha hay đổi cấu trúc thì không
/// điền, trả "false" để app báo đăng nhập lại như thường. Hàm thuần, không giữ mật khẩu.
/// </summary>
public static class SsoForm
{
    public static string FillScript(string user, string password) =>
        "(() => { const f = document.getElementById('fm1'), u = document.getElementById('username'), p = document.getElementById('password');"
        + " if (!f || !u || !p || document.querySelector('.g-recaptcha, [name*=\"captcha\" i], [id*=\"captcha\" i]')) return 'false';"
        + $" u.value = {JsonSerializer.Serialize(user)}; p.value = {JsonSerializer.Serialize(password)}; f.submit(); return 'true'; }})()";
}
