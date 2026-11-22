using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SoHocTap.Files;

namespace SoHocTap.Tests;

internal sealed class TempStudy : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "bk-test-" + Guid.NewGuid().ToString("N"));
    public string Subjects => Path.Combine(Root, "Môn học");
    public const string LmsSub = "Tài liệu LMS";

    public TempStudy() => Directory.CreateDirectory(Subjects);

    public string Put(string relative, string content)
    {
        var path = Path.Combine(Subjects, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string Download(string content)
    {
        var path = Path.Combine(Root, "data", "tmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string Archive(string old) => Path.Combine(Root, "_Lưu trữ", "Bản cũ", Path.GetRelativePath(Root, old));

    public static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { }
    }
}

public class LmsPlacementTests
{
    private static Dictionary<string, string> Hashes(string subjectDir) =>
        JunkFilter.Files(subjectDir).GroupBy(f => TempStudy.Sha(f.FullName)).ToDictionary(g => g.Key, g => g.First().FullName);

    [Fact]
    public void UserFileWithSameContentIsNeverReplacedWhenLmsUpdates()
    {
        using var t = new TempStudy();
        var user = t.Put(Path.Combine("Giải tích 2", "BKeL", "GK251.pdf"), "đề của tôi");
        var dest = Path.Combine(t.Subjects, "Giải tích 2", TempStudy.LmsSub, "Chương 1", "GK251.pdf");

        var tmp1 = t.Download("đề của tôi");
        var hashes = Hashes(Path.Combine(t.Subjects, "Giải tích 2"));
        var p1 = LmsPlacement.Decide(null, hashes.GetValueOrDefault(TempStudy.Sha(tmp1)), dest, t.Subjects, TempStudy.LmsSub, File.Exists);
        Assert.Equal(PlacementKind.Duplicate, p1.Kind);
        Assert.Null(LmsPlacement.Apply(tmp1, p1, t.Archive));
        var (path1, dedup1) = LmsPlacement.IndexFields(p1, null, null, t.Subjects, TempStudy.LmsSub);
        Assert.Null(path1);
        Assert.Equal(user, dedup1);
        Assert.False(File.Exists(tmp1));

        var tmp2 = t.Download("đề đã sửa");
        hashes = Hashes(Path.Combine(t.Subjects, "Giải tích 2"));
        var p2 = LmsPlacement.Decide(path1, hashes.GetValueOrDefault(TempStudy.Sha(tmp2)), dest, t.Subjects, TempStudy.LmsSub, File.Exists);
        Assert.Equal(PlacementKind.New, p2.Kind);
        var final = LmsPlacement.Apply(tmp2, p2, t.Archive);

        Assert.Equal("đề của tôi", File.ReadAllText(user));
        Assert.Equal(dest, final);
        Assert.Equal("đề đã sửa", File.ReadAllText(dest));
        Assert.False(Directory.Exists(Path.Combine(t.Root, "_Lưu trữ")));
    }

    [Fact]
    public void OldIndexPointingAtUserFileIsNotTreatedAsOldVersion()
    {
        using var t = new TempStudy();
        var user = t.Put(Path.Combine("Kỹ thuật số", "OnGK", "C1.pdf"), "ghi chú của tôi");
        var dest = Path.Combine(t.Subjects, "Kỹ thuật số", TempStudy.LmsSub, "Chương 1", "C1.pdf");
        var tmp = t.Download("bản LMS mới");

        var p = LmsPlacement.Decide(user, null, dest, t.Subjects, TempStudy.LmsSub, File.Exists);
        Assert.Equal(PlacementKind.New, p.Kind);
        LmsPlacement.Apply(tmp, p, t.Archive);

        Assert.Equal("ghi chú của tôi", File.ReadAllText(user));
        Assert.Equal("bản LMS mới", File.ReadAllText(dest));
        Assert.False(Directory.Exists(Path.Combine(t.Root, "_Lưu trữ")));
    }

    [Fact]
    public void OwnOldVersionMovesToArchiveAndNewTakesItsPlace()
    {
        using var t = new TempStudy();
        var own = t.Put(Path.Combine("Phương pháp tính", TempStudy.LmsSub, "Bài giảng", "C1.pdf"), "bản 1");
        var tmp = t.Download("bản 2");

        var p = LmsPlacement.Decide(own, null, own, t.Subjects, TempStudy.LmsSub, File.Exists);
        Assert.Equal(PlacementKind.ReplaceOwn, p.Kind);
        Assert.Equal(own, LmsPlacement.Apply(tmp, p, t.Archive));

        Assert.Equal("bản 2", File.ReadAllText(own));
        Assert.Equal("bản 1", File.ReadAllText(t.Archive(own)));
    }

    [Fact]
    public void SameContentAsOwnFileKeepsPath()
    {
        using var t = new TempStudy();
        var own = t.Put(Path.Combine("Giải tích 2", TempStudy.LmsSub, "C1.pdf"), "x");
        var p = new Placement(PlacementKind.Duplicate, own);
        Assert.Equal((own, (string?)null), LmsPlacement.IndexFields(p, own, null, t.Subjects, TempStudy.LmsSub));
    }

    [Fact]
    public void SameContentAsAnotherLmsFileIsOnlyDedup()
    {
        using var t = new TempStudy();
        var other = t.Put(Path.Combine("Giải tích 2", TempStudy.LmsSub, "A.pdf"), "x");
        var p = new Placement(PlacementKind.Duplicate, other);
        Assert.Equal(((string?)null, other), LmsPlacement.IndexFields(p, null, null, t.Subjects, TempStudy.LmsSub));
    }

    [Theory]
    [InlineData("Giải tích 2/Tài liệu LMS/C1.pdf", true)]
    [InlineData("Vật lý 1/Thí nghiệm/Tài liệu LMS/Bài 5/a.pdf", true)]
    [InlineData("Giải tích 2/BKeL/GK.pdf", false)]
    [InlineData("Giải tích 2/Tải ngoài/Tài liệu LMS.pdf", false)]
    [InlineData("Tài liệu LMS/a.pdf", false)]
    public void AppOwnedOnlyInsideLmsSubfolder(string relative, bool owned)
    {
        using var t = new TempStudy();
        Assert.Equal(owned, LmsPlacement.IsAppOwned(Path.Combine(t.Subjects, relative), t.Subjects, TempStudy.LmsSub));
    }

    [Fact]
    public void OutsideSubjectsIsNeverAppOwned()
    {
        using var t = new TempStudy();
        Assert.False(LmsPlacement.IsAppOwned(Path.Combine(t.Root, "_Lưu trữ", "Tài liệu LMS", "x.pdf"), t.Subjects, TempStudy.LmsSub));
        Assert.False(LmsPlacement.IsAppOwned(null, t.Subjects, TempStudy.LmsSub));
    }

    [Fact]
    public void NfdLmsFolderNameIsStillAppOwned()
    {
        using var t = new TempStudy();
        var nfd = TempStudy.LmsSub.Normalize(NormalizationForm.FormD);
        Assert.True(LmsPlacement.IsAppOwned(Path.Combine(t.Subjects, "Giải tích 2", nfd, "a.pdf"), t.Subjects, TempStudy.LmsSub));
    }
}

public class JunkFilterTests
{
    private static TempStudy Tree()
    {
        var t = new TempStudy();
        t.Put("Kỹ thuật số/Lab1/bao-cao.pdf", "1");
        t.Put("Kỹ thuật số/Lab1/mach.kicad_pcb", "2");
        t.Put("Kỹ thuật số/Lab1/.history/.git/objects/ab", "rác");
        t.Put("Kỹ thuật số/Quartus/db/mach.qdb", "rác");
        t.Put("Kỹ thuật số/py/__pycache__/m.cpython-313.pyc", "rác");
        t.Put("Kỹ thuật số/web/node_modules/x/index.js", "rác");
        t.Put("Kỹ thuật số/Lab1/~$bao-cao.docx", "rác");
        t.Put("Kỹ thuật số/Lab1/.$mach.kicad_pcb.lck", "rác");
        t.Put("Kỹ thuật số/Lab1/a.tmp", "rác");
        t.Put("Kỹ thuật số/Lab1/mach.kicad_pcb.bkp", "rác");
        t.Put("Kỹ thuật số/Lab1/dang-tai.pdf.part", "rác");
        t.Put("Kỹ thuật số/desktop.ini", "rác");
        t.Put("Kỹ thuật số/Lab1/Thumbs.db", "rác");
        return t;
    }

    [Theory]
    [InlineData(".git", true)]
    [InlineData(".history", true)]
    [InlineData("db", true)]
    [InlineData("DB", true)]
    [InlineData("__pycache__", true)]
    [InlineData("node_modules", true)]
    [InlineData("Lab1", false)]
    [InlineData("dbms", false)]
    public void JunkDirs(string name, bool junk) => Assert.Equal(junk, JunkFilter.IsJunkDir(name));

    [Theory]
    [InlineData("~$bao-cao.docx", true)]
    [InlineData(".$x.lck", true)]
    [InlineData("a.tmp", true)]
    [InlineData("A.TMP", true)]
    [InlineData("x.bkp", true)]
    [InlineData("y.part", true)]
    [InlineData("desktop.ini", true)]
    [InlineData("thumbs.db", true)]
    [InlineData("bao-cao.pdf", false)]
    [InlineData("tmp.pdf", false)]
    public void JunkFiles(string name, bool junk) => Assert.Equal(junk, JunkFilter.IsJunkFile(name));

    [Fact]
    public void FilesSkipJunkTrees()
    {
        using var t = Tree();
        var names = JunkFilter.Files(Path.Combine(t.Subjects, "Kỹ thuật số")).Select(f => f.Name).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["bao-cao.pdf", "mach.kicad_pcb"], names);
    }

    [Fact]
    public void SubjectCountIgnoresJunk()
    {
        using var t = Tree();
        var s = Assert.Single(DocumentScan.ListSubjects(t.Subjects).OfType<JsonObject>());
        Assert.Equal("Kỹ thuật số", s["name"]!.GetValue<string>());
        Assert.Equal(2, s["files"]!.GetValue<int>());
    }

    [Fact]
    public void RecentTabIgnoresJunk()
    {
        using var t = Tree();
        var r = DocumentScan.SubjectFiles(t.Subjects, "Kỹ thuật số", f => f);
        Assert.Equal(2, r["total"]!.GetValue<int>());
        var names = (r["files"] as JsonArray)!.Select(f => f!["name"]!.GetValue<string>()).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["bao-cao.pdf", "mach.kicad_pcb"], names);
        var folders = (r["folders"] as JsonArray)!.Select(f => f!.GetValue<string>()).ToList();
        Assert.Equal(["Lab1", "Quartus", "py", "web"], folders);
    }

    [Fact]
    public void ListDirHidesJunkAndCountsWithoutIt()
    {
        using var t = Tree();
        var d = DocumentScan.ListDir(Path.Combine(t.Subjects, "Kỹ thuật số"), f => f);
        var dirs = (d["dirs"] as JsonArray)!.OfType<JsonObject>().ToDictionary(x => x["name"]!.GetValue<string>(), x => x["count"]!.GetValue<int>());
        Assert.Equal(0, dirs["Quartus"]);
        Assert.Equal(2, dirs["Lab1"]);
        Assert.Empty((d["files"] as JsonArray)!);
        var lab = DocumentScan.ListDir(Path.Combine(t.Subjects, "Kỹ thuật số", "Lab1"), f => f);
        Assert.DoesNotContain(".history", (lab["dirs"] as JsonArray)!.Select(x => x!["name"]!.GetValue<string>()));
        Assert.Equal(2, (lab["files"] as JsonArray)!.Count);
    }

    [Fact]
    public void ChangesInsideJunkDirsDoNotBustTheCache()
    {
        using var t = Tree();
        var subject = Path.Combine(t.Subjects, "Kỹ thuật số");
        var before = Settled(subject);
        t.Put("Kỹ thuật số/Lab1/.history/.git/objects/cd", "rác mới");
        t.Put("Kỹ thuật số/Quartus/db/mach2.qdb", "rác mới");
        Assert.Equal(before, Settled(subject));
        t.Put("Kỹ thuật số/Lab1/bao-cao-2.pdf", "3");
        Assert.NotEqual(before, Settled(subject));
    }

    private static string Settled(string root)
    {
        var stamp = DocumentScan.TreeStamp(root);
        for (var i = 0; i < 20; i++)
        {
            Thread.Sleep(50);
            var again = DocumentScan.TreeStamp(root);
            if (again == stamp) return stamp;
            stamp = again;
        }
        return stamp;
    }
}

