using SoHocTap.Files;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests;

/// <summary>Downloads organizer: plan reads only, move runs on the checked list, per-file errors do not stop the rest.</summary>
public sealed class OrganizeDownloadsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bksd-dl-" + Guid.NewGuid().ToString("N"));
    private string Downloads => Path.Combine(_root, "Downloads");
    private string Subjects => Path.Combine(_root, "Môn học");
    private static readonly string[] Exts = [".pdf", ".docx"];
    private static Dictionary<string, HashSet<string>> Known() => new() { ["Giải tích 2"] = ["MT1005"] };

    public OrganizeDownloadsTests() => Directory.CreateDirectory(Downloads);

    public void Dispose() { try { Directory.Delete(_root, true); } catch (IOException) { } }

    private string Put(string rel)
    {
        var p = Path.Combine(Downloads, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, rel);
        return p;
    }

    private List<Organizer.DownloadMove> Plan() => Organizer.PlanDownloads(Downloads, Subjects, "Tải ngoài", Exts, Known());

    private static bool Move(Organizer.DownloadMove m)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(m.Dest)!);
        File.Move(m.Source, m.Dest);
        return true;
    }

    [Fact]
    public void Plan_matches_name_code_and_folder_and_skips_other_files()
    {
        Put("Giai tich 2 de thi.pdf");          // name without accents
        Put("MT1005_bai tap.docx");             // code
        Put("Giải tích 2 setup.exe");           // extension not allowed
        Put("Ghi chu.pdf");                     // no match
        Put(Path.Combine("Giải tích 2 tài liệu", "a.pdf"));
        Put(Path.Combine("Giải tích 2 tài liệu", "con", "b.docx"));
        var plan = Plan();
        Assert.Equal(4, plan.Count);
        Assert.All(plan, m => Assert.Equal("Giải tích 2", m.Subject));
        Assert.Contains(plan, m => m.Dest == Path.Combine(Subjects, "Giải tích 2", "Tải ngoài", "Giải tích 2 tài liệu", "con", "b.docx"));
        Assert.True(File.Exists(Path.Combine(Downloads, "Giai tich 2 de thi.pdf")));   // plan moves nothing
    }

    [Fact]
    public void Plan_is_empty_without_folder_or_subjects()
    {
        Put("Giai tich 2.pdf");
        Assert.Empty(Organizer.PlanDownloads(Path.Combine(_root, "none"), Subjects, "x", Exts, Known()));
        Assert.Empty(Organizer.PlanDownloads(Downloads, Subjects, "x", Exts, []));
    }

    [Fact]
    public void Nothing_to_move_offers_no_move_button()
    {
        var step = SettingsCatalog.OrganizeStep([], () => throw new InvalidOperationException("must not run"));
        Assert.Equal(L.T("organize.none"), step.Message);
        Assert.Null(step.Confirm);
        Assert.Null(step.Apply);
    }

    [Fact]
    public void Check_lists_name_and_subject_without_arrows_and_caps_the_list()
    {
        for (var i = 0; i < 10; i++) Put($"Giai tich 2 bai {i}.pdf");
        var step = SettingsCatalog.OrganizeStep(Plan(), () => new(0, 0, []));
        var lines = step.Message.Split(Environment.NewLine);
        Assert.Equal(9, lines.Length);
        Assert.Equal(L.F("organize.line", "Giai tich 2 bai 0.pdf", "Giải tích 2"), lines[0]);
        Assert.Equal(L.F("organize.more", 2), lines[^1]);
        Assert.Equal(L.F("organize.move", 10), step.Confirm);
        Assert.DoesNotContain("->", step.Message);
    }

    [Fact]
    public void Locked_file_fails_alone_and_others_move()
    {
        var a = Put(Path.Combine("Giải tích 2 tài liệu", "a.pdf"));
        Put(Path.Combine("Giải tích 2 tài liệu", "b.pdf"));
        Put("Giai tich 2 c.pdf");
        var plan = Plan();
        Organizer.DownloadResult r;
        using (new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.None)) r = Organizer.MoveDownloads(plan, Downloads, Move);
        Assert.Equal(2, r.Moved);
        var failed = Assert.Single(r.Failed);
        Assert.Equal(Path.Combine("Giải tích 2 tài liệu", "a.pdf"), failed.File);
        Assert.True(Directory.Exists(Path.Combine(Downloads, "Giải tích 2 tài liệu")));   // not empty, kept
        var (text, error) = SettingsCatalog.OrganizeResult(r);
        Assert.True(error);
        Assert.Contains(L.F("organize.failed", 1), text);
    }

    [Fact]
    public void Moved_folder_is_removed_and_duplicates_and_gone_files_are_counted()
    {
        Put(Path.Combine("Giải tích 2 tài liệu", "a.pdf"));
        var gone = Put("Giai tich 2 x.pdf");
        Put("MT1005 dup.pdf");
        var plan = Plan();
        File.Delete(gone);
        var r = Organizer.MoveDownloads(plan, Downloads, m =>
        {
            if (!m.Source.Contains("dup")) return Move(m);
            File.Delete(m.Source);   // stands in for the duplicates archive
            return false;
        });
        Assert.Equal(1, r.Moved);
        Assert.Equal(1, r.Duplicates);
        Assert.Single(r.Failed);
        Assert.False(Directory.Exists(Path.Combine(Downloads, "Giải tích 2 tài liệu")));
        Assert.True(Directory.Exists(Downloads));
    }

    [Theory]
    [InlineData("_Lưu trữ", false)]
    [InlineData(".git", false)]
    [InlineData("Vi", false)]
    [InlineData("Giải tích 2", true)]
    public void Helper_folders_are_not_subjects(string name, bool expected) => Assert.Equal(expected, Organizer.IsSubjectFolder(name));
}
