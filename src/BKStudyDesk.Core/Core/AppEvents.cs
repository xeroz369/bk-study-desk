namespace SoHocTap.Core;

public static class AppEvents
{
    public static event Action<Exception>? UnhandledError;

    public static void RaiseUnhandled(Exception e)
    {
        try { UnhandledError?.Invoke(e); }
        catch (Exception x) when (x is not OutOfMemoryException) { Log.Error("Handler của AppEvents.UnhandledError lỗi", x); }
    }
}
