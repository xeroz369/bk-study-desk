using SoHocTap.Library;
using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

/// <summary>Tab Thư viện (LibraryPresenter): tên hiện, sách, bộ lọc.</summary>
public class LibraryTabTests
{
    private static LibraryItem Item(string id, string type = "exam", string? title = null, bool example = false, string? term = null, string? teacher = null, BookRef? book = null) =>
        new(id, type, title, null, null, term, teacher, null, null, null, null, null, null, null, null, null, example, false, null, null, null, book);

    private static readonly CourseRef Course = new("mt1005", "MT1005", "Giải tích 2", null, 4, null, null, null, null, null, 3, null, null);

    [Fact]
    public void Shown_ExampleGetsLabel_UnlessTitleSaysSo()
    {
        Assert.Equal("Đề GK (mẫu)", LibraryPresenter.Shown(Item("a", title: "Đề GK", example: true)));
        Assert.Equal("Đề ví dụ", LibraryPresenter.Shown(Item("b", title: "Đề ví dụ", example: true)));
        Assert.Equal("c", LibraryPresenter.Shown(Item("c")));
    }

    [Fact]
    public void BookTitle_AuthorsYearPublisher()
    {
        var book = Item("s", LibraryTypes.BookRef, "Giải tích", book: new BookRef(null, ["Nguyễn A", "Trần B"], 2020, "NXB ĐHQG", null));
        Assert.Equal("Giải tích (Nguyễn A, Trần B, 2020, NXB ĐHQG)", LibraryPresenter.Shown(book));
    }

    [Fact]
    public void Options_AllFirst_NewestTermFirst()
    {
        var o = LibraryPresenter.Options(["HK231", "", "HK241", "HK231"], "library.filter.allTerms", newestFirst: true);
        Assert.Null(o[0].Value);
        Assert.Equal(["HK241", "HK231"], o.Skip(1).Select(x => x.Value));
    }

    [Fact]
    public void Filter_ByTeacherAndTerm()
    {
        var rows = LibraryPresenter.Sort([
            LibraryPresenter.Row(Course, Item("1", term: "HK241", teacher: "Cô A"), false, ItemLocal.None),
            LibraryPresenter.Row(Course, Item("2", term: "HK231", teacher: "Cô A"), false, ItemLocal.None),
            LibraryPresenter.Row(Course, Item("3", term: "HK241", teacher: "Thầy B"), false, ItemLocal.Downloaded),
        ]);
        Assert.Equal(["1"], LibraryPresenter.Filter(rows, "Cô A", "HK241", null).Select(r => r.Item.Id));
        Assert.Equal(3, LibraryPresenter.Filter(rows, null, null, null).Count);
        Assert.Equal("1", rows[0].Item.Id);   // học kỳ mới trước
    }
}
