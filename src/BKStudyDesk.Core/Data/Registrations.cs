namespace SoHocTap.Data;

public static class Registrations
{
    public static List<MybkRegistration> Upcoming(IEnumerable<MybkRegistration> all, long now) =>
        [.. all.Where(r => r.End > now).OrderBy(r => r.Start <= now ? 0 : 1).ThenBy(r => r.Start <= now ? r.End : r.Start)];

    public static bool IsOpen(MybkRegistration r, long now) => r.Start <= now && now < r.End;
}
