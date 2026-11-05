using SoHocTap.Core;

namespace BKStudyDesk.Logic.Tests;

[CollectionDefinition("activity", DisableParallelization = true)]
public sealed class ActivityCollection;

/// <summary>Background work runs only while someone is at the computer (docs/plans/2026-11-06-on-demand-sync.md, section 3).</summary>
[Collection("activity")]
public sealed class UserActivityTests : IDisposable
{
    private DateTimeOffset _now = new(2026, 11, 6, 1, 0, 0, TimeSpan.Zero);

    public UserActivityTests()
    {
        UserActivity.Now = () => _now;
        UserActivity.Reset();
    }

    public void Dispose()
    {
        UserActivity.Now = () => DateTimeOffset.UtcNow;
        UserActivity.Reset();
    }

    [Fact]
    public void System_idle_decides_when_available()
    {
        UserActivity.SystemIdle = () => TimeSpan.FromMinutes(3);
        Assert.True(UserActivity.Active);
        UserActivity.SystemIdle = () => TimeSpan.FromMinutes(11);
        UserActivity.SetFocused(true);   // focus does not override a real system reading
        Assert.False(UserActivity.Active);
    }

    [Fact]
    public void Without_system_idle_app_input_and_focus_count()
    {
        _now += TimeSpan.FromMinutes(11);
        Assert.False(UserActivity.Active);
        UserActivity.Touch();
        Assert.True(UserActivity.Active);
        _now += TimeSpan.FromMinutes(30);
        UserActivity.SetFocused(true);
        Assert.True(UserActivity.Active);   // window in front: present
        UserActivity.SetFocused(false);
        _now += TimeSpan.FromMinutes(10);
        Assert.False(UserActivity.Active);
    }

    [Fact]
    public void Failing_probe_falls_back_to_app_input()
    {
        UserActivity.SystemIdle = () => throw new InvalidOperationException("no session");
        Assert.True(UserActivity.Active);
        UserActivity.SystemIdle = () => null;
        _now += TimeSpan.FromMinutes(20);
        Assert.False(UserActivity.Active);
    }

    [Fact]
    public void Poll_reports_each_flip_once()
    {
        var seen = new List<bool>();
        void On(bool a) => seen.Add(a);
        UserActivity.Changed += On;
        try
        {
            var idle = TimeSpan.Zero;
            UserActivity.SystemIdle = () => idle;
            UserActivity.Poll();
            idle = TimeSpan.FromMinutes(15);
            UserActivity.Poll();
            UserActivity.Poll();
            idle = TimeSpan.FromSeconds(2);
            UserActivity.Poll();
            Assert.Equal([false, true], seen);
        }
        finally { UserActivity.Changed -= On; }
    }
}