public class NameMatchTests
{
    private static readonly string Nfc = "Giải tích 2";
    private static readonly string Nfd = "Giải tích 2".Normalize(NormalizationForm.FormD);

    [Fact]
    public void NfdAndNfcAreTheSameName()
    {
        Assert.NotEqual(Nfc, Nfd);
        Assert.True(NameMatch.Same(Nfc, Nfd));
        Assert.True(NameMatch.Same("giải TÍCH 2", Nfd));
    }

    [Fact]
    public void DictionaryKeyedByNfc()
    {
        var map = new Dictionary<string, int>(NameMatch.NfcIgnoreCase) { [Nfd] = 1 };
        Assert.True(map.ContainsKey(Nfc));
    }

    [Theory]
    [InlineData("Giải tích 2", "giai tich")]
    [InlineData("Kỹ thuật số", "KY THUAT")]
    [InlineData("Đại số tuyến tính", "dai so")]
    public void FilterIgnoresDiacritics(string name, string query) => Assert.True(NameMatch.ContainsFolded(name, query));

    [Fact]
    public void FoldedMatchWorksOnNfdNames() => Assert.True(NameMatch.ContainsFolded("GT2_" + Nfd + ".pdf", "giai tich 2"));

    [Theory]
    [InlineData("Giải tích 1", "Giải tích 1", true)]
    [InlineData("Vật lý 1 (Thí nghiệm)", "Vật lý 1", true)]
    [InlineData("Giải tích 12", "Giải tích 1", false)]
    [InlineData("Giải tích 1 nâng cao", "Giải tích 1", false)]
    [InlineData("Vật lý 1 (", "Vật lý 1", false)]
    public void SubjectIsExactNotPrefix(string item, string subject, bool same) => Assert.Equal(same, NameMatch.SubjectIs(item, subject));

    [Fact]
    public void SubjectIsHandlesNfd() => Assert.True(NameMatch.SubjectIs(Nfd + " (BTL)", Nfc));
}
