using SoHocTap.Web;

namespace BKStudyDesk.Core.Tests;

/// <summary>Nội dung mục mở cùng hệ điều hành: đường dẫn có dấu cách, dấu tiếng Việt, ký tự đặc biệt vẫn đúng.</summary>
public class AutostartTests
{
    [Fact]
    public void RunCommand_QuotesPath() =>
        Assert.Equal("\"C:\\Users\\Lê Hùng\\BK Study Desk\\BKStudyDesk.exe\" --tray", AutostartFiles.RunCommand("C:\\Users\\Lê Hùng\\BK Study Desk\\BKStudyDesk.exe"));

    [Fact]
    public void DesktopEntry_EscapesExec()
    {
        var text = AutostartFiles.DesktopEntry("/home/a b/$x/\"q\"/BKStudyDesk", "BK Study Desk");
        Assert.Contains("Exec=\"/home/a b/\\$x/\\\"q\\\"/BKStudyDesk\" --tray", text);
        Assert.Contains("Name=BK Study Desk", text);
    }

    [Fact]
    public void LaunchAgent_EscapesXml()
    {
        var text = AutostartFiles.LaunchAgent("vn.bk.studydesk", "/Applications/A&B.app/Contents/MacOS/BKStudyDesk");
        Assert.Contains("<string>/Applications/A&amp;B.app/Contents/MacOS/BKStudyDesk</string><string>--tray</string>", text);
        Assert.Contains("<key>RunAtLoad</key><true/>", text);
    }
}
