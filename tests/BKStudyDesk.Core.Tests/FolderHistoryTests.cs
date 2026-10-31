using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

/// <summary>Nút Quay lại của tab Tài liệu (FolderHistory): về thư mục vừa xem, chưa đi đâu thì lên thư mục cha, không ra ngoài môn.</summary>
public class FolderHistoryTests
{
    private const string Root = "Môn học/Kỹ thuật số";

    private static FolderHistory At(string root = Root)
    {
        var h = new FolderHistory();
        h.Reset(root);
        return h;
    }

    [Fact]
    public void Back_returns_to_the_folder_seen_before()
    {
        var h = At();
        h.Went(Root, Root + "/prelab");
        h.Went(Root + "/prelab", Root);           // bấm cấp trên cùng của thanh đường dẫn
        Assert.Equal(Root + "/prelab", h.Back(Root));
        Assert.Equal(Root, h.Back(Root + "/prelab"));   // hết lịch sử: lên thư mục cha
    }

    [Fact]
    public void Without_history_back_goes_to_parent_and_stops_at_subject_folder()
    {
        var h = At();
        Assert.False(h.CanGoBack(Root));
        Assert.Null(h.Back(Root));
        Assert.True(h.CanGoBack(Root + "/a/b"));
        Assert.Equal(Root + "/a", h.Back(Root + "/a/b"));
    }

    [Fact]
    public void Reset_clears_history_of_previous_subject()
    {
        var h = At();
        h.Went(Root, Root + "/prelab");
        h.Reset("Môn học/Giải tích 2");
        Assert.Null(h.Back("Môn học/Giải tích 2"));
    }

    [Fact]
    public void Folders_outside_subject_are_skipped()
    {
        var h = At();
        h.Went("Môn học/Giải tích 2", Root);
        Assert.Null(h.Back(Root));
    }

    [Fact]
    public void Back_is_disabled_when_history_only_has_folders_outside_subject()
    {
        var h = At();
        h.Went("Môn học/Giải tích 2", Root);
        Assert.False(h.CanGoBack(Root));
    }
}
