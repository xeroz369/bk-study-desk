using SoHocTap.Data;
using SoHocTap.Presentation;
using SoHocTap.Sources.Lms;

namespace BKStudyDesk.Core.Tests;

/// <summary>Các lỗi tìm được khi rà tính năng tin nhắn, đọc offline (31/10/2026).</summary>
public class ReviewFixTests
{
    [Fact]
    public void Fresh_without_messages_keeps_no_mark_so_inbox_is_not_flooded_later()
    {
        Assert.Equal(((IReadOnlyList<(string, string)>)[], (long?)null), (MessagesPresenter.Fresh(null, null).Fresh, MessagesPresenter.Fresh(null, null).Seen));
        Assert.Equal(5, MessagesPresenter.Fresh(new LmsMessages(0, []), 5).Seen);
    }

    [Fact]
    public void Slim_keeps_attribute_like_words_in_text()
    {
        var slim = LmsHtml.Slim("<p style=\"x\">Câu 3: width = 10, color=red</p>");
        Assert.Equal(["Câu 3: width = 10, color=red"], LmsReading.Paragraphs(slim));
    }

    [Fact]
    public void Webservice_file_links_open_through_browser_session()
    {
        var r = LmsReading.Of("t", "", "<a href=\"https://lms.hcmut.edu.vn/webservice/pluginfile.php/1/mod_forum/attachment/2/de.pdf\">Đề</a>",
            [new LmsLink("a.pdf", "https://lms.hcmut.edu.vn/webservice/pluginfile.php/9/a.pdf")], null);
        Assert.Equal("https://lms.hcmut.edu.vn/pluginfile.php/1/mod_forum/attachment/2/de.pdf", r.Links[0].Url);
        Assert.Equal("https://lms.hcmut.edu.vn/pluginfile.php/9/a.pdf", r.Attachments[0].Url);
    }

    [Fact]
    public void Filter_ignores_html_tags()
    {
        var c = new LmsConversation(1, "Cô Lan", null, ["Cô Lan"], 0, 10, null, [new(1, 2, "Cô Lan", null, "<div>Chào em</div>", 10)]);
        var data = new LmsMessages(10, [c]);
        Assert.Empty(MessagesPresenter.Rows(data, "div"));
        Assert.Single(MessagesPresenter.Rows(data, "chao em"));
    }
}
