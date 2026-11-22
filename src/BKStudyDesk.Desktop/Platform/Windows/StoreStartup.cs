using System.Runtime.InteropServices;
using SoHocTap.Core;
using Windows.ApplicationModel;

namespace BKStudyDesk.Desktop.Platform.Windows;

internal static class StoreStartup
{
    private const string TaskId = "BKStudyDeskStartup";

    public static bool LaunchedAtLogin
    {
        get
        {
            try { return AppInstance.GetActivatedEventArgs()?.Kind == global::Windows.ApplicationModel.Activation.ActivationKind.StartupTask; }
            catch (Exception e) when (e is COMException or InvalidOperationException) { return false; }
        }
    }

    public static bool Enabled
    {
        get
        {
            try { return On(Task.Run(async () => (await StartupTask.GetAsync(TaskId)).State).Result); }
            catch (AggregateException e) { Log.Warn($"Đọc StartupTask lỗi: {e.InnerException?.Message}"); return false; }
        }
    }

    public static bool Set(bool on)
    {
        try
        {
            return Task.Run(async () =>
            {
                var task = await StartupTask.GetAsync(TaskId);
                if (!on) { task.Disable(); return false; }
                var state = await task.RequestEnableAsync();
                if (!On(state)) Log.Info($"StartupTask không bật được: {state}");
                return On(state);
            }).Result;
        }
        catch (AggregateException e) { Log.Warn($"Đổi StartupTask lỗi: {e.InnerException?.Message}"); return Enabled; }
    }

    private static bool On(StartupTaskState s) => s is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
}
