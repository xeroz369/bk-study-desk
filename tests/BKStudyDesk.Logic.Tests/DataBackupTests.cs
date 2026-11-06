using System.IO.Compression;
using SoHocTap.Core;

namespace SoHocTap.Tests;

/// <summary>Sao lưu dữ liệu: chỉ file trong danh sách cho phép, không bao giờ kèm tin nhắn, phiên đăng nhập, token, cookie, log.</summary>
public sealed class DataBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bk-backup-test-" + Guid.NewGuid().ToString("N"));

    public DataBackupTests()
    {
        foreach (var f in new[]
        {
            "mybk.json", "lms.json", "ket-qua.json", "config.json",
            "lms-messages.json", "sso-session.json", "inbox-read.json", "app.log", "window.json",
            "secrets/lms.json", "webview/EBWebView/Default/Cookies", "lms-media/abc.png", "tmp/x.json", "backup/ket-qua.json",
            "lms-course/123/info.json", "lms-quiz/9.json", "lms-quiz/9.json.part", "packs/gt2.studypack.json",
        })
        {
            var p = Path.Combine(_root, f);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, "{}");
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    [Fact]
    public void Entries_are_allowlisted_only()
    {
        var e = DataBackup.Entries(_root);
        Assert.Equal(["config.json", "mybk.json", "lms.json", "ket-qua.json", "lms-course/123/info.json", "lms-quiz/9.json", "packs/gt2.studypack.json"], e);
        Assert.DoesNotContain(e, x => x.Contains("message") || x.Contains("secrets") || x.Contains("webview") || x.Contains("sso") || x.Contains("lms-media") || x.EndsWith(".log"));
    }

    [Fact]
    public void Write_makes_zip_with_readme()
    {
        var dest = Path.Combine(_root, "out.zip");
        var (count, skipped) = DataBackup.Write(_root, dest, "1.2.9", new DateTime(2026, 11, 5, 14, 30, 0));
        Assert.Equal(7, count);
        Assert.Empty(skipped);
        using var zip = ZipFile.OpenRead(dest);
        Assert.Equal(8, zip.Entries.Count);
        Assert.Contains(zip.Entries, x => x.FullName == "DOC-TRUOC.txt");
        Assert.DoesNotContain(zip.Entries, x => x.FullName.Contains("message"));
        Assert.False(File.Exists(dest + ".tmp"));
    }
}
