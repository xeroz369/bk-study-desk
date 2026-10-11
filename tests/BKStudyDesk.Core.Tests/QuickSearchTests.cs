using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

public class QuickSearchTests
{
    private static readonly QuickItem[] All =
    [
        new("Hôm nay", "Trang", "page:0"),
        new("Lịch", "Trang", "page:2"),
        new("Giải tích 2", "Môn học", "subject:Giải tích 2"),
        new("Phương pháp tính", "Môn học", "subject:Phương pháp tính"),
        new("Tích phân kép", "Bài, Giải tích 2", "lesson:gt2-07"),
        new("Đạo hàm theo hướng", "Bài, Giải tích 2", "lesson:gt2-03"),
    ];

    [Fact]
    public void Empty_query_lists_the_first_items()
    {
        Assert.Equal(["page:0", "page:2"], QuickSearch.Match(All, " ", limit: 2).Select(i => i.Target));
    }

    [Fact]
    public void Prefix_beats_word_start_beats_contains_and_folds_diacritics()
    {
        // "tich": "Tích phân kép" (đầu tên), "Giải tích 2" (đầu một chữ), bài có dòng phụ "Giải tích 2"; "Phương pháp tính" không khớp.
        Assert.Equal(["lesson:gt2-07", "subject:Giải tích 2", "lesson:gt2-03"], QuickSearch.Match(All, "tich").Select(i => i.Target));
        Assert.Equal(["page:2"], QuickSearch.Match(All, "LICH").Select(i => i.Target));
    }

    [Fact]
    public void Sub_text_matches_last()
    {
        // "giai tich" khớp tên môn trước, rồi tới hai bài có dòng phụ "Giải tích 2".
        Assert.Equal(["subject:Giải tích 2", "lesson:gt2-07", "lesson:gt2-03"], QuickSearch.Match(All, "giai tich").Select(i => i.Target));
    }

    [Fact]
    public void Limit_caps_the_results()
    {
        Assert.Single(QuickSearch.Match(All, "giai tich", limit: 1));
    }
}
