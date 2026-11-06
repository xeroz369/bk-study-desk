using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace BKStudyDesk.Desktop.Platform;

/// <summary>Time since the last keyboard or mouse input anywhere in the Windows session (GetLastInputInfo), for UserActivity.</summary>
[SupportedOSPlatform("windows")]
internal static class IdleTime
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    /// <summary>null if Windows does not answer (UserActivity then falls back to input in the app window).</summary>
    public static TimeSpan? Read()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return null;
        // Both are milliseconds since boot on a 32 bit counter that wraps every 49.7 days: unsigned subtraction handles the wrap.
        var idle = unchecked((uint)Environment.TickCount - info.Time);
        return TimeSpan.FromMilliseconds(idle);
    }
}
