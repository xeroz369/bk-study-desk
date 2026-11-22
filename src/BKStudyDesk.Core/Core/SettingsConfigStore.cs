using System.Text.Json.Nodes;

namespace SoHocTap.Core;

public static partial class Settings
{
    private static partial ISettingsStore CreateDefaultStore() => new ConfigStore();

    private sealed class ConfigStore : ISettingsStore
    {
        public JsonNode? Node(string dotted) => Config.Node(dotted);
        public JsonNode? DefaultValue(string dotted) => Config.DefaultNode(dotted);
        public void Write(string dotted, JsonNode? value) => Config.Set(dotted, value);
    }
}
