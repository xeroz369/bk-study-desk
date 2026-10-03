using System.Net;
using System.Security.Cryptography;
using System.Text;
using SoHocTap.Api;
using SoHocTap.Library;

namespace SoHocTap.Tests;

/// <summary>
/// Thư viện /v1: đọc hợp đồng, ETag, cache khi mất mạng, ghép môn, tải file có kiểm sha256, mục đã gỡ, cài gói quiz.
/// Fixture viết tay ở Fixtures/library (không lấy từ repo thư viện thật); "mạng" là HttpMessageHandler giả, không gọi ra ngoài.
/// </summary>
public sealed class LibraryTests : IDisposable
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "library");
    private const string Base = "https://library.example/";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bksd-library-" + Guid.NewGuid().ToString("N"));
    private readonly FakeServer _server = new(Fixtures);
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero));

    public LibraryTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private string Target => Path.Combine(_dir, "Môn học", "Giải tích 2", "Thư viện");

    private LibraryClient Client(string baseUrl = Base) =>
        new(new HttpClient(_server), new LibraryOptions(baseUrl, Path.Combine(_dir, "library-cache"), Path.Combine(_dir, "library-downloads.json"))
        {
            Gap = TimeSpan.Zero,
        }, _time);

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Fixtures, .. parts]));

    private static LibraryIndex Index() => LibraryJson.ParseIndex(Read("v1", "index.json"));

    private static CourseDetail Course(string id) => LibraryJson.ParseCourse(Read("v1", "courses", id + ".json"));

    private static CourseRef Ref(string id) => Index().Courses!.Single(c => c.Id == id);

    // ------------------------------------------------------------------ hợp đồng

    [Fact]
    public void ParsesIndex_IgnoresUnknownFields()
    {
        var x = Index();
        Assert.Equal(1, x.SchemaVersion);
        Assert.Equal("https://library.example/", x.Site);
        Assert.Equal(7, x.Courses!.Count);
        Assert.Equal(3, x.Faculties!.Count);
        var mt = x.Courses.Single(c => c.Id == "MT1005");
        Assert.Equal(["MT1004"], mt.Aliases!);
        Assert.Equal("courses/MT1005.json", mt.Detail);
        Assert.Equal("2024", x.Courses.Single(c => c.Id == "GE4169-2024").Edition);
        Assert.Null(mt.Edition);
        Assert.True(x.Courses.Single(c => c.Id == "GE1007").Retired);
    }

    [Fact]
    public void ParsesCourse_RemovedItem_UnknownType_UpdatedDefault()
    {
        var mt = Course("MT1005");
        Assert.Equal(6, mt.Items!.Count);
        var removed = mt.Items.Single(i => i.Id == "ban-cu-da-go");
        Assert.True(removed.Removed);
        Assert.Null(removed.Title);
        Assert.Empty(removed.Files!);
        Assert.Equal("2026-09-20", removed.Updated);   // updated vắng thì lấy added
        var link = mt.Items.Single(i => i.Id == "mit-ocw-1802");
        Assert.True(link.IsLink);
        Assert.Empty(link.Files!);
        Assert.Equal("2026-10-02", mt.Items.Single(i => i.Id == "bang-cong-thuc-gk").Updated);
        var video = Course("EE1009").Items!.Single(i => i.Id == "danh-sach-video");
        Assert.Equal(LibraryTypes.Other, LibraryTypes.GroupOf(video.Type));
        Assert.Equal(LibraryTypes.Known.Count, LibraryTypes.Order(video.Type));
        Assert.Equal(0, LibraryTypes.Order("summary"));
    }

    [Fact]
    public void RejectsWrongSchemaAndBadIds()
    {
        Assert.Throws<SoHocTap.Data.DataReadException>(() => LibraryJson.ParseIndex("""{"schemaVersion":2,"courses":[]}"""));
        Assert.Throws<SoHocTap.Data.DataReadException>(() => LibraryJson.ParseCourse("""{"schemaVersion":1,"id":"../x","items":[]}"""));
        var x = LibraryJson.ParseIndex("""{"schemaVersion":1,"courses":[{"id":"mt1005"},{"id":"MT1005","code":"MT1005","name":"A"},null]}""");
        Assert.Equal("MT1005", Assert.Single(x.Courses!).Id);
        var d = LibraryJson.ParseCourse("""
            {"schemaVersion":1,"id":"MT1005","items":[{"id":"Bad Id","type":"notes"},{"id":"ok","type":"notes",
             "files":[{"name":"a.md","size":1,"sha256":"xyz","urls":["https://a/"]},{"name":"b.md","size":1,"sha256":"%%","urls":[]}]}]}
            """.Replace("%%", new string('a', 64), StringComparison.Ordinal));
        var item = Assert.Single(d.Items!);
        Assert.Empty(item.Files!);   // sha sai mẫu, không có url: không tải được nên bỏ
    }

    [Theory]
    [InlineData("https://library.example/", "https://library.example/v1/")]
    [InlineData("https://library.example/v1", "https://library.example/v1/")]
    [InlineData("https://library.example/sub/v1/", "https://library.example/sub/v1/")]
    [InlineData(@"C:\lib\v1\", "file:///C:/lib/v1/")]
    [InlineData("file:///C:/lib/", "file:///C:/lib/v1/")]
    [InlineData("", null)]
    [InlineData("ftp://library.example/", null)]
    [InlineData("library.example", null)]
    public void RootOf(string input, string? expected) => Assert.Equal(expected, LibraryClient.RootOf(input)?.AbsoluteUri);

    [Fact]
    public void ItemAndContributeUrls()
    {
        var index = Index();
        var mt = Ref("MT1005");
        var items = Course("MT1005").Items!;
        Assert.Equal("https://ocw.mit.edu/courses/18-02sc-multivariable-calculus-fall-2010/",
            LibraryClient.ItemUrl(index, mt, items.Single(i => i.Id == "mit-ocw-1802"))!.AbsoluteUri);
        Assert.Equal("https://library.example/course/MT1005/#tom-tat-chuong-1", LibraryClient.ItemUrl(index, mt, items.Single(i => i.Id == "tom-tat-chuong-1"))!.AbsoluteUri);
        Assert.Equal("https://library.example/contribute/", LibraryClient.ContributeUrl(index)!.AbsoluteUri);
        var js = items.Single(i => i.Id == "mit-ocw-1802") with { Url = "javascript:alert(1)" };
        Assert.Null(LibraryClient.ItemUrl(index, mt, js));
    }

    [Theory]
    [InlineData("tom-tat.md", "tom-tat.md")]
    [InlineData("../../evil.md", "evil.md")]
    [InlineData(@"..\a\b:c.pdf", "b_c.pdf")]
    [InlineData(".hidden", "hidden")]
    [InlineData("...", null)]
    [InlineData("  ", null)]
    public void SafeFileName(string input, string? expected) => Assert.Equal(expected, LibraryClient.SafeFileName(input));

    // ------------------------------------------------------------------ index, ETag, cache

    [Fact]
    public async Task Index_SecondFetchSendsETag_And304KeepsCache()
    {
        using var c = Client();
        var first = await c.GetIndexAsync(LibraryRefresh.Force, default);
        Assert.Equal(LibraryOrigin.Network, first.Origin);
        Assert.Null(_server.Requests.Single().IfNoneMatch);
        Assert.NotNull(c.LastChecked);

        _time.Advance(TimeSpan.FromHours(1));
        var second = await c.GetIndexAsync(LibraryRefresh.Force, default);
        Assert.Equal(LibraryOrigin.NotModified, second.Origin);
        Assert.Equal(_server.ETagOf("/v1/index.json"), _server.Requests[^1].IfNoneMatch);
        Assert.Equal(7, second.Value!.Courses!.Count);
        Assert.Equal(_time.GetUtcNow().ToUnixTimeSeconds(), c.LastChecked!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Index_AtMostDaily_TabOpenThrottled()
    {
        using var c = Client();
        await c.GetIndexAsync(LibraryRefresh.IfDue, default);
        Assert.Single(_server.Requests);
        _time.Advance(TimeSpan.FromHours(23));
        var cached = await c.GetIndexAsync(LibraryRefresh.IfDue, default);
        Assert.Equal(LibraryOrigin.Cache, cached.Origin);
        Assert.Single(_server.Requests);
        _time.Advance(TimeSpan.FromHours(2));
        await c.GetIndexAsync(LibraryRefresh.IfDue, default);
        Assert.Equal(2, _server.Requests.Count);

        // Mở tab: gọi lại, nhưng mở tab liên tục trong 10 phút thì không.
        _time.Advance(TimeSpan.FromMinutes(5));
        await c.GetIndexAsync(LibraryRefresh.Tab, default);
        Assert.Equal(2, _server.Requests.Count);
        _time.Advance(TimeSpan.FromMinutes(6));
        await c.GetIndexAsync(LibraryRefresh.Tab, default);
        Assert.Equal(3, _server.Requests.Count);
        await c.GetIndexAsync(LibraryRefresh.CacheOnly, default);
        Assert.Equal(3, _server.Requests.Count);
    }

    [Fact]
    public async Task Offline_UsesCache()
    {
        using (var c = Client()) Assert.Equal(LibraryOrigin.Network, (await c.GetIndexAsync(LibraryRefresh.Force, default)).Origin);
        _server.Offline = true;
        using var again = Client();   // app mở lại khi mất mạng
        var r = await again.GetIndexAsync(LibraryRefresh.Force, default);
        Assert.Equal(LibraryOrigin.Cache, r.Origin);
        Assert.NotNull(r.Error);
        Assert.Equal(7, r.Value!.Courses!.Count);

        var detail = await again.GetCourseAsync(Ref("MT1005"), LibraryRefresh.Force, default);
        Assert.Equal(LibraryOrigin.None, detail.Origin);   // chưa từng tải chi tiết: không có gì, nhưng không throw
        Assert.Null(detail.Value);
    }

    [Fact]
    public async Task ServerError_And_BrokenJson_KeepOldCache()
    {
        using var c = Client();
        await c.GetCourseAsync(Ref("EE1009"), LibraryRefresh.Force, default);
        _server.Override = path => path == "/v1/courses/EE1009.json" ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{ hỏng") } : null;
        var broken = await c.GetCourseAsync(Ref("EE1009"), LibraryRefresh.Force, default);
        Assert.Equal(LibraryOrigin.Cache, broken.Origin);
        Assert.Equal(3, broken.Value!.Items!.Count);
        _server.Override = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var down = await c.GetCourseAsync(Ref("EE1009"), LibraryRefresh.Force, default);
        Assert.Equal(LibraryOrigin.Cache, down.Origin);
        Assert.Equal("HTTP 503", down.Error);
    }

    [Fact]
    public async Task CourseDetail_PathOutsideV1_Refused()
    {
        using var c = Client();
        var r = await c.GetCourseAsync(Ref("MT1005") with { Detail = "../secrets.json" }, LibraryRefresh.Force, default);
        Assert.Null(r.Value);
        Assert.Empty(_server.Requests);
        var abs = await c.GetCourseAsync(Ref("MT1005") with { Detail = "https://evil.example/x.json" }, LibraryRefresh.Force, default);
        Assert.Null(abs.Value);
        Assert.Empty(_server.Requests);
        var ok = await c.GetCourseAsync(Ref("MT1005") with { Detail = null }, LibraryRefresh.Force, default);   // mặc định courses/<id>.json
        Assert.Equal(6, ok.Value!.Items!.Count);
    }

    [Fact]
    public async Task ClearCache_KeepsDownloadManifest()
    {
        using var c = Client();
        await c.GetIndexAsync(LibraryRefresh.Force, default);
        var mt = Ref("MT1005");
        await c.DownloadAsync(mt, Item("MT1005", "tom-tat-chuong-1"), Target, default);
        c.ClearCache();
        Assert.Null(c.LastChecked);
        Assert.Equal(LibraryOrigin.None, (await c.GetIndexAsync(LibraryRefresh.CacheOnly, default)).Origin);
        Assert.Single(c.Records());
    }

    // ------------------------------------------------------------------ ghép môn

    [Fact]
    public void Match_ByCode_Alias_NfcCaseInsensitive()
    {
        var lib = Index().Courses!;
        Assert.Equal("MT1005", Assert.Single(LibraryMatch.FindAll([LibraryMatch.LmsCode("MT1005_HK251_CC01")], lib)).Course.Id);
        Assert.Equal("MT1005", Assert.Single(LibraryMatch.FindAll([" mt1004 "], lib)).Course.Id);   // mã cũ (alias)
        Assert.Equal("EE1009", Assert.Single(LibraryMatch.FindAll(["ee1009".Normalize(NormalizationForm.FormD)], lib)).Course.Id);
        Assert.Empty(LibraryMatch.FindAll(["CO1005", "", null], lib));
        Assert.Empty(LibraryMatch.FindAll(["Giải tích 2"], lib));   // không khớp theo tên
    }

    [Fact]
    public void Match_SharedCode_ReturnsBoth()
    {
        var found = LibraryMatch.FindAll(["GE4169"], Index().Courses!);
        Assert.Equal(["GE4169", "GE4169-2024"], found.Select(m => m.Course.Id));
        Assert.All(found, m => Assert.Null(m.ReplacementOf));
    }

    [Fact]
    public void Match_Retired_FollowsReplacedBy()
    {
        var found = LibraryMatch.FindAll(["GE1007"], Index().Courses!);
        Assert.Equal(["GE1007", "GE2033"], found.Select(m => m.Course.Id));
        Assert.Equal("GE1007", found[1].ReplacementOf!.Id);
        // Người dùng có cả mã mới: môn mới là khớp thẳng, không ghi "thay cho".
        var both = LibraryMatch.FindAll(["GE2033", "GE1007"], Index().Courses!);
        Assert.Equal("GE2033", both[0].Course.Id);
        Assert.All(both, m => Assert.Null(m.ReplacementOf));
    }

    [Fact]
    public void Match_ReplacedByCycle_Stops()
    {
        var lib = new List<CourseRef>
        {
            Ref("GE1007") with { Id = "AA1000", Code = "AA1000", ReplacedBy = "BB1000" },
            Ref("GE1007") with { Id = "BB1000", Code = "BB1000", ReplacedBy = "AA1000" },
        };
        Assert.Equal(["AA1000", "BB1000"], LibraryMatch.FindAll(["AA1000"], lib).Select(m => m.Course.Id));
    }

    [Fact]
    public void MatchAll_LmsAndMybkCodes()
    {
        var map = LibraryMatch.MatchAll(
        [
            ("Giải tích 2", [LibraryMatch.LmsCode("MT1005_HK251")]),
            ("Kỹ thuật số", ["EE1009"]),          // mã từ thời khóa biểu MyBK
            ("Môn lạ", ["XX9999"]),
        ], Index().Courses!);
        Assert.Equal(2, map.Count);
        Assert.Equal("EE1009", map["Kỹ thuật số"][0].Course.Id);
        Assert.True(map.ContainsKey("giải tích 2".Normalize(NormalizationForm.FormD)));
    }

    // ------------------------------------------------------------------ tải file

    private static LibraryItem Item(string course, string id) => Course(course).Items!.Single(i => i.Id == id);

    [Fact]
    public async Task Download_VerifiesSha256_AndSkipsWhenPresent()
    {
        using var c = Client();
        var item = Item("MT1005", "tom-tat-chuong-1");
        var r = await c.DownloadAsync(Ref("MT1005"), item, Target, default);
        Assert.True(r.Ok, r.Detail);
        var path = Assert.Single(r.Paths);
        Assert.Equal(Path.Combine(Target, "tom-tat-c1.md"), path);
        Assert.Equal(item.Files![0].Sha256, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
        Assert.Empty(Directory.GetFiles(Target, "*.part"));
        Assert.Equal(ItemLocal.Downloaded, c.LocalState("MT1005", item));
        Assert.Equal([path], c.DownloadedFiles("MT1005", item.Id));

        var count = _server.Requests.Count;
        var again = await c.DownloadAsync(Ref("MT1005"), item, Target, default);
        Assert.True(again.Ok);
        Assert.Equal(count, _server.Requests.Count);   // đúng nội dung rồi thì không tải lại
    }

    [Fact]
    public async Task Download_FirstUrlMismatch_TriesNext()
    {
        using var c = Client();
        var r = await c.DownloadAsync(Ref("MT1005"), Item("MT1005", "meo-on-gk"), Target, default);
        Assert.True(r.Ok, r.Detail);
        Assert.Equal(["/files/MT1005/meo-on-gk-hong.md", "/MT1005/meo-on-gk.md"], _server.Requests.Select(x => x.Path));
        Assert.StartsWith("# Mẹo ôn giữa kỳ", File.ReadAllText(Assert.Single(r.Paths)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Download_AllMismatch_LeavesNothing()
    {
        using var c = Client();
        var item = Item("MT1005", "tom-tat-chuong-1");
        var bad = item with { Files = [item.Files![0] with { Sha256 = new string('0', 64) }] };
        var r = await c.DownloadAsync(Ref("MT1005"), bad, Target, default);
        Assert.Equal(DownloadError.Mismatch, r.Error);
        Assert.Empty(r.Paths);
        Assert.Empty(Directory.GetFiles(Target));
        Assert.Empty(c.Records());
        Assert.Equal(2, _server.Requests.Count);   // đã thử cả hai url
    }

    [Fact]
    public async Task Download_Offline_ReportsNetwork()
    {
        _server.Offline = true;
        using var c = Client();
        var r = await c.DownloadAsync(Ref("MT1005"), Item("MT1005", "tom-tat-chuong-1"), Target, default);
        Assert.Equal(DownloadError.Network, r.Error);
        Assert.False(File.Exists(Path.Combine(Target, "tom-tat-c1.md")));
    }

    [Fact]
    public async Task Download_TooLarge_Refused()
    {
        using var c = new LibraryClient(new HttpClient(_server), new LibraryOptions(Base, Path.Combine(_dir, "cache"), Path.Combine(_dir, "m.json"))
        { Gap = TimeSpan.Zero, MaxFileBytes = 10 }, _time);
        var r = await c.DownloadAsync(Ref("MT1005"), Item("MT1005", "tom-tat-chuong-1"), Target, default);
        Assert.Equal(DownloadError.TooLarge, r.Error);
        Assert.Empty(_server.Requests);
    }

    [Fact]
    public async Task Download_NeverOverwritesUserFile()
    {
        Directory.CreateDirectory(Target);
        var mine = Path.Combine(Target, "tom-tat-c1.md");
        File.WriteAllText(mine, "ghi chú của tôi");
        using var c = Client();
        var r = await c.DownloadAsync(Ref("MT1005"), Item("MT1005", "tom-tat-chuong-1"), Target, default);
        Assert.True(r.Ok);
        Assert.Equal("ghi chú của tôi", File.ReadAllText(mine));
        var placed = Assert.Single(r.Paths);
        Assert.NotEqual(mine, placed);
        Assert.Contains("(bản ", Path.GetFileName(placed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Download_NewVersion_ReplacesOwnCopyOnly()
    {
        using var c = Client();
        var item = Item("MT1005", "tom-tat-chuong-1");
        var path = Assert.Single((await c.DownloadAsync(Ref("MT1005"), item, Target, default)).Paths);

        // Thư viện ra bản mới (nội dung khác, updated mới hơn): bản trên máy là cũ.
        var text = "# Chương 1 (bản 2)\n";
        var bytes = Encoding.UTF8.GetBytes(text);
        _server.Override = p => p == "/files/MT1005/tom-tat-c1.md" ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) } : null;
        var v2 = item with { Updated = "2026-10-05", Files = [item.Files![0] with { Size = bytes.Length, Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) }] };
        Assert.Equal(ItemLocal.Outdated, c.LocalState("MT1005", v2));
        var r = await c.DownloadAsync(Ref("MT1005"), v2, Target, default);
        Assert.Equal([path], r.Paths);   // thay đúng bản của app, không thêm "(bản ...)"
        Assert.Equal(text, File.ReadAllText(path));
        Assert.Equal(ItemLocal.Downloaded, c.LocalState("MT1005", v2));
    }

    [Fact]
    public async Task UpdatedDateOnly_SameContent_RefreshesRecordWithoutDownload()
    {
        using var c = Client();
        var item = Item("MT1005", "tom-tat-chuong-1");
        await c.DownloadAsync(Ref("MT1005"), item, Target, default);
        var newer = item with { Updated = "2026-11-01" };
        Assert.Equal(ItemLocal.Outdated, c.LocalState("MT1005", newer));
        var count = _server.Requests.Count;
        Assert.True((await c.DownloadAsync(Ref("MT1005"), newer, Target, default)).Ok);
        Assert.Equal(count, _server.Requests.Count);
        Assert.Equal(ItemLocal.Downloaded, c.LocalState("MT1005", newer));
    }

    // ------------------------------------------------------------------ mục đã gỡ

    [Fact]
    public async Task Removed_DeletesOnlyAppCopy()
    {
        using var c = Client();
        var mt = Ref("MT1005");
        var a = Assert.Single((await c.DownloadAsync(mt, Item("MT1005", "tom-tat-chuong-1"), Target, default)).Paths);
        var b = Assert.Single((await c.DownloadAsync(mt, Item("MT1005", "bang-cong-thuc-gk"), Target, default)).Paths);
        var user = Path.Combine(Target, "cua-toi.md");
        File.WriteAllText(user, "file người dùng tự thêm");
        File.AppendAllText(b, "\nngười dùng sửa thêm");   // đã sửa: không còn là bản cache của app

        var detail = Course("MT1005");
        var gone = detail with
        {
            Items = [.. detail.Items!.Select(i => i.Id is "tom-tat-chuong-1" or "bang-cong-thuc-gk"
                ? new LibraryItem(i.Id, i.Type, null, null, null, null, null, null, null, null, null, null, null, null, i.Added, null, false, true, "gỡ", null, [])
                : i)],
        };
        var deleted = c.ApplyRemovals(gone);
        Assert.Equal([a], deleted);
        Assert.False(File.Exists(a));
        Assert.True(File.Exists(b));
        Assert.True(File.Exists(user));
        Assert.Empty(c.Records());   // cả hai mục bỏ khỏi manifest; file đã sửa giờ là của người dùng
        Assert.Empty(c.ApplyRemovals(gone));
    }

    [Fact]
    public async Task Removed_FixtureItemHasNothingToDownload()
    {
        var removed = Item("MT1005", "ban-cu-da-go");
        Assert.True(removed.Removed);
        using var c = Client();
        Assert.Equal(DownloadError.NoFiles, (await c.DownloadAsync(Ref("MT1005"), removed, Target, default)).Error);
        Assert.Empty(_server.Requests);
    }

    // ------------------------------------------------------------------ gói quiz

    [Fact]
    public async Task QuizPack_Json_InstallsThroughPackImport()
    {
        using var c = Client();
        var path = Assert.Single((await c.DownloadAsync(Ref("MT1005"), Item("MT1005", "quiz-tich-phan-kep"), Target, default)).Paths);
        var fresh = QuizPackHandOff.Decide(path, _ => false);
        Assert.Equal(HandOffKind.Install, fresh.Kind);
        Assert.Equal("thu-vien-mt1005-tich-phan-kep", fresh.Pack!["id"]!.GetValue<string>());
        // Đã cài bản cũ: cập nhật qua khung Luyện tập để giữ kết quả theo fingerprint.
        Assert.Equal(HandOffKind.PracticeImport, QuizPackHandOff.Decide(path, id => id == "thu-vien-mt1005-tich-phan-kep").Kind);
    }

    [Fact]
    public async Task QuizPack_Markdown_GoesToPracticeImport()
    {
        using var c = Client();
        var path = Assert.Single((await c.DownloadAsync(Ref("EE1009"), Item("EE1009", "quiz-ban-do-k"), Path.Combine(_dir, "kts"), default)).Paths);
        var h = QuizPackHandOff.Decide(path, _ => false);
        Assert.Equal(HandOffKind.PracticeImport, h.Kind);
        Assert.Null(h.Pack);
    }

    [Theory]
    [InlineData("""{"format":"studypack/1","id":"../../x"}""", "id gói không hợp lệ")]
    [InlineData("""{"format":"studypack/2","id":"goi-a"}""", "không phải studypack/1")]
    [InlineData("""{"format":1,"id":"goi-a"}""", "không phải studypack/1")]
    [InlineData("""[1,2]""", "không phải studypack/1")]
    [InlineData("""{ hỏng""", "không phải JSON")]
    public void QuizPack_InvalidJson_Refused(string body, string error)
    {
        var file = Path.Combine(_dir, "goi.json");
        File.WriteAllText(file, body);
        var h = QuizPackHandOff.Decide(file, _ => false);
        Assert.Equal(HandOffKind.Invalid, h.Kind);
        Assert.Equal(error, h.Error);
        Assert.Equal(error, PackImport.Check(body, out _));
    }

    [Fact]
    public void QuizPack_OtherExtension_Invalid()
    {
        var file = Path.Combine(_dir, "goi.pdf");
        File.WriteAllText(file, "x");
        Assert.Equal(HandOffKind.Invalid, QuizPackHandOff.Decide(file, _ => false).Kind);
    }

    // ------------------------------------------------------------------ lệnh chính theo loại file

    [Theory]
    [InlineData("a.pdf", null, LibraryFileKind.Pdf)]
    [InlineData("a.MD", null, LibraryFileKind.Markdown)]
    [InlineData("a.zip", null, LibraryFileKind.Zip)]
    [InlineData("a.json", null, LibraryFileKind.Json)]
    [InlineData("a.docx", null, LibraryFileKind.Other)]
    [InlineData("a.bin", "application/pdf", LibraryFileKind.Pdf)]          // có mime thì theo mime
    [InlineData("a.md", "text/markdown; charset=utf-8", LibraryFileKind.Markdown)]
    [InlineData("a.pdf", "application/octet-stream", LibraryFileKind.Other)]
    public void FileKind_MimeThenExtension(string name, string? mime, LibraryFileKind kind) =>
        Assert.Equal(kind, new FileRef(name, 1, new string('a', 64), ["https://a/"], mime).Kind);

    [Fact]
    public void FileRef_ReadsOptionalMime()
    {
        var d = LibraryJson.ParseCourse("{\"schemaVersion\":1,\"id\":\"MT1005\",\"items\":[{\"id\":\"a\",\"type\":\"summary\",\"files\":[{\"name\":\"x\",\"size\":1,\"sha256\":\""
                                        + new string('b', 64) + "\",\"urls\":[\"https://a/\"],\"mime\":\"application/pdf\"}]}]}");
        Assert.Equal(LibraryFileKind.Pdf, d.Items![0].Files![0].Kind);
        Assert.Null(Item("MT1005", "tom-tat-chuong-1").Files![0].Mime);   // fixture chưa có mime: theo đuôi
    }

    [Fact]
    public void PrimaryAction_ByTypeAndFile()
    {
        LibraryAction Of(string course, string id, ItemLocal local = ItemLocal.None) => LibraryActions.Primary(Item(course, id), local);
        Assert.Equal(LibraryAction.OpenWeb, Of("MT1005", "mit-ocw-1802"));
        Assert.Equal(LibraryAction.Install, Of("MT1005", "quiz-tich-phan-kep"));      // .json
        Assert.Equal(LibraryAction.Install, Of("EE1009", "quiz-ban-do-k", ItemLocal.Downloaded));   // .md
        Assert.Equal(LibraryAction.Open, Of("MT1005", "tom-tat-chuong-1"));            // Markdown: mở (chưa có thì tải rồi mở)
        Assert.Equal(LibraryAction.Download, Of("MT1005", "tom-tat-chuong-1", ItemLocal.Outdated));
        Assert.Equal(LibraryAction.Download, Of("EE1009", "danh-sach-video"));        // .txt: chỉ tải về
        Assert.Equal(LibraryAction.Open, Of("EE1009", "danh-sach-video", ItemLocal.Downloaded));
        var pdf = Item("PH1007", "prelab-bai-5");
        pdf = pdf with { Files = [pdf.Files![0] with { Name = "prelab", Mime = "application/pdf" }] };
        Assert.Equal(LibraryAction.Open, LibraryActions.Primary(pdf, ItemLocal.None));
        var zipQuiz = Item("MT1005", "quiz-tich-phan-kep");
        zipQuiz = zipQuiz with { Files = [zipQuiz.Files![0] with { Name = "goi.pdf" }] };
        Assert.Equal(LibraryAction.Open, LibraryActions.Primary(zipQuiz, ItemLocal.None));   // quiz-pack mà không phải .md/.zip/.json
        Assert.True(LibraryActions.OpensAfterDownload(Item("MT1005", "tom-tat-chuong-1")));
        Assert.False(LibraryActions.OpensAfterDownload(Item("EE1009", "danh-sach-video")));
    }

    // ------------------------------------------------------------------ thư viện trên máy (dev) và dữ liệu thật (tùy chọn)

    [Fact]
    public async Task LocalFolderBase_ReadsWithoutNetwork()
    {
        using var c = Client(Path.Combine(Fixtures, "v1"));
        var index = await c.GetIndexAsync(LibraryRefresh.Force, default);
        Assert.Equal(LibraryOrigin.Network, index.Origin);
        var detail = await c.GetCourseAsync(Ref("PH1007"), LibraryRefresh.Force, default);
        Assert.Equal(2, detail.Value!.Items!.Count);
        Assert.Empty(_server.Requests);
    }

    /// <summary>
    /// Đọc thư mục v1/ thật do repo thư viện sinh ra (chỉ đọc). Đặt biến môi trường BKSD_LIBRARY_V1 trỏ tới thư mục đó;
    /// không có thì bỏ qua. Không ghi gì vào thư mục đó: cache và manifest nằm trong thư mục tạm của test.
    /// </summary>
    [LibraryFolderFact]
    public async Task RealLibraryFolder_ParsesAndMatches()
    {
        var root = Environment.GetEnvironmentVariable(LibraryFolderFactAttribute.Variable)!;
        using var c = Client(root);
        var index = (await c.GetIndexAsync(LibraryRefresh.Force, default)).Value;
        Assert.NotNull(index);
        Assert.NotEmpty(index.Courses!);
        foreach (var course in index.Courses!)
        {
            Assert.Matches(LibraryJson.CourseId(), course.Id);
            var d = await c.GetCourseAsync(course, LibraryRefresh.Force, default);
            Assert.True(d.Value is not null, $"{course.Id}: {d.Error}");
            foreach (var i in d.Value.Items!)
            {
                if (i.Removed) { Assert.Empty(i.Files!); continue; }
                Assert.False(string.IsNullOrEmpty(i.Title), i.Id);
                Assert.False(string.IsNullOrEmpty(i.Updated), i.Id);
                if (i.IsLink) Assert.NotNull(LibraryClient.ItemUrl(index, course, i));
                else Assert.NotEmpty(i.Files!);
            }
        }
        if (index.Courses.Count(x => LibraryMatch.Key(x.Code) == "GE4169") is > 1 and var n)
            Assert.Equal(n, LibraryMatch.FindAll(["GE4169"], index.Courses).Count);
    }

    // ------------------------------------------------------------------ server giả

    private sealed record Seen(string Path, string? IfNoneMatch);

    /// <summary>Phục vụ Fixtures/library: library.example/v1/* và /files/*, mirror.library.example/&lt;môn&gt;/&lt;file&gt;. Có ETag.</summary>
    private sealed class FakeServer(string root) : HttpMessageHandler
    {
        public List<Seen> Requests { get; } = [];
        public bool Offline { get; set; }
        public Func<string, HttpResponseMessage?>? Override { get; set; }

        private string? FileFor(Uri u) => u.Host switch
        {
            "library.example" => Path.Combine(root, Uri.UnescapeDataString(u.AbsolutePath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar)),
            "mirror.library.example" => Path.Combine(root, "files", Uri.UnescapeDataString(u.AbsolutePath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar)),
            _ => null,
        };

        public string ETagOf(string path) => Tag(File.ReadAllBytes(FileFor(new Uri("https://library.example" + path))!));

        private static string Tag(byte[] bytes) => "\"" + Convert.ToHexStringLower(SHA256.HashData(bytes))[..16] + "\"";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var u = request.RequestUri!;
            Requests.Add(new Seen(u.AbsolutePath, request.Headers.IfNoneMatch.FirstOrDefault()?.ToString()));
            if (Offline) throw new HttpRequestException("No such host is known.");
            if (Override?.Invoke(u.AbsolutePath) is { } custom) return Task.FromResult(custom);
            if (FileFor(u) is not { } file || !File.Exists(file)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            var bytes = File.ReadAllBytes(file);
            var tag = Tag(bytes);
            if (request.Headers.IfNoneMatch.Any(t => t.ToString() == tag))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            resp.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(tag);
            return Task.FromResult(resp);
        }
    }

    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}

/// <summary>Test chỉ chạy khi biến môi trường BKSD_LIBRARY_V1 trỏ tới thư mục v1/ có index.json.</summary>
public sealed class LibraryFolderFactAttribute : FactAttribute
{
    public const string Variable = "BKSD_LIBRARY_V1";

    public LibraryFolderFactAttribute()
    {
        var dir = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(dir) || !File.Exists(Path.Combine(dir, "index.json")))
            Skip = $"Đặt {Variable} trỏ tới thư mục v1/ của thư viện để chạy test này.";
    }
}
