using System.Diagnostics;
using System.IO.Pipes;
using SoHocTap.Core;
using SoHocTap.Shell;

namespace BKStudyDesk.Desktop.Platform;

/// <summary>
/// Chỉ cho chạy một bản (như 1.x, Program.cs): khóa là Mutex có tên AppInfo.InstanceKey (theo thư mục app, nên bản demo, bản thử chạy
/// song song được). Bản thứ hai gọi vào named pipe của bản đang chạy để nó hiện cửa sổ (kể cả khi đang nằm dưới khay) rồi thoát.
/// Named pipe thay cho EventWaitHandle có tên của 1.x vì macOS, Linux không có EventWaitHandle có tên; CurrentUserOnly: tài khoản khác
/// trên máy không gọi được.
/// </summary>
internal static class SingleInstance
{
    private const string RestartArg = "--restart";
    private static string PipeName => AppInfo.InstanceKey + ".show";

    /// <summary>Giữ khóa chạy một bản; null khi đã có bản khác đang chạy (bản này báo cho nó rồi nên thoát).</summary>
    public static Mutex? Acquire(string[] args)
    {
        var mutex = new Mutex(initiallyOwned: true, AppInfo.InstanceKey, out var first);
        // Khởi động lại (đổi ngôn ngữ): bản cũ đang thoát, đợi nó nhả khóa tối đa 5 giây.
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
            // Kết nối là tín hiệu, không cần gửi gì: bản đang chạy hiện cửa sổ ngay khi có kết nối.
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(TimeSpan.FromSeconds(2));
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException) { Log.Warn($"Không gọi được bản đang chạy: {e.Message}"); }
    }

    /// <summary>Bản đang chạy: gọi <paramref name="show"/> (từ thread nền) mỗi khi có bản thứ hai được mở.</summary>
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

    /// <summary>Mở bản mới với --restart (bản mới đợi bản này nhả khóa); true khi đã mở, bản này cần thoát.</summary>
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
