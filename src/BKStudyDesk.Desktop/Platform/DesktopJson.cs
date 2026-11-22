using System.Text.Json;
using System.Text.Json.Serialization;
using SoHocTap.Core;

namespace BKStudyDesk.Desktop.Platform;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(WindowPlacement))]
internal sealed partial class DesktopJson : JsonSerializerContext;

internal static class DesktopJsonOptions
{
    internal static readonly JsonSerializerOptions Store = new(JsonStore.Options) { TypeInfoResolver = DesktopJson.Default };
}
