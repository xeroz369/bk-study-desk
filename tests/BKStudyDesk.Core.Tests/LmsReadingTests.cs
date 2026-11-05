using SoHocTap.Presentation;

namespace BKStudyDesk.Core.Tests;

/// <summary>LmsReading: HTML của bài thông báo, tin nhắn LMS thành đoạn chữ và link, đọc offline.</summary>
public class LmsReadingTests
{
    [Fact]
    public void Paragraphs_follow_blocks_and_line_breaks()
    {
        var html = "<p>Chào các em,</p><p>Lớp nghỉ <b>thứ Hai</b> 03/11.<br>Học bù thứ Bảy.</p><ul><li>Mang máy tính</li><li>Nộp bài 2</li></ul>";
        Assert.Equal(["Chào các em,", "Lớp nghỉ thứ Hai 03/11.\nHọc bù thứ Bảy.", "• Mang máy tính\n• Nộp bài 2"], LmsReading.Paragraphs(html));
    }

    [Fact]
    public void Entities_scripts_and_images_are_handled()
    {
        var html = "<div>Điểm &amp; hạn&nbsp;nộp<script>alert(1)</script><img src=\"https://lms/pluginfile.php/1/a.png\"></div>";
        Assert.Equal(["Điểm & hạn nộp [ảnh]"], LmsReading.Paragraphs(html));
    }

    [Fact]
    public void Plain_text_message_without_tags_is_kept()
    {
        Assert.Equal(["Thầy ơi em xin nộp trễ"], LmsReading.Paragraphs("Thầy ơi em xin nộp trễ"));
        Assert.Empty(LmsReading.Paragraphs("  "));
        Assert.Empty(LmsReading.Paragraphs(null));
    }

    [Fact]
    public void Links_keep_text_skip_scripts_and_duplicates()
    {
        var html = "<a href=\"https://meet.google.com/abc\">Link học online</a> <a href='https://meet.google.com/abc'>lại</a>"
                 + "<a href=\"javascript:void(0)\">x</a><a href=\"mailto:gv@hcmut.edu.vn\"></a>";
        var links = LmsReading.Links(html);
        Assert.Equal(["Link học online", "mailto:gv@hcmut.edu.vn"], links.Select(l => l.Name));
        Assert.Equal("https://meet.google.com/abc", links[0].Url);
    }

    [Fact]
    public void Links_whose_text_is_the_address_are_not_repeated()
    {
        var html = "<p>Khảo sát: <a href=\"https://forms.gle/abc\">https://forms.gle/abc</a></p>"
                 + "<a href=\"https://meet.google.com/x\">Link học online</a><a href=\"www.x.vn\">www.x.vn</a>";
        Assert.Equal(["Link học online"], LmsReading.Links(html).Select(l => l.Name));
    }

    [Fact]
    public void Links_stay_inside_the_text_as_spans()
    {
        var html = "<p>Vào <a href=\"https://meet.google.com/abc\">link học <b>online</b></a> lúc 9h.</p><p>Không link</p>";
        var blocks = LmsReading.Blocks(html);
        Assert.Equal("Vào link học online lúc 9h.", blocks[0].Text);
        Assert.Equal(["Vào ", "link học online", " lúc 9h."], blocks[0].Spans!.Select(x => x.Text));
        Assert.Equal("https://meet.google.com/abc", blocks[0].Spans![1].Url);
        Assert.Null(blocks[0].Spans![0].Url);
        Assert.Null(blocks[1].Spans);
        Assert.Equal(["Vào link học online lúc 9h.", "Không link"], LmsReading.Paragraphs(html));
    }

    [Fact]
    public void Script_links_and_empty_anchor_text()
    {
        var blocks = LmsReading.Blocks("<a href=\"javascript:x()\">bấm</a> và <a href=\"https://lms.hcmut.edu.vn/x\"></a>");
        Assert.Equal("bấm và https://lms.hcmut.edu.vn/x", blocks[0].Text);
        Assert.Single(blocks[0].Spans!, x => x.Url is not null);
    }
}
