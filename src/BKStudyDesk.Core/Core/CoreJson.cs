using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using SoHocTap.Data;
using SoHocTap.Library;

namespace SoHocTap.Core;

/// <summary>
/// Metadata JSON sinh sẵn lúc biên dịch (source generation) cho mọi kiểu lõi đọc/ghi bằng System.Text.Json, để bản publish cắt (trim)
/// không cần reflection. Cài đặt (camelCase, converter chịu lỗi...) vẫn nằm ở từng JsonSerializerOptions, options nào dùng thì gắn
/// <c>TypeInfoResolver = CoreJson.Default</c>. Thêm kiểu đọc/ghi mới thì thêm [JsonSerializable] ở đây, thiếu sẽ ném NotSupportedException.
/// </summary>
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(double[]))]
[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "StringMap")]
[JsonSerializable(typeof(Dictionary<long, HashSet<string>>), TypeInfoPropertyName = "GroupMap")]
// Data/ (đọc qua DataJson, ghi qua CustomEvents)
[JsonSerializable(typeof(LmsData))]
[JsonSerializable(typeof(LmsMessages))]
[JsonSerializable(typeof(MybkData))]
[JsonSerializable(typeof(CustomEventFile))]
// Thư viện
[JsonSerializable(typeof(LibraryIndex))]
[JsonSerializable(typeof(CourseDetail))]
[JsonSerializable(typeof(List<DownloadRecord>))]
// Kiểu của Luyện tập khai ở Presentation/Practice/PracticeJson.cs (cùng partial class; test Logic không link thư mục đó).
internal sealed partial class CoreJson : JsonSerializerContext;

/// <summary>Gọi System.Text.Json qua metadata của <see cref="CoreJson"/> (hoặc context khác gắn vào options), không dùng reflection.</summary>
internal static class Js
{
    private static JsonTypeInfo<T> Info<T>(JsonSerializerOptions o) => (JsonTypeInfo<T>)o.GetTypeInfo(typeof(T));

    public static string Serialize<T>(T value, JsonSerializerOptions o) => JsonSerializer.Serialize(value, Info<T>(o));

    public static JsonNode? SerializeToNode<T>(T value, JsonSerializerOptions o) => JsonSerializer.SerializeToNode(value, Info<T>(o));

    public static T? Deserialize<T>(string json, JsonSerializerOptions o) => JsonSerializer.Deserialize(json, Info<T>(o));

    public static T? Deserialize<T>(JsonNode node, JsonSerializerOptions o) => JsonSerializer.Deserialize(node, Info<T>(o));
}
