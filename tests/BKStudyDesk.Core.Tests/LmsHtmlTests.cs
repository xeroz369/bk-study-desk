using SoHocTap.Presentation;
using SoHocTap.Sources.Lms;

namespace BKStudyDesk.Core.Tests;

/// <summary>LmsHtml.Slim: bỏ style, span, ghi chú Office của bài dán từ Word mà vẫn giữ chữ, đoạn và link.</summary>
public class LmsHtmlTests
{
    [Fact]
    public void Word_markup_is_dropped_but_text_and_links_stay()
    {
        var html = "<!--[if gte mso 9]><xml><o:OfficeDocumentSettings/></xml><![endif]-->"
                 + "<p class=\"MsoNormal\" align=\"center\" style=\"text-align:center;background:#FFFFFF;\"><strong><span style=\"font-size:14pt;font-family:'Times New Roman'\">THÔNG BÁO</span></strong><o:p></o:p></p>"
                 + "<p style=\"margin:0\"><span lang=\"VI\">Xem <a href=\"https://meet.google.com/abc\" style=\"color:blue\">link</a></span></p>";
        var slim = LmsHtml.Slim(html);
        Assert.DoesNotContain("style", slim);
        Assert.DoesNotContain("span", slim);
        Assert.DoesNotContain("mso", slim);
        Assert.Equal(["THÔNG BÁO", "Xem link"], LmsReading.Paragraphs(slim));
        Assert.Equal("https://meet.google.com/abc", LmsReading.Links(slim).Single().Url);
    }

    [Fact]
    public void Empty_input_gives_empty_text() => Assert.Equal("", LmsHtml.Slim(null));
}
