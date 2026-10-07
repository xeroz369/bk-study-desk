using System.Text;
using System.Text.Json.Nodes;
using SoHocTap.Api;

namespace BKStudyDesk.Core.Tests;

/// <summary>Đọc ghi ket-qua.json qua /api/state, trên thư mục tạm (không đụng data của người dùng).</summary>
public sealed class StateFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bk-state-" + Guid.NewGuid().ToString("N"));
    private string File_ => Path.Combine(_dir, "ket-qua.json");
    private string Backup => Path.Combine(_dir, "backup");

    public StateFileTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static JsonNode? Body(ApiResponse r) => JsonNode.Parse(Encoding.UTF8.GetString(r.Body));

    [Fact]
    public void Missing_file_reads_as_empty_state()
    {
        var r = ApiRouter.ReadState(File_);
        Assert.Equal(200, r.Status);
        Assert.NotNull(Body(r)!["questions"]);
    }

    [Fact]
    public void Corrupt_file_is_an_error_not_an_empty_state()
    {
        File.WriteAllText(File_, "{\"questions\": {\"a\": ");
        Assert.Equal(500, ApiRouter.ReadState(File_).Status);
    }

    [Fact]
    public void Writing_over_a_corrupt_file_keeps_a_copy()
    {
        File.WriteAllText(File_, "{ hỏng");
        var r = ApiRouter.WriteState(File_, Backup, """{"questions":{}}""", new DateTime(2026, 10, 8, 9, 30, 0));
        Assert.Equal(200, r.Status);
        var kept = Directory.GetFiles(Backup, "ket-qua-hong-*.json");
        Assert.Equal("{ hỏng", File.ReadAllText(Assert.Single(kept)));
        Assert.Equal(200, ApiRouter.ReadState(File_).Status);
    }

    [Fact]
    public void First_write_of_the_day_backs_up_once()
    {
        File.WriteAllText(File_, """{"questions":{"a":{}}}""");
        var day = new DateTime(2026, 10, 8, 9, 0, 0);
        ApiRouter.WriteState(File_, Backup, """{"questions":{"b":{}}}""", day);
        ApiRouter.WriteState(File_, Backup, """{"questions":{"c":{}}}""", day.AddHours(1));
        var b = Assert.Single(Directory.GetFiles(Backup));
        Assert.Contains("\"a\"", File.ReadAllText(b));
    }
}
