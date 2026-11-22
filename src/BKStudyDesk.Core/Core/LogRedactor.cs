namespace SoHocTap.Core;

public static partial class LogRedactor
{
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

    public static string Redact(string message)
    {
        message = SecretRx().Replace(message, "$1=***");
        message = CasTicketRx().Replace(message, "$1-***");
        message = HeaderRx().Replace(message, "$1: ***");
        message = BearerRx().Replace(message, "Bearer ***");
        return EmailRx().Replace(message, "***@***");
    }
}
