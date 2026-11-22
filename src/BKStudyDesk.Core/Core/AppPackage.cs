using System.Reflection;
using System.Runtime.InteropServices;

namespace SoHocTap.Core;

public static class AppPackage
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int length, char[]? name);

    private const int AppModelErrorNoPackage = 15700;

    public static bool IsPackaged { get; } = Detect();

    public static string Product { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "BK Study Desk";

    public static string Id { get; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "BKStudyDesk";

    private static bool Detect()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            var len = 0;
            return GetCurrentPackageFullName(ref len, null) != AppModelErrorNoPackage;
        }
        catch (EntryPointNotFoundException) { return false; }
    }
}
