namespace SoHocTap.Core;

public static class UserActivity
{
    private static readonly object Gate = new();
    private static DateTimeOffset _lastTouch = DateTimeOffset.UtcNow;
    private static bool _focused, _wasActive = true;

    public static TimeSpan IdleLimit { get; set; } = TimeSpan.FromMinutes(10);

    public static Func<TimeSpan?>? SystemIdle { get; set; }

    public static Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;

    public static event Action<bool>? Changed;

    public static void Touch() { lock (Gate) _lastTouch = Now(); }

    public static void SetFocused(bool on)
    {
        lock (Gate)
        {
            _focused = on;
            if (on) _lastTouch = Now();
        }
    }

    public static TimeSpan Idle
    {
        get
        {
            TimeSpan? system = null;
            try { system = SystemIdle?.Invoke(); }
            catch (Exception e) when (e is not OutOfMemoryException) { _ = e; }
            if (system is { } s) return s;
            lock (Gate) return _focused ? TimeSpan.Zero : Now() - _lastTouch;
        }
    }

    public static bool Active => Idle < IdleLimit;

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
