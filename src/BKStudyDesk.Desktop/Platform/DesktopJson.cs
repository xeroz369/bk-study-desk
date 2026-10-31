using System.Text.Json;
using System.Text.Json.Serialization;
using SoHocTap.Core;

namespace BKStudyDesk.Desktop.Platform;

/// <summary>
/// Metadata JSON sinh sẵn cho các kiểu riêng của app desktop (xem <c>CoreJson</c> bên lõi), để bản publish cắt (trim) không cần reflection.
/// Thêm kiểu đọc/ghi mới ở app thì thêm [JsonSerializable] ở đây.
/// </summary>
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(WindowPlacement))]
internal sealed partial class DesktopJson : JsonSerializerContext;

internal static class DesktopJsonOptions
{
    /// <summary>Options ghi file của lõi (camelCase, xuống dòng, giữ tiếng Việt) nhưng tra metadata ở DesktopJson.
    /// Để ngoài DesktopJson: trong class sinh code, thứ tự khởi tạo static giữa các file partial không đảm bảo (Default còn null).</summary>
    internal static readonly JsonSerializerOptions Store = new(JsonStore.Options) { TypeInfoResolver = DesktopJson.Default };
}
