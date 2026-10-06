using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using SoHocTap.Api;
using SoHocTap.Web;

namespace BKStudyDesk.Core.Tests;

/// <summary>Máy chủ cục bộ của khung Luyện tập: chỉ 127.0.0.1, dưới khóa bí mật, /api cần X-App, không thoát khỏi thư mục cho phép.</summary>
public class LocalServerTests
{
    private sealed class NoShell : IShellActions
    {
        public bool OpenWeb(string url, string title) => false;
        public bool SyncLms() => false;
    }

    [Fact]
    public void StaticRoutes_BlocksTraversal()
    {
        var root = Directory.CreateTempSubdirectory("bk-ui-");
        try
        {
            var ui = Directory.CreateDirectory(Path.Combine(root.FullName, "ui")).FullName;
            File.WriteAllText(Path.Combine(ui, "index.html"), "<p>ok</p>");
            File.WriteAllText(Path.Combine(root.FullName, "secret.txt"), "no");
            var content = Path.Combine(root.FullName, "content");
            var packs = Path.Combine(root.FullName, "packs");
            Assert.Equal("text/html; charset=utf-8", StaticRoutes.Resolve("/", ui, content, packs)!.Type);
            Assert.Null(StaticRoutes.Resolve("/../secret.txt", ui, content, packs));
            Assert.Null(StaticRoutes.Resolve("/content/../../secret.txt", ui, content, packs));
            Assert.Null(StaticRoutes.Resolve("/missing.js", ui, content, packs));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Server_ApiNeedsAppHeader_WrongKey404_MessageRaised()
    {
        using var server = new LocalServer(new ApiRouter(new NoShell()));
        var root = server.Start();
        Assert.StartsWith("http://127.0.0.1:", root);
        JsonObject? got = null;
        server.Message += m => got = m;
        using var http = new HttpClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync(root + "api/state")).StatusCode);
        var wrong = root.Replace(server.Key, new string('0', 32));
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(wrong + "index.html")).StatusCode);

        using var msg = new HttpRequestMessage(HttpMethod.Post, root + "api/app/message") { Content = new StringContent("{\"type\":\"key\",\"key\":\"Ctrl+2\"}", Encoding.UTF8, "application/json") };
        msg.Headers.Add("X-App", "1");
        Assert.Equal(HttpStatusCode.NoContent, (await http.SendAsync(msg)).StatusCode);
        Assert.Equal("Ctrl+2", got?["key"]?.GetValue<string>());
    }
}
