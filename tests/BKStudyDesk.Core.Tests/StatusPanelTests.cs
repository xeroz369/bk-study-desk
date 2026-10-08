using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests;

/// <summary>Nút trạng thái trên thanh trên cùng và danh sách thông báo.</summary>
public class StatusPanelTests
{
    private static SourceState Row(string label, bool syncing = false, string? problem = null) => new(label, "", problem, null, syncing, false);

    [Fact]
    public void Bar_SyncingWins_ThenFailures_ThenTime()
    {
        Assert.Equal("Đang đồng bộ", StatusPanel.Bar([Row("LMS", syncing: true), Row("MyBK", problem: "x")], 0, null).Text);
        var (text, attention) = StatusPanel.Bar([Row("LMS"), Row("MyBK", problem: "Phiên hết hạn")], 0, Format.Now);
        Assert.Equal("Lỗi MyBK", text);
        Assert.True(attention);
        Assert.StartsWith("Đồng bộ ", StatusPanel.Bar([Row("LMS"), Row("MyBK")], 0, Format.Now).Text);
        Assert.Equal("Chưa đồng bộ", StatusPanel.Bar([Row("LMS")], 0, null).Text);
    }

    [Fact]
    public void Bar_UnreadCount()
    {
        var (text, attention) = StatusPanel.Bar([Row("LMS")], 2, null);
        Assert.Equal("Chưa đồng bộ (2)", text);
        Assert.True(attention);
    }

    [Fact]
    public void NoticeLog_NewestFirst_KeepsTwenty_MarkRead()
    {
        var log = new NoticeLog();
        for (var i = 0; i < 25; i++) log.Add(new Notice(i, "t" + i, "", ""));
        Assert.Equal(20, log.Items.Count);
        Assert.Equal("t24", log.Items[0].Title);
        Assert.Equal(25, log.Unread);
        log.MarkRead();
        Assert.Equal(0, log.Unread);
    }
}
