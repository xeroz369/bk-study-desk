using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

/// <summary>Ô tìm và nút lọc loại ở tab Tài liệu của thẻ môn.</summary>
public class FileFilterTests
{
    private static RecentRow R(string name, string folder = "") => new(name, folder, folder + "/" + name, 1, 0);

    [Theory]
    [InlineData("Chuong 3.pdf", FileGroup.Docs)]
    [InlineData("anh bang.JPG", FileGroup.Images)]
    [InlineData("code.zip", FileGroup.Other)]
    public void Groups_by_extension(string name, FileGroup g) => Assert.Equal(g, FileFilter.GroupOf(name));

    [Fact]
    public void Search_ignores_accents_and_looks_in_folder()
    {
        var rows = new[] { R("Bài thí nghiệm 3.pdf", "Tài liệu LMS"), R("IMG_01.jpg", "soạn bài 10"), R("Đề cương.docx") };
        Assert.Equal(["Bài thí nghiệm 3.pdf"], FileFilter.Apply(rows, "thi nghiem", FileGroup.All).Select(r => r.Name));
        Assert.Equal(["IMG_01.jpg"], FileFilter.Apply(rows, "soan bai", FileGroup.All).Select(r => r.Name));
        Assert.Equal(2, FileFilter.Apply(rows, "", FileGroup.Docs).Count);
        Assert.Empty(FileFilter.Apply(rows, "thi nghiem", FileGroup.Images));
    }

    [Fact]
    public void Folders_show_only_when_browsing_without_filter()
    {
        var rows = new[] { new FileRow("Tài liệu LMS", true, "", 0, 0, 3), new FileRow("a.pdf", false, "PDF", 1, 0, 0), new FileRow("b.png", false, "PNG", 1, 0, 0) };
        Assert.Equal(3, FileFilter.Apply(rows, "", FileGroup.All).Count);
        Assert.Equal(["a.pdf"], FileFilter.Apply(rows, "", FileGroup.Docs).Select(r => r.Name));
        Assert.Equal(["Tài liệu LMS"], FileFilter.Apply(rows, "tai lieu", FileGroup.All).Select(r => r.Name));
    }
}
