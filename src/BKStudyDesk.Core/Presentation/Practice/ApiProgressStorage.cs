using System.Text;
using SoHocTap.Api;

namespace SoHocTap.Presentation.Practice;

/// <summary>
/// Đọc ghi <c>ket-qua.json</c> qua <see cref="ApiRouter"/> của 1.x (gọi thẳng, không qua HTTP): cùng file, cùng kiểm tra cỡ,
/// cùng bản backup mỗi ngày như khung Luyện tập cũ.
/// </summary>
public sealed class ApiProgressStorage(ApiRouter router) : IProgressStorage
{
    private static readonly IReadOnlyDictionary<string, string> NoQuery = new Dictionary<string, string>();

    public async Task<string?> ReadAsync(CancellationToken ct)
    {
        var r = await router.HandleAsync(new ApiRequest("GET", "/api/state", NoQuery, null), ct);
        if (r.Status != 200) throw new IOException($"GET state: HTTP {r.Status}");
        return Encoding.UTF8.GetString(r.Body);
    }

    public async Task<bool> WriteAsync(string json, CancellationToken ct)
    {
        var r = await router.HandleAsync(new ApiRequest("PUT", "/api/state", NoQuery, json), ct);
        return r.Status == 200;
    }
}
