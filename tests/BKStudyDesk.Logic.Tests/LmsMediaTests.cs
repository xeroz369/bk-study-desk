using System.Text.Json.Nodes;
using SoHocTap.Sources.Lms;

namespace SoHocTap.Tests;

public class LmsMediaTests
{
    private static JsonObject Course(params JsonObject[] modules) => new()
    {
        ["part"] = "Kho chung",
        ["sections"] = new JsonArray(new JsonObject { ["name"] = "Chương 1", ["modules"] = new JsonArray(modules) }),
    };

    private static JsonObject Mod(string type, string name, string url, JsonArray? files = null, JsonArray? links = null, string text = "") => new()
    {
        ["type"] = type, ["name"] = name, ["url"] = url, ["files"] = files ?? [], ["links"] = links, ["text"] = text,
    };

    [Fact]
    public void Video_files_links_pages_and_tools_are_listed()
    {
        var items = LmsMedia.Items(Course(
            Mod("resource", "KTS1_C1_record", "https://lms/mod/resource/view.php?id=1", [new JsonObject { ["name"] = "c1.mp4" }]),
            Mod("resource", "Đề cương", "https://lms/mod/resource/view.php?id=2", [new JsonObject { ["name"] = "dc.pdf" }]),
            Mod("url", "Video 1: Hàm nhiều biến", "https://lms/mod/url/view.php?id=3", [new JsonObject { ["url"] = "https://youtu.be/abc" }]),
            Mod("url", "PLECS", "https://lms/mod/url/view.php?id=4", [new JsonObject { ["url"] = "https://plexim.com" }]),
            Mod("page", "Đáp án GK261", "https://lms/mod/page/view.php?id=5"),
            Mod("lti", "Quiz ôn tập", "https://lms/mod/lti/view.php?id=6"),
            Mod("label", "", "", links: ["https://www.youtube.com/embed/xyz"], text: "Bài giảng chương 2")));
        Assert.Equal([MediaKind.Video, MediaKind.Video, MediaKind.Link, MediaKind.Page, MediaKind.Tool, MediaKind.Video], items.Select(i => i.Kind));
        Assert.Equal("https://youtu.be/abc", items[1].Url);           // link ngoài thay cho trang chuyển hướng của LMS
        Assert.Equal("Bài giảng chương 2", items[5].Name);
        Assert.All(items, i => Assert.Equal("Kho chung", i.Part));
        Assert.DoesNotContain(items, i => i.Name == "Đề cương");      // PDF là tài liệu, không phải video
    }

    [Fact]
    public void Label_links_skip_lms_files_and_duplicates()
    {
        var html = "<p><a href=\"https://drive.google.com/file/d/1\">x</a><iframe src=\"https://www.youtube.com/embed/q\"></iframe>"
                   + "<img src=\"https://lms.hcmut.edu.vn/pluginfile.php/1/a.png\"><a href=\"https://drive.google.com/file/d/1\">y</a><a href=\"#top\">z</a></p>";
        Assert.Equal(["https://drive.google.com/file/d/1", "https://www.youtube.com/embed/q"], LmsMedia.LinksIn(html));
    }

    [Fact]
    public void Link_to_another_lms_course_is_a_link_even_when_named_video()
    {
        var items = LmsMedia.Items(Course(Mod("url", "Giải tích 2 (MT1005)_Video", "https://lms/mod/url/view.php?id=9",
            [new JsonObject { ["url"] = "https://lms.hcmut.edu.vn/course/view.php?id=5147" }])));
        Assert.Equal(MediaKind.Link, Assert.Single(items).Kind);
    }

    [Fact]
    public void Missing_structure_gives_empty_list() => Assert.Empty(LmsMedia.Items(null));

    [Theory]
    [InlineData("a.MP4", FileKind.Video)]
    [InlineData("a.pdf", FileKind.Pdf)]
    [InlineData("a.pptx", FileKind.Slide)]
    [InlineData("a.zip", FileKind.Other)]
    public void File_kinds(string name, FileKind kind) => Assert.Equal(kind, FileKinds.Of(name));
}
