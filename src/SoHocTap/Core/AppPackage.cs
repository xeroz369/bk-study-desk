using System.Reflection;
using System.Runtime.InteropServices;

namespace SoHocTap.Core;

/// <summary>
/// App đang chạy dạng MSIX (cài từ Microsoft Store) hay chạy thẳng từ folder (bản zip trên GitHub).
/// Bản MSIX: folder cài đặt read-only, autostart phải qua StartupTask, AUMID do package quyết định.
/// </summary>
public static class AppPackage
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int length, char[]? name);

    private const int AppModelErrorNoPackage = 15700;

    public static bool IsPackaged { get; } = Detect();

    /// <summary>Tên sản phẩm trong csproj (Sổ học tập / BK Study Desk), dùng đặt tên folder mặc định.</summary>
    public static string Product { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "BK Study Desk";

    /// <summary>Tên assembly (SoHocTap / BKStudyDesk), dùng làm tên folder data trong LocalAppData.</summary>
    public static string Id { get; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "BKStudyDesk";

    private static bool Detect()
    {
        try
        {
            var len = 0;
            return GetCurrentPackageFullName(ref len, null) != AppModelErrorNoPackage;
        }
        catch (EntryPointNotFoundException) { return false; }   // Windows quá cũ, chắc chắn không phải MSIX
    }
}
