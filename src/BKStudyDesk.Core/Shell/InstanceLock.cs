using SoHocTap.Core;

namespace SoHocTap.Shell;

public static class InstanceLock
{
    public static string Key(string? isolate, string appRoot) =>
        string.IsNullOrWhiteSpace(isolate) ? AppInfo.Id + ".app"
            : AppInfo.Id + "." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(appRoot.ToLowerInvariant())))[..8];

    public static string Current { get; } = Key(Environment.GetEnvironmentVariable("BKSD_INSTANCE"), Paths.AppRoot);
}
