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

    /// <summary>App thoát: ghi phần chỉ giữ trong RAM (lần kiểm tra cuối của lượt không có gì mới).</summary>
    void Flush() { }
}

public sealed record SourceStatus(bool Connected, long? SyncedAt, string? User = null, string? Term = null);

/// <summary>
/// Những việc cần browser thật (session SSO, page có Bearer token). Shell implement bằng một WebView2 ẩn;
/// Sources chỉ biết interface này nên không phụ thuộc WinForms.
/// </summary>
public interface IBrowserRunner
{
    /// <summary>
    /// Chạy fetch() ngay trong page MyBK /app (dùng token #hid_Token của page). <paramref name="onEach"/> được gọi sau mỗi request
    /// (tên request), để nguồn báo tiến độ theo từng API.
    /// </summary>
    Task<IReadOnlyDictionary<string, FetchResult>> FetchAsync(IReadOnlyList<FetchRequest> requests, CancellationToken ct, Action<string>? onEach = null);

    /// <summary>HTML của trang url (mở trong WebView ẩn). mustContain: chữ phải có trong trang mới coi là đã tới (trang trung gian thì mở lại).</summary>
    Task<string> PageAsync(string url, CancellationToken ct, string? mustContain = null);
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
    /// Dòng log báo tiến độ (câu đang làm, đếm, tên đi kèm, phần đã xong theo trọng số), hiện trên thanh trạng thái.
    /// Nguồn dựng bằng <see cref="SyncPlan"/>. Người dùng thấy app đang làm gì, không phải chờ mà không biết.
    /// Dạng: \u0002count/of/permille\u0002key[\u001fdetail] (permille -1 = chưa biết tổng).
    /// </summary>
    public static string Progress(SyncProgress p) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{StepMark}{p.Count}/{p.Of}/{p.Permille ?? -1}{StepMark}{p.Key}{(p.Detail is { Length: > 0 } d ? DetailMark + d : "")}");

    public static bool TryParseProgress(string line, out SyncProgress progress)
    {
        progress = default;
        if (line.Length < 4 || line[0] != StepMark) return false;
        var end = line.IndexOf(StepMark, 1);
        if (end < 0) return false;
        var n = line[1..end].Split('/');
        if (n.Length != 3) return false;
        var v = new int[3];
        for (var i = 0; i < 3; i++)
            if (!int.TryParse(n[i], System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out v[i])) return false;
        var rest = line[(end + 1)..].Split(DetailMark, 2);
        if (rest[0].Length == 0) return false;
        progress = new SyncProgress(rest[0], v[0], v[1], rest.Length > 1 ? rest[1] : null, v[2] < 0 ? null : Math.Min(v[2], 1000));
        return true;
    }
}

/// <summary>Phiên đăng nhập trên server trường đã hết (gặp trang đăng nhập SSO). Khác lỗi mạng: cần người dùng đăng nhập lại.</summary>
public sealed class SessionExpiredException(string message) : InvalidOperationException(message);

public sealed record FetchRequest(string Name, string Url, string Method = "GET", string? Body = null);

public sealed record FetchResult(int Status, string Body, bool HasToken);
