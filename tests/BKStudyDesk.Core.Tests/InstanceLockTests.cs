using SoHocTap.Shell;

namespace BKStudyDesk.Core.Tests;

public class InstanceLockTests
{
    [Fact]
    public void One_instance_per_user_whatever_the_install_folder()
    {
        Assert.Equal(InstanceLock.Key(null, @"E:\Apps\BKStudyDesk"), InstanceLock.Key(null, @"C:\Users\sv\AppData\Local\BKStudyDesk"));
        Assert.Equal(InstanceLock.Key(null, "x"), InstanceLock.Key("", "y"));
        Assert.Equal(InstanceLock.Key(null, "x"), InstanceLock.Key("  ", "y"));
    }

    [Fact]
    public void Isolated_runs_get_a_key_per_folder()
    {
        var a = InstanceLock.Key("1", @"E:\Dev\demo");
        Assert.NotEqual(InstanceLock.Key(null, @"E:\Dev\demo"), a);
        Assert.NotEqual(InstanceLock.Key("1", @"E:\Dev\test"), a);
        Assert.Equal(InstanceLock.Key("1", @"E:\DEV\DEMO"), a);
    }
}
