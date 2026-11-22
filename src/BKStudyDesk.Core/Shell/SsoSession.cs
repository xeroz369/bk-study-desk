using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Shell;

internal static class SsoSession
{
    private static readonly object Gate = new();
    private static string StateFile => Paths.DataFile("sso-session.json");

    public static void MarkLogin() => Update(o =>
    {
        var now = Now();
        o["loginAt"] = now;
        o["aliveAt"] = now;
        o.Remove("expiredAt");
    });

    public static void MarkAlive() => Update(o =>
    {
        o["aliveAt"] = Now();
        o.Remove("expiredAt");
    });

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

    public static void Forget() => Update(o =>
    {
        o.Clear();
        o["expiredAt"] = Now();
    });

    public static DateTime? AliveAt()
    {
        lock (Gate)
        {
            var o = JsonStore.ReadObject(StateFile);
            return o["expiredAt"] is null ? Local(o["aliveAt"]) : null;
        }
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static DateTime? Local(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<long>(out var t) ? DateTimeOffset.FromUnixTimeSeconds(t).LocalDateTime : null;

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
