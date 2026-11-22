using System.Diagnostics;
using System.IO.Pipes;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace BKStudyDesk.Desktop.Platform;

internal static class SingleInstance
{
    private const string RestartArg = "--restart";
    private static string PipeName => InstanceLock.Current + ".show";

    public static Mutex? Acquire(string[] args)
    {
        var mutex = new Mutex(initiallyOwned: true, InstanceLock.Current, out var first);
        if (!first && args.Contains(RestartArg))
        {
            try { first = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (AbandonedMutexException) { first = true; }
        }
        if (first) return mutex;
        mutex.Dispose();
        ActivateExisting();
        return null;
    }

    private static void ActivateExisting()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(TimeSpan.FromSeconds(2));
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException) { Log.Warn($"Không gọi được bản đang chạy: {e.Message}"); }
    }

    public static void Listen(Action show) => _ = Task.Run(async () =>
    {
        while (true)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync().ConfigureAwait(false);
                show();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"Chờ bản thứ hai: {e.Message}");
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
        }
    });

    public static bool StartRestart()
    {
        if (Environment.ProcessPath is not { } exe) return false;
        try
        {
            Process.Start(new ProcessStartInfo(exe, RestartArg) { UseShellExecute = false });
            return true;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            Log.Error("Khởi động lại app", e);
            return false;
        }
    }
}
