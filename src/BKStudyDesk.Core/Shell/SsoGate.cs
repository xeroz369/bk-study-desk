namespace SoHocTap.Shell;

public static class SsoGate
{
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static int _fillSpent;

    public static event Action? AutoFillFailed;

    public static async Task<IDisposable> EnterAsync(CancellationToken ct)
    {
        await Lock.WaitAsync(ct).ConfigureAwait(false);
        return new Lane();
    }

    public static bool Busy => Lock.CurrentCount == 0;

    public static bool TryClaimFill(bool active) => active && Interlocked.CompareExchange(ref _fillSpent, 1, 0) == 0;

    public static void FillResult(bool ok)
    {
        if (ok) Interlocked.Exchange(ref _fillSpent, 0);
        else AutoFillFailed?.Invoke();
    }

    public static void FillAborted() => Interlocked.Exchange(ref _fillSpent, 0);

    public static void SignedIn() => Interlocked.Exchange(ref _fillSpent, 0);

    public static bool FillSpent => Volatile.Read(ref _fillSpent) == 1;

    private sealed class Lane : IDisposable
    {
        private int _done;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0) Lock.Release();
        }
    }
}
