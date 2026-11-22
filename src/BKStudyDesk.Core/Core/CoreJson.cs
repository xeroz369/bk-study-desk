using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using SoHocTap.Library;

namespace SoHocTap.Core;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(double[]))]
[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "StringMap")]
[JsonSerializable(typeof(Dictionary<long, HashSet<string>>), TypeInfoPropertyName = "GroupMap")]
[JsonSerializable(typeof(LmsData))]
[JsonSerializable(typeof(LmsMessages))]
[JsonSerializable(typeof(MybkData))]
[JsonSerializable(typeof(CustomEventFile))]
[JsonSerializable(typeof(LibraryIndex))]
[JsonSerializable(typeof(CourseDetail))]
[JsonSerializable(typeof(List<DownloadRecord>))]
internal sealed partial class CoreJson : JsonSerializerContext;

internal static class Js
{
    private static JsonTypeInfo<T> Info<T>(JsonSerializerOptions o) => (JsonTypeInfo<T>)o.GetTypeInfo(typeof(T));

    public static string Serialize<T>(T value, JsonSerializerOptions o) => JsonSerializer.Serialize(value, Info<T>(o));

    public static JsonNode? SerializeToNode<T>(T value, JsonSerializerOptions o) => JsonSerializer.SerializeToNode(value, Info<T>(o));

    public static T? Deserialize<T>(string json, JsonSerializerOptions o) => JsonSerializer.Deserialize(json, Info<T>(o));

    public static T? Deserialize<T>(JsonNode node, JsonSerializerOptions o) => JsonSerializer.Deserialize(node, Info<T>(o));
}
