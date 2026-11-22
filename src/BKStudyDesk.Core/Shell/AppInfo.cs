using SoHocTap.Core;

namespace SoHocTap.Shell;

internal static class AppInfo
{
    public const string Id = "BKStudyDesk";
    public const string Name = "BK Study Desk";

    public static string InstanceKey { get; } =
        Id + "." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Paths.AppRoot.ToLowerInvariant())))[..8];

    public const string Author = "xeroz369";
    public const string Repo = "https://github.com/xeroz369/bk-study-desk";
    public const string Issues = Repo + "/issues";
    public const string License = "AGPL-3.0";
    public const string Support = "";

    public static string Version { get; } =
        (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(System.Reflection.Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly)
            ?.InformationalVersion ?? "").Split('+')[0];
}
