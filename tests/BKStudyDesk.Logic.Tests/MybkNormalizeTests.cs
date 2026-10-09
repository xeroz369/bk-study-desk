using System.Text.Json.Nodes;
using SoHocTap.Sources.Mybk;

namespace SoHocTap.Tests;

/// <summary>Normalizer MyBK chạy trên fixture viết tay (Fixtures/mybk), không gọi MyBK thật.</summary>
public class MybkNormalizeTests
{
    private static JsonObject Fixture(string name) =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mybk", name)))!;

    private static readonly JsonObject Study = Fixture("study.json");
    private static readonly JsonObject Extra = Fixture("extra.json");
    private static readonly Dictionary<string, string> Special = new() { ["13"] = "MT" };

    [Fact]
    public void Schedule_OrdersByDay_ParsesWeeks_ToleratesNullsAndJunk()
    {
        var s = MybkNormalize.Schedule(Study["schedule"] as JsonArray);
        Assert.Equal(2, s.Count);                                   // dòng không phải object bị bỏ
        Assert.Equal("MT0002", s[0]!["code"]!.ToString());          // thứ 2 trước thứ "4"
        Assert.Equal("", s[0]!["teacher"]!.ToString());
        Assert.Null(s[0]!["room"]);
        Assert.Empty(s[0]!["weeks"]!.AsArray());
        Assert.Equal([40, 41, 43], s[1]!["weeks"]!.AsArray().Select(w => w!.GetValue<int>()));
        Assert.Equal("Giảng Viên A", s[1]!["teacher"]!.ToString());
        Assert.Equal("CS2", s[1]!["campus"]!.ToString());
    }

    [Fact]
    public void Exams_ReadsDataWrapper_SortsByDate_MinutesLenient()
    {
        var e = MybkNormalize.Exams(Study["exams"]);
        Assert.Equal(2, e.Count);
        Assert.Equal("2026-10-15", e[0]!["date"]!.ToString());
        Assert.Null(e[0]!["minutes"]);                               // "không rõ"
        Assert.Equal(90, e[1]!["minutes"]!.GetValue<int>());         // "90"
        Assert.Equal("09g00", e[0]!["time"]!.ToString());
        Assert.Empty(MybkNormalize.Exams(null));
        Assert.Single(MybkNormalize.Exams(new JsonArray(new JsonObject { ["MAMONHOC"] = "X" })));
    }

    [Fact]
    public void Grades_SpecialCodes_ResultAndNote()
    {
        var g = MybkNormalize.Grades(Study["gradesCourses"] as JsonArray, Special);
        Assert.Equal(3, g.Count);
        Assert.Equal("20252", g[0]!["term"]!.ToString());            // mới nhất lên đầu
        var normal = g.Single(x => x!["code"]!.ToString() == "MT0003")!;
        Assert.Equal(8.5, normal["score"]!.GetValue<double>());
        Assert.Null(normal["special"]);
        Assert.True(normal["passed"]!.GetValue<bool>());
        Assert.Equal("Đạt & xong", normal["note"]!.ToString());
        var exempt = g.Single(x => x!["code"]!.ToString() == "MT0004")!;
        Assert.Null(exempt["score"]);
        Assert.Equal("MT", exempt["special"]!.ToString());
        Assert.Equal(1, exempt["result"]!.GetValue<int>());          // "1" dạng chữ
        var odd = g.Single(x => x!["code"]!.ToString() == "MT0005")!;
        Assert.Equal("mã 11", odd["special"]!.ToString());           // mã chưa có trong bảng: không đoán
        Assert.Equal(-1, odd["result"]!.GetValue<int>());
        Assert.Equal("5", odd["note"]!.ToString());                  // ghi chú dạng số không làm throw
    }

    [Fact]
    public void GradeTerms_NewestFirst()
    {
        var t = MybkNormalize.GradeTerms(Study["gradesTerms"] as JsonArray);
        Assert.Equal(["20252", "20251"], t.Select(x => x!["code"]!.ToString()));
        Assert.Equal("74", t[0]!["creditsAll"]!.ToString());
    }

    [Fact]
    public void Curriculum_SplitsBlocksAndCourses()
    {
        var c = MybkNormalize.Curriculum(Study["curriculumInfo"] as JsonObject, Study["curriculum"] as JsonArray, Special);
        Assert.Equal(50, c["creditsDone"]!.GetValue<int>());
        Assert.Equal(132, c["creditsNeed"]!.GetValue<int>());
        Assert.Equal(7.3, c["gpa10"]!.GetValue<double>());
        Assert.Null(c["gpa4"]);
        var blocks = c["blocks"]!.AsArray();
        Assert.Equal(2, blocks.Count);
        Assert.Equal("Khối cơ sở", blocks[0]!["name"]!.ToString());
        Assert.True(blocks[0]!["required"]!.GetValue<bool>());
        Assert.True(blocks[1]!["complete"]!.GetValue<bool>());
        var courses = c["courses"]!.AsArray();
        Assert.Equal(["MT0006", "MT0003", "MT0004"], courses.Select(x => x!["code"]!.ToString()));   // theo stt ("1" chữ vẫn đúng)
        Assert.Null(courses[0]!["letter"]);                          // "--"
        Assert.False(courses[0]!["attempted"]!.GetValue<bool>());
        Assert.Null(courses[2]!["score"]);
        Assert.Equal("MT", courses[2]!["special"]!.ToString());
        Assert.True(courses[2]!["provisional"]!.GetValue<bool>());
    }

