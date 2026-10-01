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

public sealed record FetchRequest(string Name, string Url, string Method = "GET", string? Body = null);

public sealed record FetchResult(int Status, string Body, bool HasToken);
