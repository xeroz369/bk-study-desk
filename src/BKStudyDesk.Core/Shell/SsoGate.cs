namespace SoHocTap.Shell;

/// <summary>
/// One coordinator for every pass through HCMUT SSO (docs/plans/2026-11-06-on-demand-sync.md, section 6). LMS token refresh
/// (LoginService) and MyBK re-entry (MybkBrowser) used to sign in on their own with no shared lock and their own "tried once" flags.
/// Now: one SSO pass at a time (a second caller waits, then usually finds the fresh session and needs no password); one automatic
/// password fill per app run, only while the user is active, shared by both paths; a failed fill stops all automatic fills until
/// the user signs in (the app then asks the user, it never retries by itself).
/// </summary>
public static class SsoGate
{
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static int _fillSpent;   // 1: the automatic fill of this run is used and did not succeed (yet)

    /// <summary>An automatic fill failed (wrong password, captcha, page changed): the shell may tell the user.</summary>
    public static event Action? AutoFillFailed;

    /// <summary>Take the SSO lane; dispose to release. Waits while another sign-in is in progress.</summary>
    public static async Task<IDisposable> EnterAsync(CancellationToken ct)
    {
        await Lock.WaitAsync(ct).ConfigureAwait(false);
        return new Lane();
    }

    /// <summary>Someone is going through SSO right now.</summary>
    public static bool Busy => Lock.CurrentCount == 0;

    /// <summary>
    /// May the app fill the saved account now? Only while the user is <paramref name="active"/> and the one fill of this run is not
    /// spent. true claims it: report the outcome with <see cref="FillResult"/>.
    /// </summary>
    public static bool TryClaimFill(bool active) => active && Interlocked.CompareExchange(ref _fillSpent, 1, 0) == 0;

    /// <summary>Outcome of a claimed fill: success frees the claim for a later expiry, failure keeps it spent and raises AutoFillFailed.</summary>
    public static void FillResult(bool ok)
    {
        if (ok) Interlocked.Exchange(ref _fillSpent, 0);
        else AutoFillFailed?.Invoke();
    }

    /// <summary>A claimed fill never got to submit (network error, app closing): free the claim without counting it as failed.</summary>
    public static void FillAborted() => Interlocked.Exchange(ref _fillSpent, 0);

    /// <summary>The user signed in (or a session came back without a password): automatic fill is allowed again later.</summary>
    public static void SignedIn() => Interlocked.Exchange(ref _fillSpent, 0);

    /// <summary>The automatic fill was tried this run and failed: waiting for the user.</summary>
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
