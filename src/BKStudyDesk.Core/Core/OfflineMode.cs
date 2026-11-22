namespace SoHocTap.Core;

public static class OfflineMode
{
    public static bool On => Config.Bool("app.offline", false);

    public static event Action<bool>? Changed;

    public static void Set(bool on)
    {
        if (on == On) return;
        Config.Set("app.offline", on);
        Log.Info(on ? "Bật chế độ Offline" : "Tắt chế độ Offline");
        Changed?.Invoke(on);
    }
}
