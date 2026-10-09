using SoHocTap.Core;

namespace SoHocTap.Tests;

public class AppDirsTests
{
    private static readonly string Base = Path.Combine(Path.GetTempPath(), "appdirs-test");
    private static string P(params string[] parts) => Path.Combine([Base, .. parts]);

    private static (string Root, InstallKind Kind) Resolve(string exeDir, string[] dirs, string[] files, bool packaged = false, bool appImage = false) =>
        AppDirs.Resolve(exeDir, packaged, P("Local"), "BKStudyDesk", d => dirs.Contains(d), f => files.Contains(f), appImage);

    [Fact]
    public void Resolve_Store_UsesLocalAppData()
    {
        var r = Resolve(P("WindowsApps", "x"), [], [], packaged: true);
        Assert.Equal(P("Local", "BKStudyDesk"), r.Root);
        Assert.Equal(InstallKind.Store, r.Kind);
    }

    [Fact]
    public void Resolve_Velopack_UsesParentOfCurrent()
    {
        var r = Resolve(P("Local", "BKStudyDesk", "current"), [], [P("Local", "BKStudyDesk", "Update.exe")]);
        // Ngoài thư mục cài: chạy lại Setup.exe (cài đè) xóa cả thư mục cài, data phải nằm ở chỗ khác.
        Assert.Equal(P("Local", "BKStudyDesk.Data"), r.Root);
    }

    [Fact]
    public void Resolve_Velopack_CustomFolder_DataStaysInLocalAppData()
    {
        // Người dùng chọn cài vào ổ khác: data vẫn ở LocalAppData (riêng tư theo tài khoản), theo pack id chứ không theo tên thư mục.
        var r = AppDirs.Resolve(P("Apps", "BK app", "current"), false, P("Local"), "BKStudyDeskTest", _ => false,
            f => f == P("Apps", "BK app", "Update.exe"));
        Assert.Equal(P("Local", "BKStudyDeskTest.Data"), r.Root);
        Assert.Equal(InstallKind.Installed, r.Kind);
        Assert.Equal(InstallKind.Installed, r.Kind);
    }

    [Fact]
    public void Resolve_AppImage_UsesDataHome()
    {
        var r = Resolve(P("tmp", ".mount_BKxyz", "usr", "bin"), [], [], appImage: true);
        Assert.Equal(P("Local", "BKStudyDesk"), r.Root);
        Assert.Equal(InstallKind.Installed, r.Kind);
    }

    [Fact]
    public void Resolve_PortableAppFolder_UsesParent()
    {
        var r = Resolve(P("S", "app"), [], []);
        Assert.Equal(P("S"), r.Root);
        Assert.Equal(InstallKind.Portable, r.Kind);
    }

    [Fact]
    public void Resolve_PortableFindsExistingData()
    {
        var r = Resolve(P("S", "app"), [P("S", "data")], []);
        Assert.Equal(P("S"), r.Root);
        Assert.Equal(InstallKind.Portable, r.Kind);
    }

    [Fact]
    public void Resolve_PortableExeFolderWithoutApp_UsesExeFolder()
    {
        var r = Resolve(P("Z", "BKStudyDesk"), [], []);
        Assert.Equal(P("Z", "BKStudyDesk"), r.Root);
    }

    [Fact]
    public void Resolve_ZipBeside_Installed_KeepsSeparateRoots()
    {
        string[] dirs = [P("Z", "data")];
        string[] files = [P("Local", "BKStudyDesk", "Update.exe")];
        var zip = Resolve(P("Z", "app"), dirs, files);
        var installed = Resolve(P("Local", "BKStudyDesk", "current"), dirs, files);
        Assert.NotEqual(zip.Root, installed.Root);
        Assert.Equal(InstallKind.Portable, zip.Kind);
        Assert.Equal(InstallKind.Installed, installed.Kind);
    }

    [Fact]
    public void Resolve_CurrentFolderWithoutUpdateExe_IsPortable()
    {
        var r = Resolve(P("Q", "current"), [], []);
        Assert.Equal(InstallKind.Portable, r.Kind);
        Assert.Equal(P("Q", "current"), r.Root);
    }

    [Fact]
    public void Resolve_EmptyDataHome_Throws() =>
        Assert.Throws<ArgumentException>(() => AppDirs.Resolve(P("tmp", "x"), false, "", "BKStudyDesk", _ => false, _ => false, appImage: true));
}