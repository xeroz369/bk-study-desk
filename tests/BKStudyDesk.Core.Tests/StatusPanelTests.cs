using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests;

/// <summary>Nút trạng thái trên thanh trên cùng và danh sách thông báo.</summary>
public class StatusPanelTests
{
    private static SourceState Row(string label, bool syncing = false, string? problem = null, bool login = false) => new(label, "", problem, null, syncing, login);

    [Fact]
    public void Bar_SyncingWins_ThenFailures_ThenTime()
    {
        Assert.Equal("Đang đồng bộ", StatusPanel.Bar([Row("LMS", syncing: true), Row("MyBK", problem: "x")], 0, null).Text);
        var (text, attention) = StatusPanel.Bar([Row("LMS"), Row("MyBK", problem: "Phiên hết hạn")], 0, Format.Now);
        Assert.Equal("MyBK: chưa cập nhật được", text);
        Assert.True(attention);
        Assert.Equal("MyBK: cần đăng nhập", StatusPanel.Bar([Row("LMS", problem: "Mất mạng"), Row("MyBK", problem: "Hết phiên", login: true)], 0, Format.Now).Text);
        Assert.StartsWith("Offline, đồng bộ ", StatusPanel.Bar([Row("LMS")], 0, Format.Now, offline: true).Text);
        Assert.Equal("Offline", StatusPanel.Bar([Row("LMS")], 0, null, offline: true).Text);
        Assert.StartsWith("Đồng bộ ", StatusPanel.Bar([Row("LMS"), Row("MyBK")], 0, Format.Now).Text);
        Assert.Equal("Chưa đồng bộ", StatusPanel.Bar([Row("LMS")], 0, null).Text);
    }

    [Fact]
    public void Line_Synced_Never_Off()
    {
        var t = Format.Now;
        Assert.Equal($"LMS: đồng bộ lúc {Format.Hm(t)}", StatusPanel.Line("LMS", off: false, t));
        Assert.Equal("LMS: chưa đồng bộ", StatusPanel.Line("LMS", off: false, null));
        Assert.Equal($"MyBK: đã tắt đồng bộ (Cài đặt), bản lưu lúc {Format.Hm(t)}", StatusPanel.Line("MyBK", off: true, t));
        Assert.Equal("MyBK: đã tắt đồng bộ (Cài đặt)", StatusPanel.Line("MyBK", off: true, null));
    }

    [Fact]
    public void WithNext_OneLine()
    {
        var t = Format.Now;
        Assert.Equal($"LMS: đồng bộ lúc 01:08, lần tới khoảng {Format.Hm(t)}", StatusPanel.WithNext("LMS: đồng bộ lúc 01:08", t));
    }

    [Fact]
    public void Bar_MybkOff_IsNotAProblem()
    {
        var off = new SourceState("MyBK", "MyBK: đã tắt đồng bộ (Cài đặt)", null, null, false, false, "mybk", Off: true);
        var (text, attention) = StatusPanel.Bar([Row("LMS"), off], 0, Format.Now);
        Assert.StartsWith("Đồng bộ ", text);
        Assert.False(attention);
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
        Assert.Equal(20, log.Unread);   // chỉ đếm thông báo còn trong danh sách
        log.MarkRead();
        Assert.Equal(0, log.Unread);
    }
}
