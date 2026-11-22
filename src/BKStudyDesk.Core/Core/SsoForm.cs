using System.Text.Json;

namespace SoHocTap.Core;

public static class SsoForm
{
    public static string FillScript(string user, string password) =>
        "(() => { const f = document.getElementById('fm1'), u = document.getElementById('username'), p = document.getElementById('password');"
        + " if (!f || !u || !p || document.querySelector('.g-recaptcha, [name*=\"captcha\" i], [id*=\"captcha\" i]')) return 'false';"
        + $" u.value = {JsonSerializer.Serialize(user, CoreJson.Default.String)}; p.value = {JsonSerializer.Serialize(password, CoreJson.Default.String)};"
        + " const b = f.querySelector('[type=submit]'); if (b) b.click(); else HTMLFormElement.prototype.submit.call(f); return 'true'; })()";
}
