using System.Text.Json.Nodes;
using SoHocTap.Core;

namespace SoHocTap.Core
{
    // Test không link SettingsConfigStore.cs (nó cần Config thật), nên cung cấp phần partial này; test luôn tự gán Store.
    public static partial class Settings
    {
        private static partial ISettingsStore CreateDefaultStore() => throw new InvalidOperationException("Test phải gán Settings.Store.");
    }
}

namespace SoHocTap.Tests
{
    /// <summary>Store trong bộ nhớ: default đọc từ DefaultConfig.json thật, phần người dùng rỗng, không đụng data của máy.</summary>
    internal sealed class MemoryStore : ISettingsStore
    {
        private readonly JsonObject _defaults = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DefaultConfig.json")))!;
        private readonly JsonObject _user = new();

        private static JsonNode? Walk(JsonObject root, string dotted)
        {
            JsonNode? cur = root;
            foreach (var part in dotted.Split('.'))
                if (cur is not JsonObject o || !o.TryGetPropertyValue(part, out cur)) return null;
            return cur;
        }

        public JsonNode? Node(string dotted) => Walk(_user, dotted) ?? Walk(_defaults, dotted);
        public JsonNode? DefaultValue(string dotted) => Walk(_defaults, dotted);

        public void Write(string dotted, JsonNode? value)
        {
            var parts = dotted.Split('.');
            var o = _user;
            foreach (var p in parts[..^1]) o = o[p] as JsonObject ?? (JsonObject)(o[p] = new JsonObject());
            o[parts[^1]] = value;
        }
    }

    [Collection("Settings")]
    public class SettingsTests
    {
        private static MemoryStore Fresh()
        {
            var s = new MemoryStore();
            Settings.Store = s;
            return s;
        }

        [Fact]
        public void Sso_DefaultsComeFromDefaultConfig_NotFromUserValue()
        {
            Fresh();
            Settings.Sso.RememberDays = 0;
            Settings.Sso.KeepAliveMinutes = 0;
            Assert.Equal(30, Settings.Sso.DefaultRememberDays);
            Assert.Equal(60, Settings.Sso.DefaultKeepAliveMinutes);
        }

        [Fact]
        public void Settings_EveryKeyHasDefault()
        {
            var s = Fresh();
            foreach (var key in Settings.Keys)
            {
                Assert.NotNull(s.Node(key));
                Assert.NotNull(s.DefaultValue(key));
            }
        }

        [Fact]
        public void Settings_WrongTypeFallsBackToDefault()
        {
            var s = Fresh();
            s.Write("notify.hoursBefore", JsonValue.Create("abc"));
            Assert.Equal(24, Settings.Notify.FirstHours);
            s.Write("notify.hoursBefore", JsonValue.Create(-5));
            Assert.Equal(24, Settings.Notify.FirstHours);
            s.Write("notify.hoursBefore", JsonValue.Create(0));
            Assert.Equal(0, Settings.Notify.FirstHours);   // 0 là tắt, hợp lệ
        }

        [Fact]
        public void Settings_SetThenGet()
        {
            Fresh();
            Settings.Sso.RememberDays = 7;
            Settings.App.CloseToTray = false;
            Settings.App.Language = "en";
            Assert.Equal(7, Settings.Sso.RememberDays);
            Assert.False(Settings.App.CloseToTray);
            Assert.Equal("en", Settings.App.Language);
        }

        [Fact]
        public void Settings_NewDefaults()
        {
            Fresh();
            Assert.Equal(6, Settings.Notify.UrgentHours);
            Assert.Equal(24, Settings.Notify.SoonHours);
            Assert.Equal(2048, Settings.Archives.MaxMB);
            Assert.Equal(2, Settings.Notify.LastHours);
        }
    }
}
