using System.Text.Json.Nodes;

namespace SoHocTap.Sources;

/// <summary>
/// Một nguồn dữ liệu của trường (LMS, MyBK). Nguồn nào cũng implement chung interface này để scheduler và Api xử lý giống nhau.
/// </summary>
public interface ISource
{
    string Name { get; }

    /// <summary>Status nhẹ (không gửi request): đã kết nối chưa, lần sync cuối, tên người dùng.</summary>
    SourceStatus Status();

    /// <summary>Data đã normalize sẵn cho UI hiển thị (đọc từ file, không gửi request).</summary>
    JsonNode? Data();

    /// <summary>Sync từ trường về. Chỉ lấy phần thay đổi (incremental), trừ khi <paramref name="force"/>.</summary>
    Task SyncAsync(Action<string> log, bool force, CancellationToken ct);

    /// <summary>Chu kỳ tự động sync.</summary>
    TimeSpan Interval { get; }
}

public sealed record SourceStatus(bool Connected, long? SyncedAt, string? User = null, string? Term = null);

/// <summary>
/// Những việc cần browser thật (session SSO, page có Bearer token). Shell implement bằng một WebView2 ẩn;
/// Sources chỉ biết interface này nên không phụ thuộc WinForms.
/// </summary>
public interface IBrowserRunner
{
    /// <summary>Chạy fetch() ngay trong page MyBK /app (dùng token #hid_Token của page).</summary>
    Task<IReadOnlyDictionary<string, FetchResult>> FetchAsync(IReadOnlyList<FetchRequest> requests, CancellationToken ct);

    /// <summary>Mở một page trong WebView ẩn, trả về HTML khi đã tới đúng path.</summary>
    Task<string> PageAsync(string url, CancellationToken ct);
}

/// <summary>Dòng log đặc biệt: nguồn đã lưu phần dữ liệu chính (đang làm tiếp phần chậm như tải tài liệu), giao diện nên đọc lại ngay.</summary>
public static class SyncSignal
{
    public const string DataReady = "\u0001data-ready";

    private const char StepMark = '\u0002';
    private const char WarnMark = '\u0003';

    /// <summary>
    /// Dòng log báo một phần không đọc được (vd. sổ điểm một môn, một API MyBK): đồng bộ vẫn chạy tiếp, nhưng phải hiện cho người dùng.
    /// <paramref name="what"/> là tên phần dễ hiểu ("lịch LMS"), <paramref name="detail"/> là chữ kỹ thuật (hiện ở mục Chi tiết, ghi log).
    /// </summary>
    public static string Warn(string what, string detail) => $"{WarnMark}{what}{DetailMark}{detail}";

    /// <summary>Ngăn cách tên phần và chi tiết trong một mục lỗi.</summary>
    public const char DetailMark = '\u001f';

    public static bool TryParseWarn(string line, out string text)
    {
        text = line.Length > 1 && line[0] == WarnMark ? line[1..] : "";
        return text.Length > 0;
    }

    /// <summary>
    /// Dòng log báo bước đang làm, hiện trên thanh trạng thái: <paramref name="key"/> là key trong file ngôn ngữ (vd. "sync.lms.quizzes"),
    /// kèm số đã xong / tổng (0/0 = không đếm được). Người dùng thấy app đang làm gì, không phải chờ mà không biết.
    /// </summary>
    public static string Step(string key, int done = 0, int total = 0) => $"{StepMark}{done}/{total}{StepMark}{key}";

    public static bool TryParseStep(string line, out string key, out int done, out int total)
    {
        key = ""; done = total = 0;
        if (line.Length < 4 || line[0] != StepMark) return false;
        var end = line.IndexOf(StepMark, 1);
        if (end < 0) return false;
        var counts = line[1..end].Split('/');
        if (counts.Length != 2 || !int.TryParse(counts[0], out done) || !int.TryParse(counts[1], out total)) return false;
        key = line[(end + 1)..];
        return key.Length > 0;
    }
}

/// <summary>Phiên đăng nhập trên server trường đã hết (gặp trang đăng nhập SSO). Khác lỗi mạng: cần người dùng đăng nhập lại.</summary>
public sealed class SessionExpiredException(string message) : InvalidOperationException(message);

public sealed record FetchRequest(string Name, string Url, string Method = "GET", string? Body = null);

public sealed record FetchResult(int Status, string Body, bool HasToken);