    [Fact]
    public void Components_DropsTotals_ToleratesBadJson()
    {
        var c = MybkNormalize.Components(Extra["components"] as JsonArray, Special);
        Assert.Equal(2, c.Count);
        var items = c[0]!["items"]!.AsArray();
        Assert.Equal(["bt", "gk"], items.Select(i => i!["code"]!.ToString()));
        Assert.Equal("mã 12", items[1]!["special"]!.ToString());
        Assert.Null(items[1]!["score"]);
        Assert.Empty(c[1]!["items"]!.AsArray());
    }

    [Fact]
    public void Decisions_KeepOnlyKnownFields()
    {
        var d = MybkNormalize.Decisions(Extra["decisions"] as JsonArray);
        var o = Assert.Single(d)!.AsObject();
        Assert.Equal(["type", "term", "reason", "status", "date"], o.Select(p => p.Key));
    }

    [Fact]
    public void SocialWork_And_Fees()
    {
        var s = MybkNormalize.SocialWork(Extra["socialWork"] as JsonObject);
        Assert.Equal(4.5, s["days"]!.GetValue<double>());
        Assert.Single(s["activities"]!.AsArray());
        Assert.Empty(MybkNormalize.SocialWork(null)["activities"]!.AsArray());

        var f = MybkNormalize.Fees(Extra["fees"] as JsonArray);
        var fee = Assert.Single(f)!;                                  // bỏ khoản đã hủy ("1" chữ) và khoản đã đóng
        Assert.Equal("Học phí mẫu", fee["content"]!.ToString());
    }

    [Fact]
    public void Registered_CurrentTermLatestRound()
    {
        var r = MybkNormalize.Registered(Extra["registered"] as JsonArray, "20261");
        var row = Assert.Single(r)!;
        Assert.Equal("HK261_KQ", row["round"]!.ToString());           // semesterId "3" > 1
        Assert.Equal("L02", row["classGroup"]!.ToString());
        Assert.Equal("Thành công", row["result"]!.ToString());
        Assert.Empty(MybkNormalize.Registered(Extra["registered"] as JsonArray, "1"));
    }

    [Fact]
    public void FractionAndNumber()
    {
        Assert.Equal((50, 132), MybkNormalize.Fraction("50/132"));
        Assert.Equal((null, null), MybkNormalize.Fraction(null));
        Assert.Equal(4.11, MybkNormalize.Number("4.11/10"));
        Assert.Null(MybkNormalize.Number("--"));
    }

    [Fact]
    public void ParseRegistration_ReadsRoundsInVietnamTime()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mybk", "registration.html"));
        var rounds = MybkNormalize.ParseRegistration(html);
        Assert.Equal(2, rounds.Count);                                // dòng tiêu đề, ngày 31/02 và dòng ghi chú bị bỏ
        Assert.Equal("HK261_D1", rounds[0]!["code"]!.ToString());
        Assert.Equal("Đăng ký đợt 1 & điều chỉnh", rounds[0]!["name"]!.ToString());
        // 08:00 giờ VN = 01:00 UTC, không phụ thuộc múi giờ máy chạy test.
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 1, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), rounds[0]!["start"]!.GetValue<long>());
        Assert.Equal(new DateTimeOffset(2026, 8, 22, 16, 59, 0, TimeSpan.Zero).ToUnixTimeSeconds(), rounds[1]!["end"]!.GetValue<long>());
    }

    [Fact]
    public void ParseRegistration_EmptyOrJunk()
    {
        Assert.Empty(MybkNormalize.ParseRegistration(null));
        Assert.Empty(MybkNormalize.ParseRegistration("<html><body>Trang chủ</body></html>"));
    }

    [Theory]
    [InlineData("", "body rỗng")]
    [InlineData("{\"code\":\"401\",\"msg\":\"Unauthorized\",\"data\":{\"hoTen\":\"x\"}}", "JSON code=401, msg=Unauthorized")]
    [InlineData("{\"data\":{\"hoTen\":\"x\"}}", "JSON không có field báo lỗi, 22 ký tự")]
    [InlineData("<html><title>Bảo trì</title><body>...</body></html>", "HTML \"Bảo trì\", 51 ký tự")]
    [InlineData("TypeError: Failed to fetch", "text 26 ký tự")]
    public void ErrorSummary_NoPersonalFields(string body, string expected) => Assert.Equal(expected, MybkNormalize.ErrorSummary(body));
}
