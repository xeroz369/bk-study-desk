using BKStudyDesk.Setup;

namespace SoHocTap.Tests;

public class InstallTargetTests
{
    private static readonly string Base = Path.Combine(Path.GetTempPath(), "target-test");
    private static string P(params string[] parts) => Path.Combine([Base, .. parts]);

    private static (string? Dir, string? Error) Resolve(string picked, string[] dirs, string[] files, string? installedId = "BKStudyDesk") =>
        InstallTarget.Resolve(picked, d => dirs.Contains(d), d => files.Where(f => Path.GetDirectoryName(f) == d), f => files.Contains(f),
            "BKStudyDesk", _ => installedId);

    [Fact]
    public void Resolve_ParentFolder_AppendsAppFolder() =>
        Assert.Equal(P("Apps", "BKStudyDesk"), Resolve(P("Apps"), [P("Apps")], [P("Apps", "other.txt")]).Dir);

    [Fact]
    public void Resolve_AppFolderAlready_KeepsIt() =>
        Assert.Equal(P("Apps", "BKStudyDesk"), Resolve(P("Apps", "BKStudyDesk"), [], []).Dir);

    [Fact]
    public void Resolve_ExistingInstall_Allowed() =>
        Assert.Equal(P("Apps", "BKStudyDesk"), Resolve(P("Apps", "BKStudyDesk"), [P("Apps", "BKStudyDesk")], [P("Apps", "BKStudyDesk", "Update.exe")]).Dir);

    [Fact]
    public void Resolve_EmptyExistingFolder_Allowed() =>
        Assert.Equal(P("Apps", "BKStudyDesk"), Resolve(P("Apps", "BKStudyDesk"), [P("Apps", "BKStudyDesk")], []).Dir);

    [Fact]
    public void Resolve_FolderWithOtherFiles_Refused()
    {
        var r = Resolve(P("Apps", "BKStudyDesk"), [P("Apps", "BKStudyDesk")], [P("Apps", "BKStudyDesk", "baitap.docx")]);
        Assert.Null(r.Dir);
        Assert.NotNull(r.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Apps")]
    [InlineData("   ")]
    public void Resolve_NotFullPath_Refused(string picked) => Assert.Null(Resolve(picked, [], []).Dir);

    [Fact]
    public void Resolve_DriveRoot_InstallsInSubfolder()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Equal(@"D:\BKStudyDesk", Resolve(@"D:\", [@"D:\"], [@"D:\note.txt"]).Dir);
    }

    [Fact]
    public void Resolve_OtherAppInstalledThere_Refused()
    {
        // Bản cài của gói khác (vd. bản test) trong thư mục đó: không cài đè, tránh ghi lẫn hai app.
        var r = Resolve(P("Apps", "BKStudyDesk"), [P("Apps", "BKStudyDesk")], [P("Apps", "BKStudyDesk", "Update.exe")], installedId: "BKStudyDeskTest");
        Assert.Null(r.Dir);
        Assert.NotNull(r.Error);
    }
}