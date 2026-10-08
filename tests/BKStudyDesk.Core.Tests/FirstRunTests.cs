using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

/// <summary>Chọn thư mục lần đầu: chọn gốc ổ thì dùng thư mục con của app; đường dẫn tương đối bị từ chối, không ghi config.</summary>
public class FirstRunTests
{
    [Fact]
    public void Picked_DriveRoot_UsesAppSubfolder()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        Assert.NotEqual(root, FirstRun.Picked(root));
        Assert.StartsWith(root, FirstRun.Picked(root));
        var sub = Path.Combine(Path.GetTempPath(), "bk");
        Assert.Equal(sub, FirstRun.Picked(sub));
    }

    [Fact]
    public void SaveFolder_RelativePath_Rejected() => Assert.NotNull(FirstRun.SaveFolder("tai-lieu"));
}
