namespace SoHocTap.Core;

/// <summary>
/// Is someone at the computer? Background work (scheduled sync, message poll, library and update checks, automatic SSO sign-in)
/// runs only while this says active (docs/plans/2026-11-06-on-demand-sync.md, section 3). Buttons the user presses always run.
/// Active: the last keyboard or mouse input is within <see cref="IdleLimit"/>. Where the shell can read system-wide input time
/// (Windows, <see cref="SystemIdle"/>) that decides; elsewhere input in the app window (<see cref="Touch"/>) or the window having
/// focus does. Pure state with an injectable clock so tests need no window.
/// </summary>
public static class UserActivity
{
    private static readonly object Gate = new();
    private static DateTimeOffset _lastTouch = DateTimeOffset.UtcNow;   // app start counts as activity
    private static bool _focused, _wasActive = true;

    /// <summary>No input for this long means away (app.idleMinutes, set by the shell; default 10 minutes).</summary>
    public static TimeSpan IdleLimit { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Time since the last input anywhere on the system; null where the platform cannot tell (then app input is used).</summary>
    public static Func<TimeSpan?>? SystemIdle { get; set; }

    /// <summary>Clock, replaceable in tests.</summary>
    public static Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;

    /// <summary>Raised (on the caller of <see cref="Poll"/>) when the state flips: true = back after being away, false = went away.</summary>
    public static event Action<bool>? Changed;

    /// <summary>Input in the app window (pointer, key, wheel).</summary>
    public static void Touch() { lock (Gate) _lastTouch = Now(); }

    /// <summary>App window gained or lost focus. Focus counts as present where system idle time is not available.</summary>
    public static void SetFocused(bool on)
    {
        lock (Gate)
        {
            _focused = on;
            if (on) _lastTouch = Now();
        }
    }

    /// <summary>How long since the last input we can see.</summary>
    public static TimeSpan Idle
    {
        get
        {
            TimeSpan? system = null;
            try { system = SystemIdle?.Invoke(); }
            catch (Exception e) when (e is not OutOfMemoryException) { _ = e; }   // probe failed: fall back to app input
            if (system is { } s) return s;
            lock (Gate) return _focused ? TimeSpan.Zero : Now() - _lastTouch;
        }
    }

    public static bool Active => Idle < IdleLimit;

    /// <summary>Check for a change (the shell calls this every few seconds); raises <see cref="Changed"/> when it flips.</summary>
    public static bool Poll()
    {
        var now = Active;
        bool flipped;
        lock (Gate)
        {
            flipped = now != _wasActive;
            _wasActive = now;
        }
        if (flipped) Changed?.Invoke(now);
        return now;
    }

    /// <summary>Tests: back to the start state.</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            _lastTouch = Now();
            _focused = false;
            _wasActive = true;
        }
        SystemIdle = null;
        IdleLimit = TimeSpan.FromMinutes(10);
    }
}
