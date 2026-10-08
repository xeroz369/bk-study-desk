using SoHocTap.Library;
using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

/// <summary>Trang Thư viện: mọi môn của thư viện, môn của người dùng lên đầu, tìm bỏ dấu cả tên cũ, lọc khoa, thư mục lưu tài liệu.</summary>
public class LibraryPageTests
{
    private static CourseRef C(string id, string code, string name, string? faculty = null, string? status = null, int items = 1,
        List<string>? aliases = null, List<string>? oldNames = null) =>
        new(id, code, name, null, 3, faculty, aliases, oldNames, status, null, items, null, null);

    private static readonly LibraryIndex Index = new(1, null, null, null,
        [new LibraryFaculty("khmt", new LibraryName("Khoa học và Kỹ thuật Máy tính", "Computer Science")), new LibraryFaculty("ud", new LibraryName("Khoa học Ứng dụng", null)), new LibraryFaculty("empty", null)],
        [
            C("co1007", "CO1007", "Cấu trúc rời rạc", "khmt"),
            C("mt1005", "MT1005", "Giải tích 2", "ud", oldNames: ["Toán cao cấp B2"]),
            C("mt1003", "MT1003", "Giải tích 1", "ud", status: "retired", items: 0),
            C("ph1003", "PH1003", "Vật lý 1", "ud", aliases: ["Vat ly dai cuong 1"]),
        ]);

    [Fact]
    public void Courses_MineFirst_ThenOpen_ThenRetired()
    {
        var rows = LibraryPagePresenter.Courses(Index, [("Giải tích 2", [new CourseMatch(Index.Courses![1], null)])]);
        Assert.Equal(["mt1005", "co1007", "ph1003", "mt1003"], rows.Select(r => r.Course.Id));
        Assert.True(rows[0].Mine);
        Assert.Equal("Giải tích 2", rows[0].Folder);
        Assert.False(rows[1].Mine);
        Assert.Equal("Cấu trúc rời rạc", rows[1].Folder);
    }

    [Fact]
    public void Filter_FoldedOverCodeNameAliasesOldNames()
    {
        var rows = LibraryPagePresenter.Courses(Index, []);
        Assert.Equal(["mt1005"], LibraryPagePresenter.Filter(rows, "toan cao cap", null).Select(r => r.Course.Id));
        Assert.Equal(["ph1003"], LibraryPagePresenter.Filter(rows, "dai cuong", null).Select(r => r.Course.Id));
        Assert.Equal(["co1007"], LibraryPagePresenter.Filter(rows, "co10", null).Select(r => r.Course.Id));
        Assert.Equal(["co1007"], LibraryPagePresenter.Filter(rows, "", "khmt").Select(r => r.Course.Id));
        Assert.Equal(4, LibraryPagePresenter.Filter(rows, "  ", null).Count);
    }

    [Fact]
    public void Faculties_AllFirst_OnlyUsed()
    {
        var f = LibraryPagePresenter.Faculties(Index);
        Assert.Null(f[0].Value);
        Assert.Equal(["ud", "khmt"], f.Skip(1).Select(o => o.Value));   // theo tên tiếng Việt: "Khoa học Ứng dụng" trước "Khoa học và..."
    }

    [Theory]
    [InlineData("Lập trình C/C++", "Lập trình C C++")]
    [InlineData("Đồ án: Hệ thống?", "Đồ án Hệ thống")]
    [InlineData("  ...  ", "_")]
    public void FolderName_DropsReservedCharacters(string name, string expected) => Assert.Equal(expected, LibraryPagePresenter.FolderName(name));

    [Fact]
    public void Courses_NoIndex_Empty() => Assert.Empty(LibraryPagePresenter.Courses(null, []));
}
