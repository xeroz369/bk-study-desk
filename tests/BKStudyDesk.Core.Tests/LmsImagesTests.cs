using SoHocTap.Presentation;
using SoHocTap.Sources.Lms;

namespace BKStudyDesk.Core.Tests;

/// <summary>Ảnh trong bài, tin nhắn LMS: tách khối ảnh (LmsReading.Blocks) và địa chỉ chuẩn để lưu trên máy (LmsImages).</summary>
public class LmsImagesTests
{
    private const string Img = "https://lms.hcmut.edu.vn/webservice/pluginfile.php/12/mod_forum/post/3/lich%20thi.png?token=abc";

    [Fact]
    public void Blocks_keep_order_of_text_and_images()
    {
        var html = $"<p>Lịch thi:</p><p><img src=\"{Img}\" alt=\"x\"></p><p>Các em xem kỹ.</p>";
        var blocks = LmsReading.Blocks(html);
        Assert.Equal(3, blocks.Count);
        Assert.Equal("Lịch thi:", blocks[0].Text);
        Assert.Equal(Img, blocks[1].Image);
        Assert.Equal("Các em xem kỹ.", blocks[2].Text);
        Assert.Equal(["Lịch thi:", "[ảnh]", "Các em xem kỹ."], LmsReading.Paragraphs(html));
    }

    [Fact]
    public void Normalize_drops_token_and_webservice_prefix()
    {
        Assert.Equal("https://lms.hcmut.edu.vn/pluginfile.php/12/mod_forum/post/3/lich%20thi.png", LmsImages.Normalize(Img));
        Assert.Equal("https://lms.hcmut.edu.vn/pluginfile.php/1/a.png?forcedownload=1", LmsImages.Normalize("https://lms.hcmut.edu.vn/pluginfile.php/1/a.png?token=t&amp;forcedownload=1"));
    }

    [Fact]
    public void Only_lms_pluginfile_images_are_saved()
    {
        var html = $"<img src=\"{Img}\"><img src='https://example.com/a.png'><img src=\"{Img}\">";
        Assert.Equal(["https://lms.hcmut.edu.vn/pluginfile.php/12/mod_forum/post/3/lich%20thi.png"], LmsImages.UrlsIn(html));
    }
}
