using System.Text.Json.Nodes;

namespace SoHocTap.Core;

public static partial class Settings
{
    // Bản của app: đọc và ghi qua Config. Test không link file này mà tự gán Store.
    private static partial ISettingsStore CreateDefaultStore() => new ConfigStore();

    private sealed class ConfigStore : ISettingsStore
    {
        public JsonNode? Node(string dotted) => Config.Node(dotted);
        public JsonNode? DefaultValue(string dotted) => Config.DefaultNode(dotted);
        public void Write(string dotted, JsonNode? value) => Config.Set(dotted, value);
    }
}
