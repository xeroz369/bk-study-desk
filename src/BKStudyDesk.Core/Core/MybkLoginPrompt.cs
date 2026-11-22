namespace SoHocTap.Core;

public static class MybkLoginPrompt
{
    public const long StaleAfter = 3 * 86400;

    public const long RegistrationLead = 3 * 86400;

    public static bool Needed(bool onMybkPage, long now, long? syncedAt, IEnumerable<(long Start, long End)> registrations) =>
        onMybkPage || syncedAt is not { } t || now - t > StaleAfter || registrations.Any(r => r.End > now && r.Start - now <= RegistrationLead);
}
