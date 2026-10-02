namespace SoHocTap.Core;

/// <summary>
/// Che dữ liệu nhạy cảm trước khi ghi log, ở một chỗ duy nhất (OWASP Logging Cheat Sheet: token, session id, mật khẩu phải được
/// che hoặc bỏ). Không phụ thuộc gì khác để unit test được.
/// </summary>
public static partial class LogRedactor
{
    // Che ở một chỗ trước khi ghi (OWASP Logging Cheat Sheet: token, session id phải được che): tham số token/ticket trong URL,
    // vé CAS ST-…, header Cookie/Set-Cookie/Authorization, Bearer token, địa chỉ email.
    [System.Text.RegularExpressions.GeneratedRegex(@"(?i)\b(wstoken|token|ticket|privatetoken|passport)=[^&\s""']+")]
    private static partial System.Text.RegularExpressions.Regex SecretRx();
    [System.Text.RegularExpressions.GeneratedRegex(@"\b(ST|TGT|PGT)-[A-Za-z0-9._-]{8,}")]
    private static partial System.Text.RegularExpressions.Regex CasTicketRx();
    [System.Text.RegularExpressions.GeneratedRegex(@"(?i)\b(cookie|set-cookie|authorization)\s*:\s*[^\r\n]+")]
    private static partial System.Text.RegularExpressions.Regex HeaderRx();
    [System.Text.RegularExpressions.GeneratedRegex(@"(?i)\bbearer\s+[A-Za-z0-9._~+/=-]+")]
    private static partial System.Text.RegularExpressions.Regex BearerRx();
    [System.Text.RegularExpressions.GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial System.Text.RegularExpressions.Regex EmailRx();

    /// <summary>Che dữ liệu nhạy cảm trong một dòng log.</summary>
    public static string Redact(string message)
    {
        message = SecretRx().Replace(message, "$1=***");
        message = CasTicketRx().Replace(message, "$1-***");
        message = HeaderRx().Replace(message, "$1: ***");
        message = BearerRx().Replace(message, "Bearer ***");
        return EmailRx().Replace(message, "***@***");
    }
}
