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
}
