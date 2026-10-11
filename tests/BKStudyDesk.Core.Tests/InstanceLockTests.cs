using SoHocTap.Shell;

namespace BKStudyDesk.Core.Tests;

/// <summary>Khóa chạy một bản: mặc định một bản cho mỗi người dùng, dù cài ở thư mục nào; bản demo, bản thử tách riêng khi đặt BKSD_INSTANCE.</summary>
public class InstanceLockTests
{
    [Fact]
    public void One_instance_per_user_whatever_the_install_folder()
    {
        // Bản cài GitHub, bản Store, bản zip cũ ở thư mục khác nhau: trước đây chạy cùng lúc, đồng bộ và báo trùng (11/10/2026).
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
        Assert.Equal(InstanceLock.Key("1", @"E:\DEV\DEMO"), a);   // Windows: không phân biệt hoa thường
    }
}
