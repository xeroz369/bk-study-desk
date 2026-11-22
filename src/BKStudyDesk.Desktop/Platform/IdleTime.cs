using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace BKStudyDesk.Desktop.Platform;

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

    public static TimeSpan? Read()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return null;
        var idle = unchecked((uint)Environment.TickCount - info.Time);
        return TimeSpan.FromMilliseconds(idle);
    }
}
